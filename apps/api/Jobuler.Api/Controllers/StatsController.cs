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

    /// <summary>
    /// GET /spaces/{spaceId}/stats/historical/persons
    /// Returns daily per-person statistics for a date range (for graph rendering).
    /// Requires space.view permission.
    /// </summary>
    [HttpGet("historical/persons")]
    public async Task<IActionResult> GetHistoricalPersonStats(
        Guid spaceId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] Guid? groupId,
        CancellationToken ct = default)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var result = await _mediator.Send(
            new GetHistoricalPersonStatsQuery(spaceId, startDate, endDate, groupId), ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /spaces/{spaceId}/stats/rotation?groupId={id}
    /// Returns task rotation progress per person for an army-template group.
    /// Requires space.view permission.
    /// </summary>
    [HttpGet("rotation")]
    public async Task<IActionResult> GetRotationProgress(
        Guid spaceId, [FromQuery] Guid groupId, CancellationToken ct = default)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var result = await _mediator.Send(new GetTaskRotationQuery(spaceId, groupId), ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /spaces/{spaceId}/stats/cumulative?time_range=7d|14d|30d|90d|period&amp;group_id=...&amp;period_id=...
    /// Returns per-person cumulative statistics from cumulative_records.
    /// Defaults to current active period when no period_id specified.
    /// Requires space.view permission.
    /// </summary>
    [HttpGet("cumulative")]
    public async Task<IActionResult> GetCumulativeStats(
        Guid spaceId,
        [FromQuery(Name = "group_id")] Guid groupId,
        [FromQuery(Name = "time_range")] string timeRange = "period",
        [FromQuery(Name = "period_id")] Guid? periodId = null,
        CancellationToken ct = default)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var result = await _mediator.Send(
            new GetCumulativeStatsQuery(spaceId, groupId, timeRange, periodId), ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /spaces/{spaceId}/stats/timeseries?start_date=...&amp;end_date=...&amp;group_id=...&amp;period_id=...
    /// Returns daily data points from daily_snapshots (date, assignment count, burden breakdown).
    /// Requires space.view permission.
    /// </summary>
    [HttpGet("timeseries")]
    public async Task<IActionResult> GetStatsTimeseries(
        Guid spaceId,
        [FromQuery(Name = "group_id")] Guid groupId,
        [FromQuery(Name = "start_date")] DateOnly startDate,
        [FromQuery(Name = "end_date")] DateOnly endDate,
        [FromQuery(Name = "period_id")] Guid? periodId = null,
        CancellationToken ct = default)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var result = await _mediator.Send(
            new GetStatsTimeseriesQuery(spaceId, groupId, startDate, endDate, periodId), ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /spaces/{spaceId}/schedule/history?group_id=...&amp;start_date=...&amp;end_date=...
    /// Returns historical assignments from daily_snapshots for a date range.
    /// Respects schedule_history_retention_days setting.
    /// Requires space.view permission.
    /// </summary>
    [HttpGet("/spaces/{spaceId:guid}/schedule/history")]
    public async Task<IActionResult> GetHistoricalSchedule(
        Guid spaceId,
        [FromQuery(Name = "group_id")] Guid groupId,
        [FromQuery(Name = "start_date")] DateOnly startDate,
        [FromQuery(Name = "end_date")] DateOnly endDate,
        CancellationToken ct = default)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var result = await _mediator.Send(
            new GetHistoricalScheduleQuery(spaceId, groupId, startDate, endDate), ct);
        return Ok(result);
    }
}
