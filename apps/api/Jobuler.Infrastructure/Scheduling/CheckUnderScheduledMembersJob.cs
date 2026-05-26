using Jobuler.Application.Scheduling.SelfService.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Recurring background service that detects scheduling cycles whose request window
/// has just closed and dispatches <see cref="CheckUnderScheduledMembersCommand"/>
/// for each, flagging members below Min_Shifts.
/// Runs every 5 minutes.
/// Requirements: 5.4, 6.7, 13.6
/// </summary>
public class CheckUnderScheduledMembersJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CheckUnderScheduledMembersJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public CheckUnderScheduledMembersJob(IServiceScopeFactory scopeFactory, ILogger<CheckUnderScheduledMembersJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 2 minutes after startup before first run to let the app stabilize
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckUnderScheduledMembersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckUnderScheduledMembersJob failed during execution");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CheckUnderScheduledMembersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-5); // Look back one interval to catch recently closed windows

        // Find scheduling cycles whose request window closed within the last 5 minutes
        // and belong to self-service groups
        var recentlyClosedCycles = await db.SchedulingCycles
            .AsNoTracking()
            .Where(c => c.RequestWindowClosesAt > windowStart && c.RequestWindowClosesAt <= now)
            .ToListAsync(ct);

        if (recentlyClosedCycles.Count == 0)
        {
            _logger.LogDebug("CheckUnderScheduledMembersJob: no cycles with recently closed request windows");
            return;
        }

        // Filter to only self-service groups
        var groupIds = recentlyClosedCycles.Select(c => c.GroupId).Distinct().ToList();
        var selfServiceGroupIds = await db.Groups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.Id) && g.SchedulingMode == SchedulingMode.SelfService)
            .Select(g => g.Id)
            .ToListAsync(ct);

        var eligibleCycles = recentlyClosedCycles
            .Where(c => selfServiceGroupIds.Contains(c.GroupId))
            .ToList();

        if (eligibleCycles.Count == 0)
        {
            _logger.LogDebug("CheckUnderScheduledMembersJob: no self-service cycles with recently closed request windows");
            return;
        }

        _logger.LogInformation(
            "CheckUnderScheduledMembersJob: found {Count} cycle(s) with recently closed request windows",
            eligibleCycles.Count);

        foreach (var cycle in eligibleCycles)
        {
            try
            {
                var command = new CheckUnderScheduledMembersCommand(
                    SpaceId: cycle.SpaceId,
                    GroupId: cycle.GroupId,
                    SchedulingCycleId: cycle.Id);

                var result = await mediator.Send(command, ct);

                _logger.LogInformation(
                    "CheckUnderScheduledMembersJob: processed cycle {CycleId} for group {GroupId} — {Count} under-scheduled member(s)",
                    cycle.Id, cycle.GroupId, result.UnderScheduledMembers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CheckUnderScheduledMembersJob: failed to process cycle {CycleId} for group {GroupId}",
                    cycle.Id, cycle.GroupId);
            }
        }
    }
}
