using Jobuler.Application.Billing.Commands;
using Jobuler.Application.Billing.Queries;
using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public BillingController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
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

    /// <summary>Validate a coupon code.</summary>
    [HttpPost("validate-coupon")]
    public async Task<IActionResult> ValidateCoupon(
        Guid spaceId, [FromBody] ValidateCouponRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new { error = "Coupon code is required." });

        var result = await _mediator.Send(new ValidateCouponQuery(req.Code), ct);
        return Ok(new { valid = result.Valid, discountPercent = result.DiscountPercent });
    }
}

/// <summary>Platform admin: manage coupons.</summary>
[ApiController]
[Route("platform/coupons")]
[Authorize]
public class CouponsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CouponsController(IMediator mediator) => _mediator = mediator;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>List all coupons (platform admin only).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListCouponsQuery(CurrentUserId), ct);
        return Ok(result);
    }

    /// <summary>Create a coupon (platform admin only).</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCouponRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateCouponCommand(CurrentUserId, req.Code, req.DiscountPercent, req.MaxUses, req.ValidUntil, req.Description), ct);
        return Ok(result);
    }

    /// <summary>Deactivate a coupon (platform admin only).</summary>
    [HttpDelete("{couponId:guid}")]
    public async Task<IActionResult> Deactivate(Guid couponId, CancellationToken ct)
    {
        await _mediator.Send(new DeactivateCouponCommand(CurrentUserId, couponId), ct);
        return NoContent();
    }
}

public record ValidateCouponRequest(string Code);
public record CreateCouponRequest(string Code, int DiscountPercent, int? MaxUses, DateTime? ValidUntil, string? Description);
