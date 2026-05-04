using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Application.Scheduling.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/schedule-runs")]
[Authorize]
public class ScheduleRunsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public ScheduleRunsController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Trigger a solver run. Returns the RunId immediately — solve happens asynchronously.
    /// Poll GET /schedule-runs/{runId} to check status.
    /// When groupId is provided, the solver only schedules that group's members and tasks.
    /// </summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> Trigger(
        Guid spaceId, [FromBody] TriggerSolverRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.ScheduleRecalculate, ct);

        var runId = await _mediator.Send(
            new TriggerSolverCommand(spaceId, req.TriggerMode ?? "standard", CurrentUserId, req.GroupId, req.StartTime), ct);

        return Accepted(new { runId });
    }

    /// <summary>Poll the status of a solver run.</summary>
    [HttpGet("{runId:guid}")]
    public async Task<IActionResult> GetRun(Guid spaceId, Guid runId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceAdminMode, ct);
        var result = await _mediator.Send(new GetScheduleRunQuery(spaceId, runId), ct);
        return result is null ? NotFound() : Ok(result);
    }
}

public record TriggerSolverRequest(string? TriggerMode, Guid? GroupId = null, DateTime? StartTime = null);
