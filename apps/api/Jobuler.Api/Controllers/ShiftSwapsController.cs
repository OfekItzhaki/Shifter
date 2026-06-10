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
    private async Task<Guid?> GetCurrentPersonIdAsync(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        return await _db.People
            .Where(p => p.SpaceId == spaceId && p.LinkedUserId == CurrentUserId)
            .Join(
                _db.GroupMemberships.AsNoTracking()
                    .Where(gm => gm.SpaceId == spaceId && gm.GroupId == groupId),
                p => p.Id,
                gm => gm.PersonId,
                (p, _) => p.Id)
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
        var personId = await GetCurrentPersonIdAsync(spaceId, groupId, ct);
        if (personId is null || personId == Guid.Empty)
            return Forbid();

        if (!await ShiftRequestsBelongToGroupAsync(
                spaceId,
                groupId,
                new[] { req.InitiatorShiftRequestId, req.TargetShiftRequestId },
                ct))
            return NotFound();

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
        var personId = await GetCurrentPersonIdAsync(spaceId, groupId, ct);
        if (personId is null || personId == Guid.Empty)
            return Forbid();

        if (!await SwapBelongsToGroupAsync(spaceId, groupId, swapRequestId, ct))
            return NotFound();

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
        var personId = await GetCurrentPersonIdAsync(spaceId, groupId, ct);
        if (personId is null || personId == Guid.Empty)
            return Forbid();

        if (!await SwapBelongsToGroupAsync(spaceId, groupId, swapRequestId, ct))
            return NotFound();

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
        var personId = await GetCurrentPersonIdAsync(spaceId, groupId, ct);
        if (personId is null || personId == Guid.Empty)
            return Forbid();

        if (!await SwapBelongsToGroupAsync(spaceId, groupId, swapRequestId, ct))
            return NotFound();

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
        var personId = await GetCurrentPersonIdAsync(spaceId, groupId, ct);
        if (personId is null || personId == Guid.Empty)
            return Forbid();

        var swaps = await _db.SwapRequests
            .AsNoTracking()
            .Where(s => s.SpaceId == spaceId
                        && s.GroupId == groupId
                        && (s.InitiatorPersonId == personId || s.TargetPersonId == personId))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var requestIds = swaps
            .SelectMany(s => new[] { s.InitiatorShiftRequestId, s.TargetShiftRequestId })
            .Distinct()
            .ToList();

        var shiftDetails = await _db.ShiftRequests
            .AsNoTracking()
            .Where(r => requestIds.Contains(r.Id))
            .Join(_db.ShiftSlots.AsNoTracking(), r => r.ShiftSlotId, slot => slot.Id, (r, slot) => new { Request = r, Slot = slot })
            .Join(_db.GroupTasks.AsNoTracking(), rs => rs.Slot.GroupTaskId, task => task.Id, (rs, task) => new
            {
                rs.Request.Id,
                rs.Slot.Date,
                rs.Slot.StartTime,
                rs.Slot.EndTime,
                TaskName = task.Name
            })
            .ToDictionaryAsync(x => x.Id, ct);

        var personIds = swaps
            .SelectMany(s => new[] { s.InitiatorPersonId, s.TargetPersonId })
            .Distinct()
            .ToList();

        var personNames = await _db.People
            .AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName ?? p.FullName, ct);

        return Ok(swaps.Select(s =>
        {
            shiftDetails.TryGetValue(s.InitiatorShiftRequestId, out var initiatorShift);
            shiftDetails.TryGetValue(s.TargetShiftRequestId, out var targetShift);
            personNames.TryGetValue(s.InitiatorPersonId, out var initiatorName);
            personNames.TryGetValue(s.TargetPersonId, out var targetName);

            return new SwapRequestDto(
                s.Id,
                s.InitiatorPersonId,
                s.TargetPersonId,
                initiatorName ?? "Member",
                targetName ?? "Member",
                s.InitiatorShiftRequestId,
                s.TargetShiftRequestId,
                initiatorShift?.Date ?? default,
                FormatSlotTime(initiatorShift?.StartTime, initiatorShift?.EndTime),
                initiatorShift?.TaskName ?? "Shift",
                targetShift?.Date ?? default,
                FormatSlotTime(targetShift?.StartTime, targetShift?.EndTime),
                targetShift?.TaskName ?? "Shift",
                s.Status.ToString(),
                s.ExpiresAt,
                s.CreatedAt);
        }));
    }

    /// <summary>List approved future shifts owned by a group member for the propose-swap flow.</summary>
    [HttpGet("members/{targetPersonId:guid}/approved-shifts")]
    public async Task<IActionResult> GetMemberApprovedShifts(
        Guid spaceId,
        Guid groupId,
        Guid targetPersonId,
        CancellationToken ct)
    {
        var currentPersonId = await GetCurrentPersonIdAsync(spaceId, groupId, ct);
        if (currentPersonId is null || currentPersonId == Guid.Empty)
            return Forbid();

        if (targetPersonId == currentPersonId.Value)
            return BadRequest(new { error = "Use the mine endpoint for your own shifts." });

        var utcNow = DateTime.UtcNow;

        var shifts = await _db.ShiftRequests
            .AsNoTracking()
            .Where(r => r.SpaceId == spaceId
                        && r.GroupId == groupId
                        && r.PersonId == targetPersonId
                        && r.Status == ShiftRequestStatus.Approved)
            .Join(_db.ShiftSlots.AsNoTracking(), r => r.ShiftSlotId, slot => slot.Id, (r, slot) => new { Request = r, Slot = slot })
            .Join(_db.GroupTasks.AsNoTracking(), rs => rs.Slot.GroupTaskId, task => task.Id, (rs, task) => new { rs.Request, rs.Slot, TaskName = task.Name })
            .Where(x => x.Slot.Date.ToDateTime(x.Slot.StartTime, DateTimeKind.Utc) > utcNow)
            .OrderBy(x => x.Slot.Date)
            .ThenBy(x => x.Slot.StartTime)
            .Select(x => new SwappableShiftDto(
                x.Request.Id,
                x.Request.ShiftSlotId,
                x.Request.GroupId,
                x.Request.SchedulingCycleId,
                x.Request.Status.ToString(),
                x.Request.IsAdminOverride,
                x.Slot.Date,
                x.Slot.StartTime,
                x.Slot.EndTime,
                x.TaskName,
                x.Request.RejectionReason,
                x.Request.CancellationReason,
                x.Request.CancelledAt,
                x.Request.CreatedAt))
            .ToListAsync(ct);

        return Ok(shifts);
    }

    private static string FormatSlotTime(TimeOnly? startsAt, TimeOnly? endsAt) =>
        startsAt is null || endsAt is null ? string.Empty : $"{startsAt:HH\\:mm}-{endsAt:HH\\:mm}";

    private async Task<bool> ShiftRequestsBelongToGroupAsync(
        Guid spaceId,
        Guid groupId,
        Guid[] shiftRequestIds,
        CancellationToken ct)
    {
        var distinctRequestIds = shiftRequestIds.Distinct().ToArray();
        var matchingCount = await _db.ShiftRequests
            .AsNoTracking()
            .CountAsync(r => distinctRequestIds.Contains(r.Id)
                             && r.SpaceId == spaceId
                             && r.GroupId == groupId,
                ct);

        return matchingCount == distinctRequestIds.Length;
    }

    private Task<bool> SwapBelongsToGroupAsync(
        Guid spaceId,
        Guid groupId,
        Guid swapRequestId,
        CancellationToken ct) =>
        _db.SwapRequests
            .AsNoTracking()
            .AnyAsync(s => s.Id == swapRequestId && s.SpaceId == spaceId && s.GroupId == groupId, ct);
}

public record ProposeSwapRequest(Guid InitiatorShiftRequestId, Guid TargetShiftRequestId);

public record SwapRequestDto(
    Guid Id,
    Guid InitiatorPersonId,
    Guid TargetPersonId,
    string InitiatorPersonName,
    string TargetPersonName,
    Guid InitiatorShiftRequestId,
    Guid TargetShiftRequestId,
    DateOnly InitiatorSlotDate,
    string InitiatorSlotTime,
    string InitiatorTaskName,
    DateOnly TargetSlotDate,
    string TargetSlotTime,
    string TargetTaskName,
    string Status,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

public record SwappableShiftDto(
    Guid Id,
    Guid ShiftSlotId,
    Guid GroupId,
    Guid SchedulingCycleId,
    string Status,
    bool IsAdminOverride,
    DateOnly SlotDate,
    TimeOnly SlotStartTime,
    TimeOnly SlotEndTime,
    string TaskName,
    string? RejectionReason,
    string? CancellationReason,
    DateTime? CancelledAt,
    DateTime CreatedAt);
