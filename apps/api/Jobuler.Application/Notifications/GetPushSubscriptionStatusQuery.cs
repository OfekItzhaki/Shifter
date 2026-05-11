using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Notifications;

public record PushSubscriptionStatusResponse(bool IsSubscribed);

public record GetPushSubscriptionStatusQuery(Guid SpaceId, Guid UserId)
    : IRequest<PushSubscriptionStatusResponse>;

public class GetPushSubscriptionStatusQueryHandler
    : IRequestHandler<GetPushSubscriptionStatusQuery, PushSubscriptionStatusResponse>
{
    private readonly AppDbContext _db;
    public GetPushSubscriptionStatusQueryHandler(AppDbContext db) => _db = db;

    public async Task<PushSubscriptionStatusResponse> Handle(
        GetPushSubscriptionStatusQuery req, CancellationToken ct)
    {
        var exists = await _db.PushSubscriptions.AsNoTracking()
            .AnyAsync(s => s.UserId == req.UserId && s.SpaceId == req.SpaceId, ct);

        return new PushSubscriptionStatusResponse(IsSubscribed: exists);
    }
}
