using Jobuler.Application.Notifications;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Notifications;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Recurring background service that detects scheduling cycles whose request window
/// has just opened and sends notifications to all group members.
/// Runs every 5 minutes.
/// Requirements: 13.4
/// </summary>
public class NotifyRequestWindowOpenJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotifyRequestWindowOpenJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public NotifyRequestWindowOpenJob(IServiceScopeFactory scopeFactory, ILogger<NotifyRequestWindowOpenJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 1 minute after startup before first run to let the app stabilize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await NotifyNewlyOpenedWindowsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotifyRequestWindowOpenJob failed during execution");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task NotifyNewlyOpenedWindowsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pushSender = scope.ServiceProvider.GetRequiredService<IPushNotificationSender>();

        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-5); // Look back one interval to catch recently opened windows

        // Find scheduling cycles whose request window opened within the last 5 minutes
        var recentlyOpenedCycles = await db.SchedulingCycles
            .AsNoTracking()
            .Where(c => c.RequestWindowOpensAt > windowStart && c.RequestWindowOpensAt <= now)
            .ToListAsync(ct);

        if (recentlyOpenedCycles.Count == 0)
        {
            _logger.LogDebug("NotifyRequestWindowOpenJob: no cycles with recently opened request windows");
            return;
        }

        // Filter to only self-service groups
        var groupIds = recentlyOpenedCycles.Select(c => c.GroupId).Distinct().ToList();
        var selfServiceGroupIds = await db.Groups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.Id) && g.SchedulingMode == SchedulingMode.SelfService)
            .Select(g => g.Id)
            .ToListAsync(ct);

        var eligibleCycles = recentlyOpenedCycles
            .Where(c => selfServiceGroupIds.Contains(c.GroupId))
            .ToList();

        if (eligibleCycles.Count == 0)
        {
            _logger.LogDebug("NotifyRequestWindowOpenJob: no self-service cycles with recently opened request windows");
            return;
        }

        _logger.LogInformation(
            "NotifyRequestWindowOpenJob: found {Count} cycle(s) with recently opened request windows",
            eligibleCycles.Count);

        foreach (var cycle in eligibleCycles)
        {
            try
            {
                await NotifyGroupMembersAsync(db, pushSender, cycle, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "NotifyRequestWindowOpenJob: failed to notify members for cycle {CycleId} in group {GroupId}",
                    cycle.Id, cycle.GroupId);
            }
        }
    }

    private async Task NotifyGroupMembersAsync(
        AppDbContext db,
        IPushNotificationSender pushSender,
        Domain.Scheduling.SchedulingCycle cycle,
        CancellationToken ct)
    {
        // Get all members of the group who have linked user accounts
        var memberPersonIds = await db.GroupMemberships
            .AsNoTracking()
            .Where(gm => gm.GroupId == cycle.GroupId && gm.SpaceId == cycle.SpaceId)
            .Select(gm => gm.PersonId)
            .ToListAsync(ct);

        if (memberPersonIds.Count == 0)
        {
            _logger.LogDebug(
                "NotifyRequestWindowOpenJob: no members in group {GroupId} for cycle {CycleId}",
                cycle.GroupId, cycle.Id);
            return;
        }

        // Resolve person IDs to user IDs (only members with linked accounts can receive notifications)
        var memberUserIds = await db.People
            .AsNoTracking()
            .Where(p => memberPersonIds.Contains(p.Id) && p.SpaceId == cycle.SpaceId && p.LinkedUserId != null)
            .Select(p => p.LinkedUserId!.Value)
            .Distinct()
            .ToListAsync(ct);

        if (memberUserIds.Count == 0)
        {
            _logger.LogDebug(
                "NotifyRequestWindowOpenJob: no linked users in group {GroupId} for cycle {CycleId}",
                cycle.GroupId, cycle.Id);
            return;
        }

        var title = "Request Window Open";
        var body = $"The shift request window is now open for {cycle.StartsAt:MMM dd} – {cycle.EndsAt:MMM dd}. " +
                   $"Submit your requests before {cycle.RequestWindowClosesAt:MMM dd, HH:mm} UTC.";

        var deduplicationHash = $"request_window_open_{cycle.Id}";

        // Check if we already sent this notification (idempotency)
        var alreadySent = await db.Notifications
            .AnyAsync(n => n.DeduplicationHash == deduplicationHash, ct);

        if (alreadySent)
        {
            _logger.LogDebug(
                "NotifyRequestWindowOpenJob: notification already sent for cycle {CycleId}. Skipping.",
                cycle.Id);
            return;
        }

        // Create in-app notifications for all members
        var notifications = memberUserIds.Select(userId =>
            Notification.CreateWithDedup(
                spaceId: cycle.SpaceId,
                userId: userId,
                eventType: "request_window_open",
                title: title,
                body: body,
                metadataJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    cycleId = cycle.Id,
                    groupId = cycle.GroupId,
                    startsAt = cycle.StartsAt,
                    endsAt = cycle.EndsAt,
                    requestWindowClosesAt = cycle.RequestWindowClosesAt
                }),
                deduplicationHash: deduplicationHash))
            .ToList();

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "NotifyRequestWindowOpenJob: sent {Count} notification(s) for cycle {CycleId} in group {GroupId}",
            memberUserIds.Count, cycle.Id, cycle.GroupId);

        // Deliver push notifications — failures must never affect in-app persistence
        try
        {
            var payload = new PushPayload(
                Title: title,
                Body: body,
                Icon: "/favicon.jpeg",
                Url: "/shifts");

            await pushSender.SendPushToUsersAsync(memberUserIds, cycle.SpaceId, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "NotifyRequestWindowOpenJob: push notification delivery failed for cycle {CycleId}. In-app notifications were persisted successfully.",
                cycle.Id);
        }
    }
}
