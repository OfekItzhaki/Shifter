using Jobuler.Application.Platform.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("platform")]
[Authorize]
public class PlatformController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Hardcoded platform owner user ID.
    /// Only this user can access platform-level endpoints.
    /// </summary>
    private static readonly Guid PlatformOwnerUserId =
        Guid.Parse("a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5");

    public PlatformController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// GET /platform/stats
    /// Returns global platform metrics. Platform owner only.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        if (CurrentUserId != PlatformOwnerUserId)
            return Forbid();

        var result = await _mediator.Send(new GetPlatformStatsQuery(), ct);
        return Ok(result);
    }
}
