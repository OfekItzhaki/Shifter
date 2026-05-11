using System.Net;
using Jobuler.Application.Notifications;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;

namespace Jobuler.Infrastructure.Notifications;

/// <summary>
/// Sends Web Push notifications to subscribed devices using VAPID authentication (RFC 8292)
/// and encrypted payloads (RFC 8291) via the WebPush NuGet library.
/// Handles push service error responses gracefully — never throws.
/// </summary>
public class PushNotificationSender : IPushNotificationSender
{
    private readonly AppDbContext _db;
    private readonly ILogger<PushNotificationSender> _logger;
    private readonly VapidDetails _vapidDetails;

    public PushNotificationSender(
        AppDbContext db,
        IOptions<VapidSettings> vapidOptions,
        ILogger<PushNotificationSender> logger)
    {
        _db = db;
        _logger = logger;

        var settings = vapidOptions.Value;
        _vapidDetails = new VapidDetails(
            settings.Subject,
            settings.PublicKey,
            settings.PrivateKey);
    }

    /// <inheritdoc />
    public async Task SendPushToUserAsync(
        Guid userId, Guid spaceId,
        PushPayload payload, CancellationToken ct = default)
    {
        await SendPushToUsersAsync(new[] { userId }, spaceId, payload, ct);
    }

    /// <inheritdoc />
    public async Task SendPushToUsersAsync(
        IEnumerable<Guid> userIds, Guid spaceId,
        PushPayload payload, CancellationToken ct = default)
    {
        try
        {
            var userIdList = userIds.ToList();
            if (userIdList.Count == 0) return;

            var subscriptions = await _db.PushSubscriptions
                .Where(s => s.SpaceId == spaceId && userIdList.Contains(s.UserId))
                .ToListAsync(ct);

            if (subscriptions.Count == 0) return;

            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                title = payload.Title,
                body = payload.Body,
                icon = payload.Icon ?? "/favicon.jpeg",
                url = payload.Url ?? "/",
                tag = payload.Tag,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            var expiredSubscriptionIds = new List<Guid>();

            foreach (var sub in subscriptions)
            {
                await SendToSubscriptionAsync(sub, jsonPayload, expiredSubscriptionIds);
            }

            // Cleanup expired subscriptions in a single batch
            if (expiredSubscriptionIds.Count > 0)
            {
                await _db.PushSubscriptions
                    .Where(s => expiredSubscriptionIds.Contains(s.Id))
                    .ExecuteDeleteAsync(ct);

                _logger.LogInformation(
                    "Deleted {Count} expired push subscriptions for space {SpaceId}",
                    expiredSubscriptionIds.Count, spaceId);
            }
        }
        catch (Exception ex)
        {
            // Never throw — all errors are logged and swallowed
            _logger.LogError(ex,
                "Unexpected error during push notification delivery for space {SpaceId}",
                spaceId);
        }
    }

    private async Task SendToSubscriptionAsync(
        Domain.Notifications.PushSubscription sub,
        string jsonPayload,
        List<Guid> expiredSubscriptionIds)
    {
        try
        {
            var pushSubscription = new WebPush.PushSubscription(
                sub.Endpoint, sub.P256dh, sub.Auth);

            var client = new WebPushClient();
            await client.SendNotificationAsync(pushSubscription, jsonPayload, _vapidDetails);
        }
        catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone)
        {
            // 410 Gone — subscription is expired, mark for deletion
            _logger.LogInformation(
                "Push subscription {Endpoint} returned 410 Gone, marking for deletion",
                sub.Endpoint);
            expiredSubscriptionIds.Add(sub.Id);
        }
        catch (WebPushException ex) when (ex.StatusCode == (HttpStatusCode)429)
        {
            // 429 Rate Limited — log warning, skip
            _logger.LogWarning(
                "Push service rate limited for endpoint {Endpoint}. Skipping delivery",
                sub.Endpoint);
        }
        catch (WebPushException ex)
        {
            // Other push service errors — log and skip
            _logger.LogError(ex,
                "Push delivery failed for endpoint {Endpoint} with status {StatusCode}",
                sub.Endpoint, ex.StatusCode);
        }
        catch (Exception ex)
        {
            // Network errors, timeouts, etc. — log and skip
            _logger.LogError(ex,
                "Unexpected error sending push to endpoint {Endpoint}",
                sub.Endpoint);
        }
    }
}
