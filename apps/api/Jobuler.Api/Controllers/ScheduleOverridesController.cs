using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/schedule/overrides")]
[Authorize]
public class ScheduleOverridesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public ScheduleOverridesController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Apply a manual override to a specific slot.
    /// Creates a draft version (cloned from published) if one doesn't exist.
    /// Returns the draft version ID.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Apply(
        Guid spaceId,
        [FromBody] ApplyOverrideRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SchedulePublish, ct);

        var draftVersionId = await _mediator.Send(
            new ApplyManualOverrideCommand(spaceId, req.SlotId, req.NewPersonIds, CurrentUserId), ct);

        return Ok(new { draftVersionId });
    }

    /// <summary>
    /// Remove all assignments for a slot (mark it explicitly unassigned in the draft).
    /// Creates a draft version if one doesn't exist.
    /// Returns the draft version ID.
    /// </summary>
    [HttpDelete("{slotId:guid}")]
    public async Task<IActionResult> Remove(
        Guid spaceId,
        Guid slotId,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SchedulePublish, ct);

        var draftVersionId = await _mediator.Send(
            new RemoveManualOverrideCommand(spaceId, slotId, CurrentUserId), ct);

        return Ok(new { draftVersionId });
    }
}

public record ApplyOverrideRequest(Guid SlotId, List<Guid> NewPersonIds);
