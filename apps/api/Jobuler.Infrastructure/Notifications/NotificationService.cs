using Jobuler.Application.Notifications;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Notifications;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IPushNotificationSender _pushSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext db,
        IPushNotificationSender pushSender,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _pushSender = pushSender;
        _logger = logger;
    }

    public async Task NotifySpaceAdminsAsync(
        Guid spaceId, string eventType, string title, string body,
        string? metadataJson = null, CancellationToken ct = default)
    {
        // Notify all members who have at least SpaceView — i.e. everyone in the space
        var memberIds = await _db.SpaceMemberships.AsNoTracking()
            .Where(m => m.SpaceId == spaceId)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        var notifications = memberIds.Select(userId =>
            Notification.Create(spaceId, userId, eventType, title, body, metadataJson))
            .ToList();

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync(ct);

        // Deliver push notifications — failures must never affect in-app persistence
        try
        {
            var payload = new PushPayload(
                Title: title,
                Body: body,
                Icon: "/favicon.jpeg",
                Url: "/notifications");

            await _pushSender.SendPushToUsersAsync(memberIds, spaceId, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Push notification delivery failed for space {SpaceId}, event {EventType}. In-app notifications were persisted successfully.",
                spaceId, eventType);
        }
    }
}
