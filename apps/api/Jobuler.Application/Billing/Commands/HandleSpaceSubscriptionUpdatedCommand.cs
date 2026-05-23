using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Billing.Commands;

/// <summary>
/// Handles the subscription_updated webhook for space-level subscriptions.
/// Updates period dates, tier, and auto-renew flag. Triggers statistics period rotation on period change.
/// No user permission check — this is a webhook handler.
/// </summary>
public record HandleSpaceSubscriptionUpdatedCommand(
    Guid SpaceId,
    string VariantId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    bool AutoRenew) : IRequest;

public class HandleSpaceSubscriptionUpdatedCommandHandler : IRequestHandler<HandleSpaceSubscriptionUpdatedCommand>
{
    private readonly AppDbContext _db;
    private readonly IStatisticsPeriodService _statisticsPeriods;
    private readonly ILogger<HandleSpaceSubscriptionUpdatedCommandHandler> _logger;

    public HandleSpaceSubscriptionUpdatedCommandHandler(
        AppDbContext db,
        IStatisticsPeriodService statisticsPeriods,
        ILogger<HandleSpaceSubscriptionUpdatedCommandHandler> logger)
    {
        _db = db;
        _statisticsPeriods = statisticsPeriods;
        _logger = logger;
    }

    public async Task Handle(HandleSpaceSubscriptionUpdatedCommand req, CancellationToken ct)
    {
        // ── Load space subscription ──────────────────────────────────────────
        var sub = await _db.SpaceSubscriptions
            .FirstOrDefaultAsync(s => s.SpaceId == req.SpaceId, ct);

        if (sub is null)
        {
            _logger.LogWarning(
                "No SpaceSubscription found for SpaceId={SpaceId}. Skipping subscription_updated",
                req.SpaceId);
            return;
        }

        // ── Detect period change ─────────────────────────────────────────────
        var periodChanged = sub.CurrentPeriodStart != req.PeriodStart
                            || sub.CurrentPeriodEnd != req.PeriodEnd;

        // ── Update period dates ──────────────────────────────────────────────
        sub.UpdatePeriod(req.PeriodStart, req.PeriodEnd);

        // ── Update tier if variant changed ───────────────────────────────────
        if (!string.Equals(req.VariantId, sub.TierId, StringComparison.Ordinal))
        {
            sub.UpdateTier(req.VariantId);
            _logger.LogInformation(
                "SpaceSubscription for SpaceId={SpaceId} tier updated to {NewTierId}",
                req.SpaceId, req.VariantId);
        }

        // ── Sync auto-renew flag ─────────────────────────────────────────────
        sub.SetAutoRenew(req.AutoRenew);

        // ── If period changed, reset peak and trigger statistics rotation ────
        if (periodChanged)
        {
            sub.ResetPeakForNewPeriod();
            await _statisticsPeriods.OnPeriodRenewedAsync(req.SpaceId, req.PeriodStart, ct);

            _logger.LogInformation(
                "SpaceSubscription for SpaceId={SpaceId} period renewed: {PeriodStart} → {PeriodEnd}",
                req.SpaceId, req.PeriodStart, req.PeriodEnd);
        }

        // ── Save changes ─────────────────────────────────────────────────────
        await _db.SaveChangesAsync(ct);
    }
}
