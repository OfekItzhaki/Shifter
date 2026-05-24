using Jobuler.Application.Common;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Commands;

public record CancelSpaceSubscriptionCommand(
    Guid SpaceId,
    Guid UserId) : IRequest;

public class CancelSpaceSubscriptionCommandHandler : IRequestHandler<CancelSpaceSubscriptionCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IStatisticsPeriodService _statisticsPeriods;

    public CancelSpaceSubscriptionCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IStatisticsPeriodService statisticsPeriods)
    {
        _db = db;
        _permissions = permissions;
        _statisticsPeriods = statisticsPeriods;
    }

    public async Task Handle(CancelSpaceSubscriptionCommand req, CancellationToken ct)
    {
        // ── Permission check ─────────────────────────────────────────────────
        await _permissions.RequirePermissionAsync(
            req.UserId, req.SpaceId, Permissions.BillingManage, ct);

        // ── Load space subscription ──────────────────────────────────────────
        var sub = await _db.SpaceSubscriptions
            .FirstOrDefaultAsync(s => s.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Subscription not found for space.");

        // ── Check if currently trialing (before calling Cancel) ──────────────
        var wasTrialing = sub.Status == SubscriptionStatus.Trialing;

        // ── Cancel (throws InvalidOperationException if already canceled/expired)
        sub.Cancel();

        // ── If was trialing, trigger statistics period closure ────────────────
        if (wasTrialing)
        {
            await _statisticsPeriods.OnTrialExpiredAsync(req.SpaceId, DateTime.UtcNow, ct);
        }

        // ── Save changes ─────────────────────────────────────────────────────
        await _db.SaveChangesAsync(ct);
    }
}
