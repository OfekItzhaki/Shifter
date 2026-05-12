using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/stats")]
[Authorize]
public class StatsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public StatsController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// GET /spaces/{spaceId}/stats/burden
    /// Returns burden and fairness statistics for all people in the space.
    /// Requires space.view permission.
    /// </summary>
    [HttpGet("burden")]
    public async Task<IActionResult> GetBurdenStats(Guid spaceId, [FromQuery] Guid? groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var result = await _mediator.Send(new GetBurdenStatsQuery(spaceId, groupId), ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /spaces/{spaceId}/stats/historical
    /// Returns time-series statistics (assignments per day, solver runs per day, burden trend).
    /// Requires space.view permission.
    /// </summary>
    [HttpGet("historical")]
    public async Task<IActionResult> GetHistoricalStats(
        Guid spaceId, [FromQuery] int days = 30, CancellationToken ct = default)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var result = await _mediator.Send(new GetHistoricalStatsQuery(spaceId, days), ct);
        return Ok(result);
    }
}
