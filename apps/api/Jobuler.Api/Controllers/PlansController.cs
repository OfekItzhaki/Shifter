using Jobuler.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jobuler.Api.Controllers;

/// <summary>
/// Public endpoint for fetching available subscription plans.
/// No authentication required — the pricing page is public.
/// </summary>
[ApiController]
[Route("billing")]
public class PlansController : ControllerBase
{
    private readonly ILemonSqueezyClient _lemonSqueezy;

    public PlansController(ILemonSqueezyClient lemonSqueezy)
    {
        _lemonSqueezy = lemonSqueezy;
    }

    /// <summary>Get all available subscription plans (public).</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var plans = await _lemonSqueezy.GetPlansAsync(ct);
        return Ok(plans);
    }
}
