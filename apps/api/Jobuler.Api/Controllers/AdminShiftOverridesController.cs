using Jobuler.Application.Scheduling.SelfService.Commands;
using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

/// <summary>
/// Admin endpoints for manually assigning or removing members from self-service shift slots.
/// Requires SchedulePublish permission.
/// </summary>
[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/shift-slots/{shiftSlotId:guid}/admin-overrides")]
[Authorize]
public class AdminShiftOverridesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public AdminShiftOverridesController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Admin assigns a member to a shift slot, bypassing capacity and Max_Shifts constraints.
    /// Creates an approved ShiftRequest with admin_override flag.
    /// </summary>
    [HttpPost("assign")]
    public async Task<IActionResult> AssignMember(
        Guid spaceId,
        Guid groupId,
        Guid shiftSlotId,
        [FromBody] AdminAssignRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SchedulePublish, ct);

        var result = await _mediator.Send(
            new AdminAssignShiftCommand(spaceId, groupId, shiftSlotId, req.PersonId, CurrentUserId), ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { shiftRequestId = result.ShiftRequestId });
    }

    /// <summary>
    /// Admin removes a member from a shift slot.
    /// Cancels the existing ShiftRequest with reason "admin_removed" and triggers waitlist processing.
    /// </summary>
    [HttpPost("remove")]
    public async Task<IActionResult> RemoveMember(
        Guid spaceId,
        Guid groupId,
        Guid shiftSlotId,
        [FromBody] AdminRemoveRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SchedulePublish, ct);

        var result = await _mediator.Send(
            new AdminRemoveShiftCommand(spaceId, groupId, shiftSlotId, req.PersonId, CurrentUserId), ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Member removed from shift slot." });
    }
}

// ── Request records ───────────────────────────────────────────────────────────

public record AdminAssignRequest(Guid PersonId);
public record AdminRemoveRequest(Guid PersonId);
