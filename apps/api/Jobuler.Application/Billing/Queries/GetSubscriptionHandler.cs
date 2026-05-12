using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Queries;

public class GetSubscriptionHandler : IRequestHandler<GetSubscriptionQuery, SubscriptionDto?>
{
    private readonly AppDbContext _db;
    public GetSubscriptionHandler(AppDbContext db) => _db = db;

    public async Task<SubscriptionDto?> Handle(GetSubscriptionQuery request, CancellationToken ct)
    {
        var sub = await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.GroupId == request.GroupId && s.SpaceId == request.SpaceId, ct);

        if (sub == null) return null;

        return new SubscriptionDto(
            sub.Status.ToString().ToLower(),
            sub.TierId,
            sub.TrialEndsAt,
            sub.PeakMemberCount,
            sub.DiscountPercent,
            sub.CouponCode,
            sub.IsActive
        );
    }
}
