using Jobuler.Application.HomeLeave.Commands;
using Jobuler.Application.HomeLeave.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/home-leave-config")]
[Authorize]
public class HomeLeaveConfigController : ControllerBase
{
    private readonly IMediator _mediator;

    public HomeLeaveConfigController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Get home-leave configuration for a group (returns defaults if none saved).</summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetHomeLeaveConfigQuery(spaceId, groupId), ct);
        return Ok(result);
    }

    /// <summary>Create or update home-leave configuration for a group.</summary>
    [HttpPut]
    public async Task<IActionResult> Upsert(
        Guid spaceId, Guid groupId,
        [FromBody] UpsertHomeLeaveConfigRequest req,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new UpsertHomeLeaveConfigCommand(
            spaceId,
            groupId,
            req.MinRestHours,
            req.EligibilityThresholdHours,
            req.LeaveCapacity,
            req.LeaveDurationHours,
            CurrentUserId), ct);

        return Ok(result);
    }

    /// <summary>
    /// Cancel a home-leave presence window for a person.
    /// If starts_at is in the future: deletes the window entirely.
    /// If starts_at is in the past and ends_at is in the future: truncates to current timestamp.
    /// Requires schedule.publish permission.
    /// </summary>
    [HttpDelete("~/spaces/{spaceId:guid}/home-leave-presence/{presenceWindowId:guid}")]
    public async Task<IActionResult> CancelHomeLeave(
        Guid spaceId,
        Guid presenceWindowId,
        [FromQuery] Guid personId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelHomeLeaveCommand(
            spaceId,
            personId,
            presenceWindowId,
            CurrentUserId), ct);

        return Ok(result);
    }
}

public record UpsertHomeLeaveConfigRequest(
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours);
