using Jobuler.Application.Notifications;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Infrastructure.Notifications;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db) => _db = db;

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
    }
}
