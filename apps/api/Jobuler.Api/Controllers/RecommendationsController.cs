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
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public RecommendationsController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ─── GET endpoints ───────────────────────────────────────────────────────────

    /// <summary>
    /// Get active recommendations for a group.
    /// </summary>
    [HttpGet("spaces/{spaceId:guid}/groups/{groupId:guid}/recommendations")]
    public async Task<IActionResult> GetActiveForGroup(
        Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetActiveRecommendationsQuery(spaceId, groupId, CurrentUserId), ct);

        return Ok(result);
    }

    /// <summary>
    /// Get recommendations banner data for a specific solver run.
    /// </summary>
    [HttpGet("spaces/{spaceId:guid}/runs/{runId:guid}/recommendations")]
    public async Task<IActionResult> GetForRun(
        Guid spaceId, Guid runId, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetRecommendationsForRunQuery(spaceId, runId, CurrentUserId), ct);

        return result is null ? Ok(new { }) : Ok(result);
    }

    /// <summary>
    /// Get active recommendation for a specific task (inline suggestion).
    /// </summary>
    [HttpGet("spaces/{spaceId:guid}/tasks/{taskId:guid}/recommendation")]
    public async Task<IActionResult> GetForTask(
        Guid spaceId, Guid taskId, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetRecommendationForTaskQuery(spaceId, taskId, CurrentUserId), ct);

        return result is null ? NoContent() : Ok(result);
    }

    // ─── POST action endpoints ───────────────────────────────────────────────────

    /// <summary>
    /// Dismiss a recommendation. Marks it as dismissed so it no longer appears.
    /// </summary>
    [HttpPost("spaces/{spaceId:guid}/recommendations/{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(
        Guid spaceId, Guid id, CancellationToken ct)
    {
        await _mediator.Send(
            new DismissRecommendationCommand(spaceId, id, CurrentUserId), ct);

        return NoContent();
    }

}
