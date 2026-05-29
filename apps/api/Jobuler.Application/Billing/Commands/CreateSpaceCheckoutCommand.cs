using Jobuler.Application.Common;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jobuler.Application.Billing.Commands;

public record CreateSpaceCheckoutCommand(
    Guid SpaceId,
    Guid UserId,
    string? VariantId = null) : IRequest<string>;

public class CreateSpaceCheckoutCommandHandler : IRequestHandler<CreateSpaceCheckoutCommand, string>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly ILemonSqueezyClient _lemonSqueezy;
    private readonly BillingOptions _options;

    public CreateSpaceCheckoutCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        ILemonSqueezyClient lemonSqueezy,
        IOptions<BillingOptions> options)
    {
        _db = db;
        _permissions = permissions;
        _lemonSqueezy = lemonSqueezy;
        _options = options.Value;
    }

    public async Task<string> Handle(CreateSpaceCheckoutCommand req, CancellationToken ct)
    {
        // ── Permission check ─────────────────────────────────────────────────
        await _permissions.RequirePermissionAsync(
            req.UserId, req.SpaceId, Permissions.BillingManage, ct);

        // ── Load space subscription ──────────────────────────────────────────
        var subscription = await _db.SpaceSubscriptions
            .FirstOrDefaultAsync(s => s.SpaceId == req.SpaceId, ct);

        // ── Reject if already active ─────────────────────────────────────────
        if (subscription is not null && subscription.Status == SubscriptionStatus.Active)
        {
            throw new InvalidOperationException(
                "Space already has an active subscription. Cannot create a new checkout.");
        }

        // ── Create checkout session via LemonSqueezy ─────────────────────────
        var variantId = req.VariantId ?? _options.DefaultVariantId;

        var metadata = new Dictionary<string, string>
        {
            ["space_id"] = req.SpaceId.ToString()
        };

        // Redirect back to space settings after successful payment
        var frontendBaseUrl = "https://shifter.ofeklabs.com";
        var redirectUrl = $"{frontendBaseUrl}/spaces/settings";

        var checkoutRequest = new CreateCheckoutRequest(
            VariantId: variantId,
            Metadata: metadata,
            RedirectUrl: redirectUrl);

        var checkoutUrl = await _lemonSqueezy.CreateCheckoutAsync(checkoutRequest, ct);

        return checkoutUrl;
    }
}
