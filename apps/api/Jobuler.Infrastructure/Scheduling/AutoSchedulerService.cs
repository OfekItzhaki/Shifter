using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
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

    // System user ID used for auto-triggered runs
    private static readonly Guid SystemUserId = Guid.Empty;

    public AutoSchedulerService(IServiceScopeFactory scopeFactory, ILogger<AutoSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
            .Select(g => new { g.Id, g.SpaceId, g.Name, g.SolverHorizonDays })
            .ToListAsync(ct);

        _logger.LogDebug("AutoScheduler checking {Count} groups.", groups.Count);

        foreach (var group in groups)
        {
            try
            {
                await CheckGroupAsync(db, mediator, queue, group.SpaceId, group.Id, group.Name, group.SolverHorizonDays, now, ct);
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
        DateTime now,
        CancellationToken ct)
    {
        var horizonEnd = now.AddDays(horizonDays);

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
            .AnyAsync(v => v.SpaceId == spaceId && v.Status == "draft", ct);

        if (hasDraft)
        {
            _logger.LogDebug("AutoScheduler: space {SpaceId} has an unreviewed draft. Skipping.", spaceId);
            return;
        }

        // Get the current published schedule's coverage
        var publishedVersion = await db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == spaceId && v.Status == "published")
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
            // Check if the published schedule covers the horizon
            // Look at the latest assignment end time for this space
            var latestAssignmentEnd = await db.Assignments.AsNoTracking()
                .Where(a => a.ScheduleVersionId == publishedVersion.Id && a.SpaceId == spaceId)
                .Join(db.TaskSlots, a => a.TaskSlotId, s => s.Id, (a, s) => s.EndsAt)
                .MaxAsync(endsAt => (DateTime?)endsAt, ct);

            if (latestAssignmentEnd is null || latestAssignmentEnd < horizonEnd)
            {
                needsNewSchedule = true;
                _logger.LogInformation(
                    "AutoScheduler: group {GroupName} ({GroupId}) schedule ends {End:yyyy-MM-dd}, horizon requires {Horizon:yyyy-MM-dd}. Triggering solver.",
                    groupName, groupId,
                    latestAssignmentEnd?.ToString("yyyy-MM-dd") ?? "never",
                    horizonEnd.ToString("yyyy-MM-dd"));
            }
            else
            {
                needsNewSchedule = false;
                _logger.LogDebug(
                    "AutoScheduler: group {GroupName} ({GroupId}) schedule covers until {End:yyyy-MM-dd}. OK.",
                    groupName, groupId, latestAssignmentEnd.Value);
            }
        }

        if (!needsNewSchedule) return;

        // Trigger the solver
        try
        {
            var runId = await mediator.Send(
                new TriggerSolverCommand(spaceId, "standard", SystemUserId), ct);

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
