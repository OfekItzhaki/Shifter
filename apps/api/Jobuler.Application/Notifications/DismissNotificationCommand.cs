using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Notifications;

public record DismissNotificationCommand(
    Guid SpaceId, Guid UserId, Guid NotificationId) : IRequest;

public class DismissNotificationCommandHandler
    : IRequestHandler<DismissNotificationCommand>
{
    private readonly AppDbContext _db;
    public DismissNotificationCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DismissNotificationCommand req, CancellationToken ct)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(
            n => n.Id == req.NotificationId
              && n.SpaceId == req.SpaceId
              && n.UserId == req.UserId, ct);

        if (n is null) return; // idempotent
        n.MarkRead();
        await _db.SaveChangesAsync(ct);
    }
}

public record DismissAllNotificationsCommand(
    Guid SpaceId, Guid UserId) : IRequest;

public class DismissAllNotificationsCommandHandler
    : IRequestHandler<DismissAllNotificationsCommand>
{
    private readonly AppDbContext _db;
    public DismissAllNotificationsCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DismissAllNotificationsCommand req, CancellationToken ct)
    {
        var unread = await _db.Notifications
            .Where(n => n.SpaceId == req.SpaceId
                     && n.UserId == req.UserId
                     && !n.IsRead)
            .ToListAsync(ct);

        foreach (var n in unread) n.MarkRead();
        await _db.SaveChangesAsync(ct);
    }
}
