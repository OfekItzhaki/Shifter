using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Queries;

public class GetSpaceSubscriptionHandler : IRequestHandler<GetSpaceSubscriptionQuery, SpaceSubscriptionDto?>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public GetSpaceSubscriptionHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<SpaceSubscriptionDto?> Handle(GetSpaceSubscriptionQuery request, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            request.UserId, request.SpaceId, Permissions.SpaceView, ct);

        var subscription = await _db.SpaceSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SpaceId == request.SpaceId, ct);

        if (subscription is null)
            return null;

        return new SpaceSubscriptionDto(
            Status: subscription.Status.ToString().ToLowerInvariant(),
            TierId: subscription.TierId,
            TrialStartsAt: subscription.TrialStartsAt,
            TrialEndsAt: subscription.TrialEndsAt,
            CurrentPeriodStart: subscription.CurrentPeriodStart,
            CurrentPeriodEnd: subscription.CurrentPeriodEnd,
            CanceledAt: subscription.CanceledAt,
            AutoRenew: subscription.AutoRenew,
            IsActive: subscription.IsAccessGranted,
            DaysRemaining: subscription.DaysRemaining);
    }
}
