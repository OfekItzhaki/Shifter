using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Scheduling.SelfService.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/self-service-cycles")]
[Authorize]
public class SelfServiceCyclesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly ISlotGenerationService _slotGeneration;
    private readonly IMediator _mediator;

    public SelfServiceCyclesController(
        AppDbContext db,
        IPermissionService permissions,
        ISlotGenerationService slotGeneration,
        IMediator mediator)
    {
        _db = db;
        _permissions = permissions;
        _slotGeneration = slotGeneration;
        _mediator = mediator;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var cycle = await ResolveCurrentCycleAsync(spaceId, groupId, ct);
        if (cycle is null)
            return Ok(SelfServiceCycleStatusResponse.Empty());

        return Ok(await BuildStatusAsync(cycle, ct));
    }

    [HttpPost("generate-next")]
    public async Task<IActionResult> GenerateNext(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);
        if (!await IsSelfServiceGroupAsync(spaceId, groupId, ct))
            return BadRequest(new { error = "Group is not a self-service group." });

        var config = await _db.SelfServiceConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.GroupId == groupId, ct);

        if (config is null)
            return BadRequest(new { error = "Self-service config is required before generating cycles." });

        var latestCycle = await _db.SchedulingCycles
            .AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.GroupId == groupId)
            .OrderByDescending(c => c.EndsAt)
            .FirstOrDefaultAsync(ct);

        var now = DateTime.UtcNow;
        var nextStart = latestCycle?.EndsAt ?? now.Date.AddDays(1);
        if (nextStart <= now)
            nextStart = now.Date.AddDays(1);

        var nextEnd = nextStart.AddDays(config.CycleDurationDays);
        var requestWindowOpens = nextStart.AddHours(-config.RequestWindowOpenOffsetHours);
        var requestWindowCloses = nextStart.AddHours(-config.RequestWindowCloseOffsetHours);

        var cycle = SchedulingCycle.Create(
            spaceId,
            groupId,
            nextStart,
            nextEnd,
            requestWindowOpens,
            requestWindowCloses);

        _db.SchedulingCycles.Add(cycle);
        await _db.SaveChangesAsync(ct);

        await _slotGeneration.GenerateSlotsForCycleAsync(groupId, cycle.Id, ct);

        var generated = await _db.SchedulingCycles
            .AsNoTracking()
            .FirstAsync(c => c.Id == cycle.Id, ct);

        return Ok(await BuildStatusAsync(generated, ct));
    }

    [HttpPost("{cycleId:guid}/open")]
    public async Task<IActionResult> OpenWindow(
        Guid spaceId,
        Guid groupId,
        Guid cycleId,
        [FromBody] OpenCycleWindowRequest? request,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var cycle = await GetCycleForUpdateAsync(spaceId, groupId, cycleId, ct);
        if (cycle is null)
            return NotFound();

        var now = DateTime.UtcNow;
        var requestedHours = Math.Clamp(request?.Hours ?? 24, 1, 720);
        var closeAt = now.AddHours(requestedHours);
        if (closeAt > cycle.StartsAt)
            closeAt = cycle.StartsAt;

        if (closeAt <= now)
            return BadRequest(new { error = "Cannot open a request window for a cycle that has already started." });

        cycle.UpdateRequestWindow(now.AddMinutes(-1), closeAt);
        await _db.SaveChangesAsync(ct);

        return Ok(await BuildStatusAsync(cycle, ct));
    }

    [HttpPost("{cycleId:guid}/close")]
    public async Task<IActionResult> CloseWindow(Guid spaceId, Guid groupId, Guid cycleId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var cycle = await GetCycleForUpdateAsync(spaceId, groupId, cycleId, ct);
        if (cycle is null)
            return NotFound();

        var wasOpen = cycle.IsRequestWindowOpen(DateTime.UtcNow);
        var closeAt = DateTime.UtcNow;
        if (closeAt > cycle.StartsAt)
            closeAt = cycle.StartsAt;

        cycle.UpdateRequestWindowClose(closeAt);
        await _db.SaveChangesAsync(ct);

        if (wasOpen)
        {
            await _mediator.Send(new CheckUnderScheduledMembersCommand(spaceId, groupId, cycleId), ct);
        }

        return Ok(await BuildStatusAsync(cycle, ct));
    }

    [HttpPost("{cycleId:guid}/check-under-scheduled")]
    public async Task<IActionResult> CheckUnderScheduled(Guid spaceId, Guid groupId, Guid cycleId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var result = await _mediator.Send(new CheckUnderScheduledMembersCommand(spaceId, groupId, cycleId), ct);
        return Ok(result);
    }

    private async Task<bool> IsSelfServiceGroupAsync(Guid spaceId, Guid groupId, CancellationToken ct) =>
        await _db.Groups
            .AsNoTracking()
            .AnyAsync(g => g.Id == groupId
                           && g.SpaceId == spaceId
                           && g.SchedulingMode == SchedulingMode.SelfService
                           && g.IsActive
                           && g.DeletedAt == null, ct);

    private async Task<SchedulingCycle?> ResolveCurrentCycleAsync(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        return await _db.SchedulingCycles
            .AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.GroupId == groupId && c.EndsAt >= now)
            .OrderBy(c => c.StartsAt < now ? 0 : 1)
            .ThenBy(c => c.StartsAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<SchedulingCycle?> GetCycleForUpdateAsync(Guid spaceId, Guid groupId, Guid cycleId, CancellationToken ct) =>
        await _db.SchedulingCycles
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.SpaceId == spaceId && c.GroupId == groupId, ct);

    private async Task<SelfServiceCycleStatusResponse> BuildStatusAsync(SchedulingCycle cycle, CancellationToken ct)
    {
        var slots = await _db.ShiftSlots
            .AsNoTracking()
            .Where(s => s.SpaceId == cycle.SpaceId
                        && s.GroupId == cycle.GroupId
                        && s.SchedulingCycleId == cycle.Id)
            .Select(s => new
            {
                s.Id,
                s.GroupTaskId,
                s.Date,
                s.StartTime,
                s.EndTime,
                s.Capacity,
                s.CurrentFillCount
            })
            .ToListAsync(ct);

        var approvedCount = await _db.ShiftRequests
            .AsNoTracking()
            .CountAsync(r => r.SpaceId == cycle.SpaceId
                             && r.GroupId == cycle.GroupId
                             && r.SchedulingCycleId == cycle.Id
                             && r.Status == ShiftRequestStatus.Approved, ct);

        var pendingCount = await _db.ShiftRequests
            .AsNoTracking()
            .CountAsync(r => r.SpaceId == cycle.SpaceId
                             && r.GroupId == cycle.GroupId
                             && r.SchedulingCycleId == cycle.Id
                             && r.Status == ShiftRequestStatus.Pending, ct);

        var cycleSlotIds = slots.Select(s => s.Id).ToList();

        var waitlistCount = await _db.WaitlistEntries
            .AsNoTracking()
            .CountAsync(w => w.SpaceId == cycle.SpaceId
                             && cycleSlotIds.Contains(w.ShiftSlotId)
                             && (w.Status == WaitlistEntryStatus.Waiting || w.Status == WaitlistEntryStatus.Offered), ct);

        var pendingAbsenceReports = await _db.ShiftAbsenceReports
            .AsNoTracking()
            .Where(r => r.SpaceId == cycle.SpaceId
                        && r.GroupId == cycle.GroupId
                        && r.SchedulingCycleId == cycle.Id
                        && r.Status == ShiftAbsenceReportStatus.Pending)
            .Select(r => new { r.IsLate })
            .ToListAsync(ct);

        var pendingShiftChangeRequestCount = await _db.ShiftChangeRequests
            .AsNoTracking()
            .CountAsync(r => r.SpaceId == cycle.SpaceId
                             && r.GroupId == cycle.GroupId
                             && r.SchedulingCycleId == cycle.Id
                             && r.Status == ShiftChangeRequestStatus.Pending, ct);

        var pendingSwapRequestCount = await _db.SwapRequests
            .AsNoTracking()
            .CountAsync(s => s.SpaceId == cycle.SpaceId
                             && s.GroupId == cycle.GroupId
                             && s.Status == SwapRequestStatus.Pending
                             && _db.ShiftRequests.Any(r => r.Id == s.InitiatorShiftRequestId
                                                            && r.SchedulingCycleId == cycle.Id),
                ct);

        var pendingSpecialLeaveRequestCount = await _db.SpecialLeaveRequests
            .AsNoTracking()
            .Where(r => r.SpaceId == cycle.SpaceId
                        && r.Status == SpecialLeaveRequestStatus.Pending
                        && r.StartsAt < cycle.EndsAt
                        && r.EndsAt > cycle.StartsAt)
            .Join(
                _db.GroupMemberships
                    .AsNoTracking()
                    .Where(m => m.SpaceId == cycle.SpaceId && m.GroupId == cycle.GroupId),
                request => request.PersonId,
                membership => membership.PersonId,
                (request, _) => request.Id)
            .Distinct()
            .CountAsync(ct);

        var taskIds = slots
            .Where(s => s.CurrentFillCount < s.Capacity)
            .Select(s => s.GroupTaskId)
            .Distinct()
            .ToList();

        var taskNames = await _db.GroupTasks
            .AsNoTracking()
            .Where(t => taskIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var underfilledSlotCount = slots.Count(s => s.CurrentFillCount < s.Capacity);

        var underfilledSlots = slots
            .Where(s => s.CurrentFillCount < s.Capacity)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .Take(12)
            .Select(s => new UnderfilledSlotResponse(
                s.Id,
                s.Date,
                s.StartTime,
                s.EndTime,
                taskNames.GetValueOrDefault(s.GroupTaskId, "Shift"),
                s.CurrentFillCount,
                s.Capacity,
                s.Capacity - s.CurrentFillCount))
            .ToList();

        var now = DateTime.UtcNow;
        var totalCapacity = slots.Sum(s => s.Capacity);
        var filled = slots.Sum(s => s.CurrentFillCount);

        return new SelfServiceCycleStatusResponse(
            cycle.Id,
            cycle.StartsAt,
            cycle.EndsAt,
            cycle.RequestWindowOpensAt,
            cycle.RequestWindowClosesAt,
            cycle.IsRequestWindowOpen(now),
            cycle.IsGenerated,
            slots.Count,
            totalCapacity,
            filled,
            approvedCount,
            pendingCount,
            waitlistCount,
            pendingAbsenceReports.Count,
            pendingAbsenceReports.Count(r => r.IsLate),
            pendingShiftChangeRequestCount,
            pendingSwapRequestCount,
            pendingSpecialLeaveRequestCount,
            underfilledSlotCount,
            underfilledSlots);
    }
}

public record OpenCycleWindowRequest(int? Hours);

public record UnderfilledSlotResponse(
    Guid ShiftSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string TaskName,
    int CurrentFillCount,
    int Capacity,
    int OpenSeats);

public record SelfServiceCycleStatusResponse(
    Guid? CycleId,
    DateTime? StartsAt,
    DateTime? EndsAt,
    DateTime? RequestWindowOpensAt,
    DateTime? RequestWindowClosesAt,
    bool RequestWindowOpen,
    bool IsGenerated,
    int SlotCount,
    int TotalCapacity,
    int FilledCount,
    int ApprovedCount,
    int PendingCount,
    int WaitlistCount,
    int PendingAbsenceReportCount,
    int LatePendingAbsenceReportCount,
    int PendingShiftChangeRequestCount,
    int PendingSwapRequestCount,
    int PendingSpecialLeaveRequestCount,
    int UnderfilledSlotCount,
    IReadOnlyList<UnderfilledSlotResponse> UnderfilledSlots)
{
    public static SelfServiceCycleStatusResponse Empty() =>
        new(null, null, null, null, null, false, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, []);
}
