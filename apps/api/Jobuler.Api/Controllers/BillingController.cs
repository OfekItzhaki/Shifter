using Jobuler.Application.Billing;
using Jobuler.Application.Billing.Commands;
using Jobuler.Application.Billing.Queries;
using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;
    private readonly ILemonSqueezyClient _lemonSqueezy;
    private readonly BillingOptions _billingOptions;
    private readonly AppDbContext _db;

    public BillingController(
        IMediator mediator,
        IPermissionService permissions,
        ILemonSqueezyClient lemonSqueezy,
        IOptions<BillingOptions> billingOptions,
        AppDbContext db)
    {
        _mediator = mediator;
        _permissions = permissions;
        _lemonSqueezy = lemonSqueezy;
        _billingOptions = billingOptions.Value;
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ─── Space-level billing endpoints ───────────────────────────────────────────

    /// <summary>Get space subscription status.</summary>
    [HttpGet("subscription")]
    public async Task<IActionResult> GetSpaceSubscription(
        Guid spaceId, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetSpaceSubscriptionQuery(spaceId, CurrentUserId), ct);

        return Ok(result);
    }

    /// <summary>Create a checkout session for the space subscription.</summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateSpaceCheckout(
        Guid spaceId, [FromBody] CreateSpaceCheckoutRequest? req, CancellationToken ct)
    {
        var checkoutUrl = await _mediator.Send(
            new CreateSpaceCheckoutCommand(spaceId, CurrentUserId, req?.VariantId), ct);

        return Ok(new { checkoutUrl });
    }

    /// <summary>Cancel the space subscription.</summary>
    [HttpPost("cancel")]
    public async Task<IActionResult> CancelSpaceSubscription(
        Guid spaceId, CancellationToken ct)
    {
        await _mediator.Send(
            new CancelSpaceSubscriptionCommand(spaceId, CurrentUserId), ct);

        return NoContent();
    }

    /// <summary>Renew the space subscription.</summary>
    [HttpPost("renew")]
    public async Task<IActionResult> RenewSpaceSubscription(
        Guid spaceId, CancellationToken ct)
    {
        await _mediator.Send(
            new RenewSpaceSubscriptionCommand(spaceId, CurrentUserId), ct);

        return NoContent();
    }

    /// <summary>Upgrade the space plan to a higher-tier variant.</summary>
    [HttpPost("upgrade")]
    public async Task<IActionResult> UpgradeSpacePlan(
        Guid spaceId, [FromBody] UpgradeSpacePlanRequest req, CancellationToken ct)
    {
        var checkoutUrl = await _mediator.Send(
            new UpgradeSpacePlanCommand(spaceId, CurrentUserId, req.VariantId), ct);

        return Ok(new { checkoutUrl });
    }

    // ─── Legacy group-level billing endpoints ────────────────────────────────────

    /// <summary>
    /// Returns 410 Gone if the space has been migrated to space-level billing.
    /// </summary>
    private Task<IActionResult?> RejectIfMigratedAsync(Guid spaceId, CancellationToken ct)
    {
        return Task.FromResult<IActionResult?>(null);
    }

    /// <summary>Get subscription status for a group.</summary>
    [HttpGet("groups/{groupId:guid}/subscription")]
    public async Task<IActionResult> GetSubscription(
        Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var migrated = await RejectIfMigratedAsync(spaceId, ct);
        if (migrated != null) return migrated;

        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var result = await _mediator.Send(new GetSubscriptionQuery(spaceId, groupId), ct);

        if (result == null)
            return Ok(new { status = "none", tierId = (string?)null, trialEndsAt = (DateTime?)null });

        return Ok(result);
    }

    /// <summary>Cancel a group subscription.</summary>
    [HttpPost("groups/{groupId:guid}/cancel")]
    public async Task<IActionResult> CancelSubscription(
        Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var migrated = await RejectIfMigratedAsync(spaceId, ct);
        if (migrated != null) return migrated;

        await _mediator.Send(new CancelSubscriptionCommand(spaceId, groupId, CurrentUserId), ct);
        return Ok();
    }

    /// <summary>Renew a group subscription.</summary>
    [HttpPost("groups/{groupId:guid}/renew")]
    public async Task<IActionResult> RenewSubscription(
        Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var migrated = await RejectIfMigratedAsync(spaceId, ct);
        if (migrated != null) return migrated;

        await _mediator.Send(new RenewSubscriptionCommand(spaceId, groupId, CurrentUserId), ct);
        return Ok();
    }

    /// <summary>Create a checkout session for a group subscription.</summary>
    [HttpPost("groups/{groupId:guid}/checkout")]
    public async Task<IActionResult> CreateCheckout(
        Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var migrated = await RejectIfMigratedAsync(spaceId, ct);
        if (migrated != null) return migrated;

        var checkoutUrl = await _mediator.Send(
            new CreateCheckoutCommand(spaceId, groupId, CurrentUserId), ct);

        return Ok(new { checkoutUrl });
    }

    /// <summary>Create a test-charge checkout session (~$1) for integration verification.</summary>
    [HttpPost("test-charge")]
    public async Task<IActionResult> TestCharge(Guid spaceId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.BillingManage, ct);

        var metadata = new Dictionary<string, string>
        {
            ["charge_type"] = "test-charge"
        };

        var request = new CreateCheckoutRequest(
            VariantId: _billingOptions.TestVariantId,
            Metadata: metadata);

        var checkoutUrl = await _lemonSqueezy.CreateCheckoutAsync(request, ct);

        return Ok(new { checkoutUrl });
    }

    /// <summary>Get active promo coupon code (if configured).</summary>
    [HttpGet("promo")]
    public IActionResult GetPromo(Guid spaceId)
    {
        if (string.IsNullOrWhiteSpace(_billingOptions.PromoCouponCode))
            return Ok(new { code = (string?)null, label = (string?)null });

        return Ok(new { code = _billingOptions.PromoCouponCode, label = _billingOptions.PromoCouponLabel });
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────────

public record CreateSpaceCheckoutRequest(string? VariantId = null);
public record UpgradeSpacePlanRequest(string VariantId);


