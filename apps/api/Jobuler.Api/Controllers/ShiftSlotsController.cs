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
    /// Get approved member assignments for the current self-service cycle's slots.
    /// Used by admin override tooling to remove or correct existing assignments after reload.
    /// </summary>
    [HttpGet("admin/assignments")]
    public async Task<IActionResult> GetAdminAssignments(
        Guid spaceId,
        Guid groupId,
        [FromQuery] string cycleId,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SchedulePublish, ct);

        var resolvedCycleId = await TryResolveCycleIdAsync(spaceId, groupId, cycleId, ct);
        if (resolvedCycleId is null)
            return Ok(Array.Empty<ShiftSlotAssignmentResponse>());

        var assignments = await _db.ShiftRequests
            .AsNoTracking()
            .Where(r => r.SpaceId == spaceId
                        && r.GroupId == groupId
                        && r.SchedulingCycleId == resolvedCycleId.Value
                        && r.Status == Domain.Scheduling.ShiftRequestStatus.Approved)
            .Join(
                _db.People.AsNoTracking().Where(p => p.SpaceId == spaceId),
                request => request.PersonId,
                person => person.Id,
                (request, person) => new
                {
                    ShiftRequestId = request.Id,
                    request.ShiftSlotId,
                    request.PersonId,
                    PersonName = person.DisplayName ?? person.FullName
                })
            .OrderBy(a => a.PersonName)
            .ToListAsync(ct);

        var requestIds = assignments.Select(a => a.ShiftRequestId).ToList();
        var attendanceRecords = await _db.ShiftAttendanceRecords
            .AsNoTracking()
            .Where(r => r.SpaceId == spaceId
                        && r.GroupId == groupId
                        && requestIds.Contains(r.ShiftRequestId))
            .Select(r => new { r.ShiftRequestId, r.Status, r.RecordedAt })
            .ToListAsync(ct);
        var attendanceByRequestId = attendanceRecords.ToDictionary(r => r.ShiftRequestId);

        return Ok(assignments.Select(a =>
        {
            attendanceByRequestId.TryGetValue(a.ShiftRequestId, out var attendance);
            return new ShiftSlotAssignmentResponse(
                a.ShiftRequestId,
                a.ShiftSlotId,
                a.PersonId,
                a.PersonName,
                attendance?.Status.ToString(),
                attendance?.RecordedAt);
        }).ToList());
    }

    /// <summary>
    /// Get all shift slots for a cycle for admin manual assignment tooling.
    /// This intentionally does not use member availability filtering because admins need to see
    /// the complete coverage board, including full slots that may receive manual overrides.
    /// </summary>
    [HttpGet("admin/slots")]
    public async Task<IActionResult> GetAdminSlots(
        Guid spaceId,
        Guid groupId,
        [FromQuery] string cycleId,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SchedulePublish, ct);

        var resolvedCycleId = await TryResolveCycleIdAsync(spaceId, groupId, cycleId, ct);
        if (resolvedCycleId is null)
            return Ok(new AdminShiftSlotsResponse(
                Array.Empty<AdminShiftSlotResponse>(),
                false,
                null,
                null,
                null));

        var cycle = await _db.SchedulingCycles
            .AsNoTracking()
            .Where(c => c.Id == resolvedCycleId.Value && c.SpaceId == spaceId && c.GroupId == groupId)
            .Select(c => new { c.Id, c.RequestWindowOpensAt, c.RequestWindowClosesAt })
            .FirstOrDefaultAsync(ct);

        if (cycle is null)
            return Ok(new AdminShiftSlotsResponse(
                Array.Empty<AdminShiftSlotResponse>(),
                false,
                null,
                null,
                null));

        var slots = await _db.ShiftSlots
            .AsNoTracking()
            .Where(s => s.SpaceId == spaceId
                        && s.GroupId == groupId
                        && s.SchedulingCycleId == resolvedCycleId.Value
                        && s.Status == Domain.Scheduling.ShiftSlotStatus.Open)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .Join(
                _db.GroupTasks.AsNoTracking(),
                slot => slot.GroupTaskId,
                task => task.Id,
                (slot, task) => new AdminShiftSlotResponse(
                    slot.Id,
                    slot.Date,
                    slot.StartTime,
                    slot.EndTime,
                    task.Name,
                    slot.CurrentFillCount,
                    slot.Capacity,
                    slot.SchedulingCycleId))
            .ToListAsync(ct);

        return Ok(new AdminShiftSlotsResponse(
            slots,
            false,
            cycle.RequestWindowOpensAt,
            cycle.RequestWindowClosesAt,
            cycle.Id));
    }

    /// <summary>
    /// Get available shift slots for the current member in a scheduling cycle.
    /// Returns safe slots for picking, plus safe full slots that can be joined through the waitlist.
    /// Excludes already-claimed and overlapping slots.
    /// Includes a read-only flag when the request window is closed.
    /// </summary>
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(
        Guid spaceId, Guid groupId,
        [FromQuery] string cycleId,
        CancellationToken ct)
    {
        Guid resolvedCycleId;
        if (string.Equals(cycleId, "current", StringComparison.OrdinalIgnoreCase))
        {
            resolvedCycleId = await TryResolveCycleIdAsync(spaceId, groupId, cycleId, ct) ?? Guid.Empty;

            if (resolvedCycleId == Guid.Empty)
                return Ok(new { slots = Array.Empty<object>(), requestWindowOpen = false, requestWindowOpensAt = (DateTime?)null, requestWindowClosesAt = (DateTime?)null, currentCycleId = (Guid?)null });
        }
        else if (!Guid.TryParse(cycleId, out resolvedCycleId))
        {
            return BadRequest(new { error = "Invalid cycleId. Use a scheduling cycle id or 'current'." });
        }

        var result = await _mediator.Send(
            new GetAvailableSlotsQuery(
                spaceId,
                groupId,
                resolvedCycleId,
                CurrentUserId,
                IncludeFullSlots: true),
            ct);

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
        var result = await _mediator.Send(
            new GetShiftSlotDetailQuery(spaceId, groupId, slotId, CurrentUserId), ct);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    private async Task<Guid?> TryResolveCycleIdAsync(Guid spaceId, Guid groupId, string cycleId, CancellationToken ct)
    {
        if (!string.Equals(cycleId, "current", StringComparison.OrdinalIgnoreCase))
            return Guid.TryParse(cycleId, out var parsedCycleId) ? parsedCycleId : null;

        var now = DateTime.UtcNow;
        var resolvedCycleId = await _db.SchedulingCycles
            .AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.GroupId == groupId && c.EndsAt >= now)
            .OrderBy(c => c.StartsAt < now ? 0 : 1)
            .ThenBy(c => c.StartsAt)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        return resolvedCycleId == Guid.Empty ? null : resolvedCycleId;
    }
}

public record ShiftSlotAssignmentResponse(
    Guid ShiftRequestId,
    Guid ShiftSlotId,
    Guid PersonId,
    string PersonName,
    string? AttendanceStatus,
    DateTime? AttendanceRecordedAt);

public record AdminShiftSlotsResponse(
    IReadOnlyList<AdminShiftSlotResponse> Slots,
    bool RequestWindowOpen,
    DateTime? RequestWindowOpensAt,
    DateTime? RequestWindowClosesAt,
    Guid? CurrentCycleId);

public record AdminShiftSlotResponse(
    Guid ShiftSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string TaskName,
    int CurrentFillCount,
    int Capacity,
    Guid SchedulingCycleId);
