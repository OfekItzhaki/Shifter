using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Scheduling.SelfService.Commands;
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
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/waitlist")]
[Authorize]
public class WaitlistController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;
    private readonly IWaitlistService _waitlistService;
    private readonly AppDbContext _db;

    public WaitlistController(
        IMediator mediator,
        IPermissionService permissions,
        IWaitlistService waitlistService,
        AppDbContext db)
    {
        _mediator = mediator;
        _permissions = permissions;
        _waitlistService = waitlistService;
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Join the waitlist for a full shift slot.
    /// The member is resolved from the authenticated user's linked person in the space.
    /// Rejects duplicates (Req 9.7).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Join(
        Guid spaceId, Guid groupId,
        [FromBody] JoinWaitlistRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

        var result = await _waitlistService.JoinWaitlistAsync(personId.Value, req.ShiftSlotId, ct);

        if (!result.Success)
        {
            return UnprocessableEntity(new WaitlistErrorResponse(Error: result.ErrorMessage!));
        }

        return Created("", new JoinWaitlistResponse(
            Position: result.Position!.Value,
            ShiftSlotId: req.ShiftSlotId));
    }

    /// <summary>
    /// Accept a waitlist offer for a shift slot.
    /// Processes the acceptance as a standard shift request (subject to Max_Shifts validation).
    /// If Max_Shifts validation fails, removes the member from the waitlist and cascades to the next member (Req 9.5).
    /// </summary>
    [HttpPost("accept")]
    public async Task<IActionResult> AcceptOffer(
        Guid spaceId, Guid groupId,
        [FromBody] AcceptWaitlistOfferRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

        var result = await _mediator.Send(
            new AcceptWaitlistOfferCommand(spaceId, personId.Value, req.ShiftSlotId), ct);

        if (!result.Success)
        {
            return UnprocessableEntity(new WaitlistErrorResponse(Error: result.ErrorMessage!));
        }

        return Ok(new AcceptWaitlistOfferResponse(ShiftRequestId: result.ShiftRequestId!.Value));
    }

    /// <summary>
    /// Leave the waitlist for a shift slot.
    /// If the member has an active offer, treats removal as a decline and cascades to the next member (Req 9.6).
    /// </summary>
    [HttpDelete("{shiftSlotId:guid}")]
    public async Task<IActionResult> Leave(
        Guid spaceId, Guid groupId, Guid shiftSlotId,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

        await _waitlistService.LeaveWaitlistAsync(personId.Value, shiftSlotId, ct);

        return NoContent();
    }

    /// <summary>
    /// Get the current member's active waitlist entries (Waiting or Offered status).
    /// Returns entries sorted by slot date and start time.
    /// </summary>
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        Guid spaceId, Guid groupId,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

        var result = await _mediator.Send(
            new GetMyWaitlistEntriesQuery(spaceId, personId.Value), ct);

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

public record JoinWaitlistRequest(Guid ShiftSlotId);

public record AcceptWaitlistOfferRequest(Guid ShiftSlotId);

// --- Response DTOs ---

public record JoinWaitlistResponse(int Position, Guid ShiftSlotId);

public record AcceptWaitlistOfferResponse(Guid ShiftRequestId);

public record WaitlistErrorResponse(string Error);
