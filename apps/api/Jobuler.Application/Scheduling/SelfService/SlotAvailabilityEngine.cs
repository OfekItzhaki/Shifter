using Jobuler.Application.Scheduling.SelfService.Models;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.SelfService;

/// <summary>
/// Queries available shift slots for a member within a scheduling cycle.
/// Filters out full slots, slots already claimed by the member, and slots
/// that overlap or violate rest windows against the member's approved shifts.
/// Results are sorted by date ascending, then start time ascending.
/// </summary>
public class SlotAvailabilityEngine : ISlotAvailabilityEngine
{
    private readonly AppDbContext _db;

    public SlotAvailabilityEngine(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<SlotAvailabilityResult> GetAvailableSlotsAsync(
        Guid personId, Guid groupId, Guid schedulingCycleId, CancellationToken ct = default)
    {
        // Load the scheduling cycle
        var cycle = await _db.SchedulingCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == schedulingCycleId && c.GroupId == groupId, ct);

        if (cycle is null)
            return new SlotAvailabilityResult([], false, null);

        // Determine request window state
        var utcNow = DateTime.UtcNow;
        var isReadOnly = !cycle.IsRequestWindowOpen(utcNow);
        string? message = isReadOnly ? "Requests are not currently accepted." : null;

        // Load all open slots for this cycle with remaining capacity
        var slotsWithTasks = await _db.ShiftSlots
            .AsNoTracking()
            .Where(s => s.SchedulingCycleId == schedulingCycleId
                        && s.GroupId == groupId
                        && s.Status == ShiftSlotStatus.Open
                        && s.CurrentFillCount < s.Capacity)
            .Join(
                _db.GroupTasks.AsNoTracking(),
                slot => slot.GroupTaskId,
                task => task.Id,
                (slot, task) => new { Slot = slot, TaskName = task.Name })
            .ToListAsync(ct);

        if (slotsWithTasks.Count == 0)
            return new SlotAvailabilityResult([], isReadOnly, message);

        // Load the member's existing pending/approved request slot IDs for this cycle
        var memberRequestedSlotIds = await _db.ShiftRequests
            .AsNoTracking()
            .Where(r => r.PersonId == personId
                        && r.SchedulingCycleId == schedulingCycleId
                        && (r.Status == ShiftRequestStatus.Pending || r.Status == ShiftRequestStatus.Approved))
            .Select(r => r.ShiftSlotId)
            .ToListAsync(ct);

        var memberRequestedSlotIdSet = memberRequestedSlotIds.ToHashSet();

        // Filter slots: exclude already requested and schedule-safety conflicts.
        var availableSlots = new List<AvailableSlotDto>();

        foreach (var item in slotsWithTasks)
        {
            var slot = item.Slot;

            if (slot.Date.ToDateTime(slot.StartTime, DateTimeKind.Utc) <= utcNow)
                continue;

            // Requirement 7.3: Exclude slots where the member already has a pending/approved request
            if (memberRequestedSlotIdSet.Contains(slot.Id))
                continue;

            // Requirement 7.4: Exclude slots that overlap or violate rest windows.
            var assignmentConflict = await ShiftAssignmentSafety.FindApprovedAssignmentConflictAsync(
                _db,
                personId,
                slot,
                ct);

            if (assignmentConflict != ShiftAssignmentConflictKind.None)
                continue;

            availableSlots.Add(new AvailableSlotDto(
                ShiftSlotId: slot.Id,
                Date: slot.Date,
                StartTime: slot.StartTime,
                EndTime: slot.EndTime,
                TaskName: item.TaskName,
                CurrentFillCount: slot.CurrentFillCount,
                Capacity: slot.Capacity));
        }

        // Requirement 7.1: Sort by date ascending, then start time ascending
        var sortedSlots = availableSlots
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .ToList();

        return new SlotAvailabilityResult(sortedSlots, isReadOnly, message);
    }
}
