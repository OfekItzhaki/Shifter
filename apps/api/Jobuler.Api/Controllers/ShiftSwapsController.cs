using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

/// <summary>
/// Manages shift swap proposals between members.
/// Members can propose, accept, decline, cancel swaps, and view their swap history.
/// Requirements: 12.1, 12.3, 12.5, 12.6, 12.8, 12.9
/// </summary>
[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/shift-swaps")]
[Authorize]
public class ShiftSwapsController : ControllerBase
{
    private readonly IShiftSwapService _swapService;
    private readonly AppDbContext _db;

    public ShiftSwapsController(IShiftSwapService swapService, AppDbContext db)
    {
        _swapService = swapService;
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Resolves the current user's person ID within the given space.
    /// </summary>
    private async Task<Guid?> GetCurrentPersonIdAsync(Guid spaceId, CancellationToken ct)
    {
        return await _db.People
            .Where(p => p.SpaceId == spaceId && p.LinkedUserId == CurrentUserId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Propose a shift swap with another member.
    /// The initiator offers their approved shift and requests the target's approved shift.
    /// Req 12.1, 12.8, 12.9
    /// </summary>
    [HttpPost("propose")]
    public async Task<IActionResult> ProposeSwap(
        Guid spaceId,
        Guid groupId,
        [FromBody] ProposeSwapRequest req,
        CancellationToken ct)
    {
        var personId = await GetCurrentPersonIdAsync(spaceId, ct);
        if (personId is null || personId == Guid.Empty)
            return Forbid();

        var result = await _swapService.ProposeSwapAsync(
            personId.Value, req.InitiatorShiftRequestId, req.TargetShiftRequestId, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Created("", new { swapRequestId = result.SwapRequestId });
    }

    /// <summary>
    /// Accept a pending swap request. Only the target member can accept.
    /// Validates no time-overlap or rest-period conflicts before executing the swap.
    /// Req 12.3
    /// </summary>
    [HttpPost("{swapRequestId:guid}/accept")]
    public async Task<IActionResult> AcceptSwap(
        Guid spaceId,
        Guid groupId,
        Guid swapRequestId,
        CancellationToken ct)
    {
        var personId = await GetCurrentPersonIdAsync(spaceId, ct);
        if (personId is null || personId == Guid.Empty)
            return Forbid();

        var result = await _swapService.AcceptSwapAsync(personId.Value, swapRequestId, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { swapRequestId = result.SwapRequestId });
    }

    /// <summary>
    /// Decline a pending swap request. Only the target member can decline.
    /// Req 12.5
    /// </summary>
    [HttpPost("{swapRequestId:guid}/decline")]
    public async Task<IActionResult> DeclineSwap(
        Guid spaceId,
        Guid groupId,
        Guid swapRequestId,
        CancellationToken ct)
    {
        var personId = await GetCurrentPersonIdAsync(spaceId, ct);
        if (personId is null || personId == Guid.Empty)
            return Forbid();

        await _swapService.DeclineSwapAsync(personId.Value, swapRequestId, ct);

        return NoContent();
    }

    /// <summary>
    /// Cancel a pending swap request. Only the initiator can cancel.
    /// Req 12.6
    /// </summary>
    [HttpPost("{swapRequestId:guid}/cancel")]
    public async Task<IActionResult> CancelSwap(
        Guid spaceId,
        Guid groupId,
        Guid swapRequestId,
        CancellationToken ct)
    {
        var personId = await GetCurrentPersonIdAsync(spaceId, ct);
        if (personId is null || personId == Guid.Empty)
            return Forbid();

        await _swapService.CancelSwapAsync(personId.Value, swapRequestId, ct);

        return NoContent();
    }

    /// <summary>
    /// Get all swap requests where the current member is either the initiator or target.
    /// Returns swaps filtered by the group context.
    /// </summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMySwaps(
        Guid spaceId,
        Guid groupId,
        CancellationToken ct)
    {
        var personId = await GetCurrentPersonIdAsync(spaceId, ct);
        if (personId is null || personId == Guid.Empty)
            return Forbid();

        var swaps = await _db.SwapRequests
            .AsNoTracking()
            .Where(s => s.SpaceId == spaceId
                        && s.GroupId == groupId
                        && (s.InitiatorPersonId == personId || s.TargetPersonId == personId))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SwapRequestDto(
                s.Id,
                s.InitiatorPersonId,
                s.TargetPersonId,
                s.InitiatorShiftRequestId,
                s.TargetShiftRequestId,
                s.Status.ToString(),
                s.ExpiresAt,
                s.CreatedAt))
            .ToListAsync(ct);

        return Ok(swaps);
    }
}

public record ProposeSwapRequest(Guid InitiatorShiftRequestId, Guid TargetShiftRequestId);

public record SwapRequestDto(
    Guid Id,
    Guid InitiatorPersonId,
    Guid TargetPersonId,
    Guid InitiatorShiftRequestId,
    Guid TargetShiftRequestId,
    string Status,
    DateTime? ExpiresAt,
    DateTime CreatedAt);
