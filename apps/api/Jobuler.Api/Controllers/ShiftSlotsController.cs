using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.SelfService.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/shift-slots")]
[Authorize]
public class ShiftSlotsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public ShiftSlotsController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Get available shift slots for the current member in a scheduling cycle.
    /// Returns slots with remaining capacity, excluding already-claimed and overlapping slots.
    /// Includes a read-only flag when the request window is closed.
    /// </summary>
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(
        Guid spaceId, Guid groupId,
        [FromQuery] Guid cycleId,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var result = await _mediator.Send(
            new GetAvailableSlotsQuery(spaceId, groupId, cycleId, CurrentUserId), ct);

        return Ok(result);
    }

    /// <summary>
    /// Get details of a specific shift slot by ID.
    /// Includes a read-only flag when the request window is closed.
    /// </summary>
    [HttpGet("{slotId:guid}")]
    public async Task<IActionResult> GetById(
        Guid spaceId, Guid groupId, Guid slotId,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var result = await _mediator.Send(
            new GetShiftSlotDetailQuery(spaceId, groupId, slotId, CurrentUserId), ct);

        if (result is null)
            return NotFound();

        return Ok(result);
    }
}
