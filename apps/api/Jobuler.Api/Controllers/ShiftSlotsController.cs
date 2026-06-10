using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.SelfService.Queries;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/shift-slots")]
[Authorize]
public class ShiftSlotsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;
    private readonly AppDbContext _db;

    public ShiftSlotsController(IMediator mediator, IPermissionService permissions, AppDbContext db)
    {
        _mediator = mediator;
        _permissions = permissions;
        _db = db;
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
        [FromQuery] string cycleId,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        Guid resolvedCycleId;
        if (string.Equals(cycleId, "current", StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTime.UtcNow;
            resolvedCycleId = await _db.SchedulingCycles
                .AsNoTracking()
                .Where(c => c.SpaceId == spaceId && c.GroupId == groupId && c.EndsAt >= now)
                .OrderBy(c => c.StartsAt < now ? 0 : 1)
                .ThenBy(c => c.StartsAt)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(ct);

            if (resolvedCycleId == Guid.Empty)
                return Ok(new { slots = Array.Empty<object>(), requestWindowOpen = false, requestWindowOpensAt = (DateTime?)null, requestWindowClosesAt = (DateTime?)null, currentCycleId = (Guid?)null });
        }
        else if (!Guid.TryParse(cycleId, out resolvedCycleId))
        {
            return BadRequest(new { error = "Invalid cycleId. Use a scheduling cycle id or 'current'." });
        }

        var result = await _mediator.Send(
            new GetAvailableSlotsQuery(spaceId, groupId, resolvedCycleId, CurrentUserId), ct);

        var cycle = await _db.SchedulingCycles
            .AsNoTracking()
            .Where(c => c.Id == resolvedCycleId)
            .Select(c => new { c.RequestWindowOpensAt, c.RequestWindowClosesAt })
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            slots = result.Slots,
            requestWindowOpen = !result.IsReadOnly,
            requestWindowOpensAt = cycle?.RequestWindowOpensAt,
            requestWindowClosesAt = cycle?.RequestWindowClosesAt,
            currentCycleId = resolvedCycleId
        });
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
