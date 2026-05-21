using Jobuler.Application.Billing;
using Jobuler.Application.Billing.Commands;
using Jobuler.Application.Billing.Queries;
using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public BillingController(
        IMediator mediator,
        IPermissionService permissions,
        ILemonSqueezyClient lemonSqueezy,
        IOptions<BillingOptions> billingOptions)
    {
        _mediator = mediator;
        _permissions = permissions;
        _lemonSqueezy = lemonSqueezy;
        _billingOptions = billingOptions.Value;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Get subscription status for a group.</summary>
    [HttpGet("groups/{groupId:guid}/subscription")]
    public async Task<IActionResult> GetSubscription(
        Guid spaceId, Guid groupId, CancellationToken ct)
    {
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
        await _mediator.Send(new CancelSubscriptionCommand(spaceId, groupId, CurrentUserId), ct);
        return Ok();
    }

    /// <summary>Renew a group subscription.</summary>
    [HttpPost("groups/{groupId:guid}/renew")]
    public async Task<IActionResult> RenewSubscription(
        Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _mediator.Send(new RenewSubscriptionCommand(spaceId, groupId, CurrentUserId), ct);
        return Ok();
    }

    /// <summary>Create a checkout session for a group subscription.</summary>
    [HttpPost("groups/{groupId:guid}/checkout")]
    public async Task<IActionResult> CreateCheckout(
        Guid spaceId, Guid groupId, CancellationToken ct)
    {
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


