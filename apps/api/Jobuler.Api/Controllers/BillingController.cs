using Jobuler.Application.Common;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public BillingController(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
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

        var sub = await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.GroupId == groupId && s.SpaceId == spaceId, ct);

        if (sub == null)
            return Ok(new { status = "none", tierId = (string?)null, trialEndsAt = (DateTime?)null });

        return Ok(new
        {
            status = sub.Status.ToString().ToLower(),
            tierId = sub.TierId,
            trialEndsAt = sub.TrialEndsAt,
            peakMemberCount = sub.PeakMemberCount,
            discountPercent = sub.DiscountPercent,
            couponCode = sub.CouponCode,
            isActive = sub.IsActive,
        });
    }

    /// <summary>Validate a coupon code.</summary>
    [HttpPost("validate-coupon")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateCoupon(
        Guid spaceId, [FromBody] ValidateCouponRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new { error = "Coupon code is required." });

        var coupon = await _db.Coupons
            .FirstOrDefaultAsync(c => c.Code == req.Code.ToUpperInvariant().Trim(), ct);

        if (coupon == null || !coupon.IsValid)
            return Ok(new { valid = false, discountPercent = 0 });

        return Ok(new { valid = true, discountPercent = coupon.DiscountPercent });
    }
}

/// <summary>Platform admin: manage coupons.</summary>
[ApiController]
[Route("platform/coupons")]
[Authorize]
public class CouponsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CouponsController(AppDbContext db) => _db = db;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>List all coupons (platform admin only).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { CurrentUserId }, ct);
        if (user == null || !user.IsPlatformAdmin)
            return Forbid();

        var coupons = await _db.Coupons.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        return Ok(coupons.Select(c => new
        {
            c.Id, c.Code, c.DiscountPercent, c.MaxUses, c.CurrentUses,
            c.ValidFrom, c.ValidUntil, c.IsActive, c.Description
        }));
    }

    /// <summary>Create a coupon (platform admin only).</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCouponRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { CurrentUserId }, ct);
        if (user == null || !user.IsPlatformAdmin)
            return Forbid();

        if (string.IsNullOrWhiteSpace(req.Code) || req.DiscountPercent < 1 || req.DiscountPercent > 100)
            return BadRequest(new { error = "Invalid coupon data." });

        var exists = await _db.Coupons.AnyAsync(c => c.Code == req.Code.ToUpperInvariant().Trim(), ct);
        if (exists)
            return BadRequest(new { error = "Coupon code already exists." });

        var coupon = Coupon.Create(req.Code, req.DiscountPercent, req.MaxUses, req.ValidUntil, req.Description);
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync(ct);

        return Ok(new { coupon.Id, coupon.Code, coupon.DiscountPercent });
    }

    /// <summary>Deactivate a coupon (platform admin only).</summary>
    [HttpDelete("{couponId:guid}")]
    public async Task<IActionResult> Deactivate(Guid couponId, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { CurrentUserId }, ct);
        if (user == null || !user.IsPlatformAdmin)
            return Forbid();

        var coupon = await _db.Coupons.FindAsync(new object[] { couponId }, ct);
        if (coupon == null) return NotFound();

        coupon.Deactivate();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record ValidateCouponRequest(string Code);
public record CreateCouponRequest(string Code, int DiscountPercent, int? MaxUses, DateTime? ValidUntil, string? Description);
