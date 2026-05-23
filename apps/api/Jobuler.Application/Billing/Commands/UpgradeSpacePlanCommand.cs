using Jobuler.Application.Common;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Commands;

public record UpgradeSpacePlanCommand(
    Guid SpaceId,
    Guid UserId,
    string VariantId) : IRequest<string>;

public class UpgradeSpacePlanCommandHandler : IRequestHandler<UpgradeSpacePlanCommand, string>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly ILemonSqueezyClient _lemonSqueezy;

    public UpgradeSpacePlanCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        ILemonSqueezyClient lemonSqueezy)
    {
        _db = db;
        _permissions = permissions;
        _lemonSqueezy = lemonSqueezy;
    }

    public async Task<string> Handle(UpgradeSpacePlanCommand req, CancellationToken ct)
    {
        // ── Permission check ─────────────────────────────────────────────────
        await _permissions.RequirePermissionAsync(
            req.UserId, req.SpaceId, Permissions.BillingManage, ct);

        // ── Load space subscription ──────────────────────────────────────────
        var subscription = await _db.SpaceSubscriptions
            .FirstOrDefaultAsync(s => s.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Space subscription not found.");

        // ── Reject if status is not Active or Trialing ───────────────────────
        if (subscription.Status != SubscriptionStatus.Active &&
            subscription.Status != SubscriptionStatus.Trialing)
        {
            throw new InvalidOperationException(
                "Subscription must be active or trialing to upgrade the plan.");
        }

        // ── Create checkout session via LemonSqueezy ─────────────────────────
        var metadata = new Dictionary<string, string>
        {
            ["space_id"] = req.SpaceId.ToString()
        };

        var checkoutRequest = new CreateCheckoutRequest(
            VariantId: req.VariantId,
            Metadata: metadata);

        var checkoutUrl = await _lemonSqueezy.CreateCheckoutAsync(checkoutRequest, ct);

        return checkoutUrl;
    }
}
