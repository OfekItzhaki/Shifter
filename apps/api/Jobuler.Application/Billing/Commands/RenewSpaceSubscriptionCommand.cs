using Jobuler.Application.Common;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Commands;

public record RenewSpaceSubscriptionCommand(
    Guid SpaceId,
    Guid UserId) : IRequest;

public class RenewSpaceSubscriptionCommandHandler : IRequestHandler<RenewSpaceSubscriptionCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IStatisticsPeriodService _statisticsPeriodService;

    public RenewSpaceSubscriptionCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IStatisticsPeriodService statisticsPeriodService)
    {
        _db = db;
        _permissions = permissions;
        _statisticsPeriodService = statisticsPeriodService;
    }

    public async Task Handle(RenewSpaceSubscriptionCommand req, CancellationToken ct)
    {
        // ── Permission check ─────────────────────────────────────────────────
        await _permissions.RequirePermissionAsync(
            req.UserId, req.SpaceId, Permissions.BillingManage, ct);

        // ── Load space subscription ──────────────────────────────────────────
        var sub = await _db.SpaceSubscriptions
            .FirstOrDefaultAsync(s => s.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Space subscription not found.");

        // ── Determine renewal path ───────────────────────────────────────────
        var now = DateTime.UtcNow;
        var isWithinGracePeriod = sub.Status == SubscriptionStatus.Canceled
            && sub.CurrentPeriodEnd.HasValue
            && sub.CurrentPeriodEnd.Value > now;

        if (isWithinGracePeriod)
        {
            // Within grace period — preserve existing period dates
            sub.RenewWithinGracePeriod();
        }
        else
        {
            // Expired or canceled past period end — create new billing period
            var newPeriodStart = now;
            var newPeriodEnd = now.AddMonths(1);
            sub.RenewAfterExpiry(newPeriodStart, newPeriodEnd);

            // Trigger statistics period rotation for expired renewals
            await _statisticsPeriodService.OnPeriodRenewedAsync(req.SpaceId, newPeriodStart, ct);
        }

        await _db.SaveChangesAsync(ct);
    }
}
