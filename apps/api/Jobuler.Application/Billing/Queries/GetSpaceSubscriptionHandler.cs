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
        {
            // No subscription record — derive trial info from space creation date
            var space = await _db.Spaces
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SpaceId, ct);

            if (space is null)
                return null;

            var trialStart = space.CreatedAt;
            var trialEnd = trialStart.AddDays(14);
            var daysRemaining = Math.Max(0, (int)Math.Ceiling((trialEnd - DateTime.UtcNow).TotalDays));
            var isExpired = DateTime.UtcNow > trialEnd;

            return new SpaceSubscriptionDto(
                Status: isExpired ? "expired" : "trialing",
                TierId: null,
                TrialStartsAt: trialStart,
                TrialEndsAt: trialEnd,
                CurrentPeriodStart: null,
                CurrentPeriodEnd: null,
                CanceledAt: null,
                AutoRenew: false,
                IsActive: !isExpired,
                DaysRemaining: isExpired ? 0 : daysRemaining);
        }

        // Determine the effective status for the frontend
        // If trialing but trial has expired, report as "expired" (not "trialing")
        var effectiveStatus = subscription.IsTrialExpired
            ? "expired"
            : subscription.Status.ToString().ToLowerInvariant();

        return new SpaceSubscriptionDto(
            Status: effectiveStatus,
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
