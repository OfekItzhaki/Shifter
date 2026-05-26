using Jobuler.Application.Notifications;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Recurring background service that expires pending swap requests older than 72 hours.
/// Runs every hour and marks expired swap requests, notifying the initiator.
/// </summary>
public class ExpireSwapRequestsJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpireSwapRequestsJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public ExpireSwapRequestsJob(IServiceScopeFactory scopeFactory, ILogger<ExpireSwapRequestsJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 3 minutes after startup before first run to let the app stabilize
        await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireSwapRequestsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExpireSwapRequestsJob failed during execution");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ExpireSwapRequestsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pushSender = scope.ServiceProvider.GetRequiredService<IPushNotificationSender>();

        var now = DateTime.UtcNow;

        // Find all pending swap requests that have passed their expiry time
        var expiredSwaps = await db.SwapRequests
            .Where(sr => sr.Status == SwapRequestStatus.Pending && sr.ExpiresAt != null && sr.ExpiresAt < now)
            .ToListAsync(ct);

        if (expiredSwaps.Count == 0)
        {
            _logger.LogDebug("ExpireSwapRequestsJob: no expired swap requests found");
            return;
        }

        foreach (var swap in expiredSwaps)
        {
            swap.Expire();

            _logger.LogInformation(
                "ExpireSwapRequestsJob: expired swap request {SwapRequestId} (initiator: {InitiatorPersonId}, target: {TargetPersonId})",
                swap.Id, swap.InitiatorPersonId, swap.TargetPersonId);
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("ExpireSwapRequestsJob: expired {Count} swap request(s)", expiredSwaps.Count);

        // Req 12.7: Notify initiators about expired swap requests
        foreach (var swap in expiredSwaps)
        {
            try
            {
                var initiator = await db.People
                    .AsNoTracking()
                    .Where(p => p.Id == swap.InitiatorPersonId && p.SpaceId == swap.SpaceId)
                    .Select(p => new { p.LinkedUserId })
                    .FirstOrDefaultAsync(ct);

                if (initiator?.LinkedUserId is null)
                    continue;

                var title = "Swap Request Expired";
                var body = "Your shift swap proposal has expired after 72 hours without a response.";

                var notification = Notification.Create(
                    spaceId: swap.SpaceId,
                    userId: initiator.LinkedUserId.Value,
                    eventType: "self_service.swap_expired",
                    title: title,
                    body: body,
                    metadataJson: JsonSerializer.Serialize(new
                    {
                        swapRequestId = swap.Id,
                        groupId = swap.GroupId
                    }));

                db.Notifications.Add(notification);
                await db.SaveChangesAsync(ct);

                // Attempt push notification — failure does not affect in-app (Req 13.7)
                try
                {
                    var payload = new PushPayload(
                        Title: title,
                        Body: body,
                        Icon: "/favicon.jpeg",
                        Url: "/shifts/swaps");

                    await pushSender.SendPushToUserAsync(initiator.LinkedUserId.Value, swap.SpaceId, payload, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "ExpireSwapRequestsJob: push notification delivery failed for swap {SwapRequestId}. " +
                        "In-app notification was persisted successfully.",
                        swap.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ExpireSwapRequestsJob: failed to send expiry notification for swap {SwapRequestId}",
                    swap.Id);
            }
        }
    }
}
