using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.SelfService;
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
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/shift-requests")]
[Authorize]
public class ShiftRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;
    private readonly IShiftRequestService _shiftRequestService;
    private readonly AppDbContext _db;

    public ShiftRequestsController(
        IMediator mediator,
        IPermissionService permissions,
        IShiftRequestService shiftRequestService,
        AppDbContext db)
    {
        _mediator = mediator;
        _permissions = permissions;
        _shiftRequestService = shiftRequestService;
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Submit a shift request for the current member on a specific slot.
    /// The member is resolved from the authenticated user's linked person in the space.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Submit(
        Guid spaceId, Guid groupId,
        [FromBody] SubmitShiftRequestRequest req,
        CancellationToken ct)
    {
        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

        var result = await _shiftRequestService.ProcessRequestAsync(personId.Value, req.ShiftSlotId, ct);

        if (!result.Success)
        {
            return UnprocessableEntity(new ShiftRequestErrorResponse(
                Error: result.RejectionReason!,
                AlternativeSlots: result.AlternativeSlots));
        }

        return Created("", new ShiftRequestSuccessResponse(
            ShiftRequestId: result.ShiftRequestId!.Value));
    }

    /// <summary>
    /// Cancel a previously approved shift request for the current member.
    /// Requires a cancellation reason between 1 and 500 characters.
    /// </summary>
    [HttpPost("{shiftRequestId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid spaceId, Guid groupId, Guid shiftRequestId,
        [FromBody] CancelShiftRequestRequest req,
        CancellationToken ct)
    {
        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

        var result = await _shiftRequestService.CancelRequestAsync(personId.Value, shiftRequestId, req.Reason, ct);

        if (!result.Success)
        {
            return UnprocessableEntity(new { error = result.ErrorMessage });
        }

        return NoContent();
    }

    /// <summary>
    /// List the current member's shift requests for a group, optionally filtered by scheduling cycle.
    /// </summary>
    [HttpGet("mine")]
    public async Task<IActionResult> ListMine(
        Guid spaceId, Guid groupId,
        [FromQuery] Guid? schedulingCycleId,
        CancellationToken ct)
    {
        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

        var result = await _mediator.Send(
            new GetMyShiftRequestsQuery(spaceId, groupId, personId.Value, schedulingCycleId), ct);

        return Ok(result);
    }

    /// <summary>
    /// Resolves the current authenticated user's person ID within the given space.
    /// Returns null if the user has no linked person in this space.
    /// </summary>
    private async Task<Guid?> ResolvePersonIdAsync(Guid spaceId, CancellationToken ct)
    {
        var personId = await _db.People
            .AsNoTracking()
            .Where(p => p.SpaceId == spaceId && p.LinkedUserId == CurrentUserId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        return personId == Guid.Empty ? null : personId;
    }
}

// --- Request DTOs ---

public record SubmitShiftRequestRequest(Guid ShiftSlotId);

public record CancelShiftRequestRequest(string Reason);

// --- Response DTOs ---

public record ShiftRequestSuccessResponse(Guid ShiftRequestId);

public record ShiftRequestErrorResponse(
    string Error,
    IReadOnlyList<Application.Scheduling.SelfService.Models.AvailableSlotDto>? AlternativeSlots);
