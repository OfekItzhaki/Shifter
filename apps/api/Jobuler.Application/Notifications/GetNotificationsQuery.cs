using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Notifications;

public record NotificationDto(
    Guid Id, string EventType, string Title, string Body,
    bool IsRead, DateTime CreatedAt, string? MetadataJson);

public record GetNotificationsQuery(
    Guid SpaceId, Guid UserId, bool UnreadOnly = false)
    : IRequest<List<NotificationDto>>;

public class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, List<NotificationDto>>
{
    private readonly AppDbContext _db;
    public GetNotificationsQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<NotificationDto>> Handle(
        GetNotificationsQuery req, CancellationToken ct)
    {
        var query = _db.Notifications.AsNoTracking()
            .Where(n => n.SpaceId == req.SpaceId && n.UserId == req.UserId);

        if (req.UnreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto(
                n.Id, n.EventType, n.Title, n.Body,
                n.IsRead, n.CreatedAt, n.MetadataJson))
            .ToListAsync(ct);
    }
}
