using Jobuler.Application.Common;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jobuler.Application.Billing.Commands;

public record CreateCheckoutCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid UserId) : IRequest<string>;

public class CreateCheckoutCommandHandler : IRequestHandler<CreateCheckoutCommand, string>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly ILemonSqueezyClient _lemonSqueezy;
    private readonly BillingOptions _options;

    public CreateCheckoutCommandHandler(
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

    public async Task<string> Handle(CreateCheckoutCommand req, CancellationToken ct)
    {
        // ── Permission check ─────────────────────────────────────────────────
        await _permissions.RequirePermissionAsync(
            req.UserId, req.SpaceId, Permissions.BillingManage, ct);

        // ── Validate group exists and belongs to space ───────────────────────
        var groupExists = await _db.Groups
            .AnyAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct);

        if (!groupExists)
            throw new KeyNotFoundException("Group not found or does not belong to this space.");

        // ── Check no active/trialing subscription exists ─────────────────────
        var existingSub = await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.GroupId == req.GroupId && s.SpaceId == req.SpaceId, ct);

        if (existingSub is not null && existingSub.Status == SubscriptionStatus.Active)
        {
            throw new InvalidOperationException("Group already has an active subscription.");
        }

        if (existingSub is null)
        {
            existingSub = GroupSubscription.CreateTrial(req.SpaceId, req.GroupId);
            _db.GroupSubscriptions.Add(existingSub);
            await _db.SaveChangesAsync(ct);
        }

        // ── Create checkout session via LemonSqueezy ─────────────────────────
        var metadata = new Dictionary<string, string>
        {
            ["space_id"] = req.SpaceId.ToString(),
            ["group_id"] = req.GroupId.ToString()
        };

        var checkoutRequest = new CreateCheckoutRequest(
            VariantId: _options.DefaultVariantId,
            Metadata: metadata);

        var checkoutUrl = await _lemonSqueezy.CreateCheckoutAsync(checkoutRequest, ct);

        return checkoutUrl;
    }
}
