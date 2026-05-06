using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Scheduling;using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Background service that automatically triggers the solver for each group
/// when the current published schedule doesn't cover the configured horizon.
///
/// Runs once per day (configurable). For each active group:
/// - If no published schedule exists → trigger solver
/// - If the published schedule's latest assignment ends within the next
///   SolverHorizonDays → trigger solver to extend coverage
///
/// Uses a system user ID (Guid.Empty) as the requesting user for auto-runs.
/// </summary>
public class AutoSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoSchedulerService> _logger;

    // How often to check (default: every 6 hours)
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    // System user ID used for auto-triggered runs — null means system/automated
    private static readonly Guid? SystemUserId = null;

    public AutoSchedulerService(IServiceScopeFactory scopeFactory, ILogger<AutoSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Auto-scheduler is disabled by default — only runs when AutoScheduler:Enabled = true in config.
        // This prevents it from creating unwanted drafts during development or when the space
        // doesn't have enough people to produce a valid schedule.
        var scope = _scopeFactory.CreateScope();
        var config = scope.ServiceProvider.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
        var enabled = config?["AutoScheduler:Enabled"] == "true";

        if (!enabled)
        {
            _logger.LogInformation("AutoScheduler is disabled (AutoScheduler:Enabled != true). Skipping.");
            return;
        }

        _logger.LogInformation("AutoScheduler started. Will check every {Interval}.", CheckInterval);

        // Wait a bit on startup to let the API fully initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndTriggerAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "AutoScheduler encountered an error.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckAndTriggerAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var queue = scope.ServiceProvider.GetRequiredService<ISolverJobQueue>();

        var now = DateTime.UtcNow;

        // Get all active groups with their solver horizon
        var groups = await db.Groups.AsNoTracking()
            .Where(g => g.IsActive && g.DeletedAt == null)
            .Select(g => new { g.Id, g.SpaceId, g.Name, g.SolverHorizonDays, g.SolverStartDateTime })
            .ToListAsync(ct);

        _logger.LogDebug("AutoScheduler checking {Count} groups.", groups.Count);

        foreach (var group in groups)
        {
            try
            {
                await CheckGroupAsync(db, mediator, queue, group.SpaceId, group.Id, group.Name, group.SolverHorizonDays, group.SolverStartDateTime, now, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoScheduler failed for group {GroupId} ({GroupName}).", group.Id, group.Name);
            }
        }
    }

    private async Task CheckGroupAsync(
        AppDbContext db,
        IMediator mediator,
        ISolverJobQueue queue,
        Guid spaceId,
        Guid groupId,
        string groupName,
        int horizonDays,
        DateTime? solverStartDateTime,
        DateTime now,
        CancellationToken ct)
    {
        // Check if there's already a pending/running solver job for this space
        var hasActiveRun = await db.ScheduleRuns.AsNoTracking()
            .AnyAsync(r => r.SpaceId == spaceId &&
                          (r.Status == ScheduleRunStatus.Queued || r.Status == ScheduleRunStatus.Running), ct);

        if (hasActiveRun)
        {
            _logger.LogDebug("AutoScheduler: space {SpaceId} already has an active solver run. Skipping.", spaceId);
            return;
        }

        // Check if there's already a draft version (admin hasn't reviewed yet)
        var hasDraft = await db.ScheduleVersions.AsNoTracking()
            .AnyAsync(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Draft, ct);

        if (hasDraft)
        {
            _logger.LogDebug("AutoScheduler: space {SpaceId} has an unreviewed draft. Skipping.", spaceId);
            return;
        }

        // Don't auto-trigger if there was a recent failure (within the last 2 hours).
        // This prevents the auto-scheduler from hammering a space that can't be scheduled
        // (e.g. not enough people) and creating noise for the admin.
        var recentFailure = await db.ScheduleRuns.AsNoTracking()
            .AnyAsync(r => r.SpaceId == spaceId
                && r.Status == ScheduleRunStatus.Failed
                && r.CreatedAt >= now.AddHours(-2), ct);

        if (recentFailure)
        {
            _logger.LogDebug("AutoScheduler: space {SpaceId} had a recent failure. Skipping auto-trigger.", spaceId);
            return;
        }

        // Get the current published schedule's coverage
        var publishedVersion = await db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        bool needsNewSchedule;

        if (publishedVersion is null)
        {
            // No published schedule at all — trigger
            needsNewSchedule = true;
            _logger.LogInformation(
                "AutoScheduler: group {GroupName} ({GroupId}) has no published schedule. Triggering solver.",
                groupName, groupId);
        }
        else
        {
            // Slot-level gap scan: check every active task slot in the horizon.
            // If any slot has no published assignment, the schedule is incomplete.
            var horizonStartDt = now.Date;
            var horizonEndDt   = now.Date.AddDays(horizonDays);

            var slotIds = await db.TaskSlots.AsNoTracking()
                .Where(s => s.SpaceId == spaceId
                    && s.Status == Domain.Tasks.TaskSlotStatus.Active
                    && s.StartsAt >= horizonStartDt
                    && s.StartsAt < horizonEndDt)
                .Select(s => s.Id)
                .ToListAsync(ct);

            if (slotIds.Count == 0)
            {
                needsNewSchedule = false;
                _logger.LogDebug(
                    "AutoScheduler: group {GroupName} ({GroupId}) has no active slots in horizon. OK.",
                    groupName, groupId);
            }
            else
            {
                // Find which slots already have a published assignment
                var coveredSlotIds = await db.Assignments.AsNoTracking()
                    .Where(a => a.ScheduleVersionId == publishedVersion.Id
                        && a.SpaceId == spaceId
                        && slotIds.Contains(a.TaskSlotId))
                    .Select(a => a.TaskSlotId)
                    .Distinct()
                    .ToListAsync(ct);

                var coveredSet = coveredSlotIds.ToHashSet();
                var gapSlotIds = slotIds.Where(id => !coveredSet.Contains(id)).ToList();

                if (gapSlotIds.Count > 0)
                {
                    needsNewSchedule = true;
                    _logger.LogInformation(
                        "AutoScheduler: group {GroupName} ({GroupId}) has {GapCount} uncovered slot(s) in horizon. Gap slot IDs: {GapIds}. Triggering solver.",
                        groupName, groupId, gapSlotIds.Count,
                        string.Join(", ", gapSlotIds.Take(10).Select(id => id.ToString())));
                }
                else
                {
                    needsNewSchedule = false;
                    _logger.LogDebug(
                        "AutoScheduler: group {GroupName} ({GroupId}) all {Count} slot(s) covered. OK.",
                        groupName, groupId, slotIds.Count);
                }
            }
        }

        if (!needsNewSchedule) return;

        // Trigger the solver
        try
        {
            var runId = await mediator.Send(
                new TriggerSolverCommand(spaceId, "standard", SystemUserId,
                    GroupId: groupId,
                    StartTime: solverStartDateTime), ct);

            _logger.LogInformation(
                "AutoScheduler: triggered solver for group {GroupName} ({GroupId}). RunId={RunId}",
                groupName, groupId, runId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AutoScheduler: failed to trigger solver for group {GroupName} ({GroupId}). " +
                "Redis may be unavailable.",
                groupName, groupId);
        }
    }
}
