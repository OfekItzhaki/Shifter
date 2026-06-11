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
        string? metadataJson = null, Guid? groupId = null, CancellationToken ct = default)
    {
        var memberIds = await GetSpaceAdminUserIdsAsync(spaceId, groupId, ct);

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

    public async Task NotifySpaceAdminsOnceAsync(
        Guid spaceId, string eventType, string title, string body,
        string? metadataJson, string deduplicationHash, Guid? groupId = null, CancellationToken ct = default)
    {
        var alreadySent = await _db.Notifications
            .AsNoTracking()
            .AnyAsync(n => n.SpaceId == spaceId
                           && n.EventType == eventType
                           && n.DeduplicationHash == deduplicationHash, ct);

        if (alreadySent)
        {
            _logger.LogDebug(
                "Skipping duplicate admin notification for space {SpaceId}, event {EventType}, dedup {DeduplicationHash}.",
                spaceId, eventType, deduplicationHash);
            return;
        }

        var memberIds = await GetSpaceAdminUserIdsAsync(spaceId, groupId, ct);

        var notifications = memberIds.Select(userId =>
            Notification.CreateWithDedup(spaceId, userId, eventType, title, body, metadataJson, deduplicationHash))
            .ToList();

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync(ct);

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

    private async Task<List<Guid>> GetSpaceAdminUserIdsAsync(Guid spaceId, Guid? groupId, CancellationToken ct)
    {
        // Notify only admins: space owner + group owners
        var spaceOwnerIds = await _db.Spaces.AsNoTracking()
            .Where(s => s.Id == spaceId)
            .Select(s => s.OwnerUserId)
            .ToListAsync(ct);

        var groupOwnerQuery = _db.GroupMemberships.AsNoTracking()
            .Where(gm => gm.SpaceId == spaceId && gm.IsOwner);

        if (groupId.HasValue)
            groupOwnerQuery = groupOwnerQuery.Where(gm => gm.GroupId == groupId.Value);

        var groupOwnerPersonIds = await groupOwnerQuery
            .Select(gm => gm.PersonId)
            .Distinct()
            .ToListAsync(ct);

        var groupOwnerUserIds = await _db.People.AsNoTracking()
            .Where(p => groupOwnerPersonIds.Contains(p.Id) && p.LinkedUserId != null)
            .Select(p => p.LinkedUserId!.Value)
            .ToListAsync(ct);

        return spaceOwnerIds
            .Concat(groupOwnerUserIds)
            .Distinct()
            .ToList();
    }
}
