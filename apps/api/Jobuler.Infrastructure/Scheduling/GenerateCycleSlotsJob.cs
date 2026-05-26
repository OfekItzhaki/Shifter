using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Domain.Groups;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Recurring background service that generates shift slots for upcoming scheduling cycles
/// in self-service groups. Runs daily at midnight UTC.
///
/// For each self-service group with a <see cref="SelfServiceConfig"/>, the job:
/// 1. Determines if an upcoming cycle exists that hasn't had slots generated yet.
/// 2. If no upcoming cycle exists, creates one based on the group's cycle duration.
/// 3. Calls <see cref="ISlotGenerationService.GenerateSlotsForCycleAsync"/> to generate slots.
///
/// Execution is idempotent — running multiple times produces the same result because
/// the SlotGenerationService skips slots that already exist for a given template+date.
/// </summary>
public class GenerateCycleSlotsJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GenerateCycleSlotsJob> _logger;

    /// <summary>
    /// Interval between runs. Targets daily execution at midnight UTC.
    /// The job calculates the delay to the next midnight on each iteration.
    /// </summary>
    private static readonly TimeSpan FallbackInterval = TimeSpan.FromHours(24);

    public GenerateCycleSlotsJob(IServiceScopeFactory scopeFactory, ILogger<GenerateCycleSlotsJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait briefly on startup to let the application stabilize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateSlotsForAllGroupsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "GenerateCycleSlotsJob failed during execution.");
            }

            // Wait until next midnight UTC
            var delay = GetDelayUntilNextMidnightUtc();
            _logger.LogDebug("GenerateCycleSlotsJob: next run in {Delay}.", delay);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task GenerateSlotsForAllGroupsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slotGenerationService = scope.ServiceProvider.GetRequiredService<ISlotGenerationService>();

        var now = DateTime.UtcNow;

        // Query all active self-service groups that have a config
        var selfServiceGroups = await db.Groups.AsNoTracking()
            .Where(g => g.IsActive
                && g.DeletedAt == null
                && g.SchedulingMode == SchedulingMode.SelfService)
            .Select(g => new { g.Id, g.SpaceId, g.Name })
            .ToListAsync(ct);

        if (selfServiceGroups.Count == 0)
        {
            _logger.LogInformation("GenerateCycleSlotsJob: no self-service groups found. Nothing to do.");
            return;
        }

        _logger.LogInformation(
            "GenerateCycleSlotsJob: processing {Count} self-service group(s).",
            selfServiceGroups.Count);

        foreach (var group in selfServiceGroups)
        {
            try
            {
                await ProcessGroupAsync(db, slotGenerationService, group.SpaceId, group.Id, group.Name, now, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "GenerateCycleSlotsJob: failed to process group {GroupName} ({GroupId}).",
                    group.Name, group.Id);
            }
        }
    }

    private async Task ProcessGroupAsync(
        AppDbContext db,
        ISlotGenerationService slotGenerationService,
        Guid spaceId,
        Guid groupId,
        string groupName,
        DateTime now,
        CancellationToken ct)
    {
        // Load the group's self-service config
        var config = await db.SelfServiceConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.GroupId == groupId, ct);

        if (config is null)
        {
            _logger.LogDebug(
                "GenerateCycleSlotsJob: group {GroupName} ({GroupId}) has no SelfServiceConfig. Skipping.",
                groupName, groupId);
            return;
        }

        // Find upcoming cycles that haven't been generated yet
        var unGeneratedCycles = await db.SchedulingCycles
            .Where(c => c.GroupId == groupId && !c.IsGenerated && c.StartsAt > now)
            .OrderBy(c => c.StartsAt)
            .ToListAsync(ct);

        if (unGeneratedCycles.Count > 0)
        {
            // Generate slots for each un-generated upcoming cycle
            foreach (var cycle in unGeneratedCycles)
            {
                _logger.LogInformation(
                    "GenerateCycleSlotsJob: generating slots for group {GroupName} ({GroupId}), cycle {CycleId} ({StartsAt} - {EndsAt}).",
                    groupName, groupId, cycle.Id, cycle.StartsAt, cycle.EndsAt);

                await slotGenerationService.GenerateSlotsForCycleAsync(groupId, cycle.Id, ct);
            }

            return;
        }

        // No upcoming un-generated cycles exist — check if we need to create the next cycle.
        // Find the latest cycle for this group (generated or not).
        var latestCycle = await db.SchedulingCycles.AsNoTracking()
            .Where(c => c.GroupId == groupId)
            .OrderByDescending(c => c.EndsAt)
            .FirstOrDefaultAsync(ct);

        // Determine the next cycle start
        DateTime nextCycleStart;
        if (latestCycle is not null)
        {
            // Next cycle starts where the last one ended
            nextCycleStart = latestCycle.EndsAt;
        }
        else
        {
            // No cycles exist yet — start from the next day at midnight UTC
            nextCycleStart = now.Date.AddDays(1);
        }

        // Only create a cycle if it starts within the request window open offset
        // (i.e., the cycle is upcoming enough that we should prepare slots)
        var lookAheadHours = config.RequestWindowOpenOffsetHours + (config.CycleDurationDays * 24);
        if (nextCycleStart > now.AddHours(lookAheadHours))
        {
            _logger.LogDebug(
                "GenerateCycleSlotsJob: next cycle for group {GroupName} ({GroupId}) starts at {NextStart}, which is beyond the look-ahead window. Skipping.",
                groupName, groupId, nextCycleStart);
            return;
        }

        // Create the next cycle
        var nextCycleEnd = nextCycleStart.AddDays(config.CycleDurationDays);
        var requestWindowOpens = nextCycleStart.AddHours(-config.RequestWindowOpenOffsetHours);
        var requestWindowCloses = nextCycleStart.AddHours(-config.RequestWindowCloseOffsetHours);

        var newCycle = Domain.Scheduling.SchedulingCycle.Create(
            spaceId: spaceId,
            groupId: groupId,
            startsAt: nextCycleStart,
            endsAt: nextCycleEnd,
            requestWindowOpensAt: requestWindowOpens,
            requestWindowClosesAt: requestWindowCloses);

        db.SchedulingCycles.Add(newCycle);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "GenerateCycleSlotsJob: created new cycle {CycleId} for group {GroupName} ({GroupId}), {StartsAt} - {EndsAt}. Generating slots.",
            newCycle.Id, groupName, groupId, nextCycleStart, nextCycleEnd);

        // Generate slots for the newly created cycle
        await slotGenerationService.GenerateSlotsForCycleAsync(groupId, newCycle.Id, ct);
    }

    /// <summary>
    /// Calculates the delay until the next midnight UTC.
    /// If we're exactly at midnight, waits a full 24 hours.
    /// </summary>
    private static TimeSpan GetDelayUntilNextMidnightUtc()
    {
        var now = DateTime.UtcNow;
        var nextMidnight = now.Date.AddDays(1);
        var delay = nextMidnight - now;

        // Safety: if delay is very small (< 1 minute), wait until the following midnight
        if (delay < TimeSpan.FromMinutes(1))
            delay = delay.Add(FallbackInterval);

        return delay;
    }
}
