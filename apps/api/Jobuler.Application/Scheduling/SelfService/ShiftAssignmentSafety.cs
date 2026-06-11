using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.SelfService;

internal enum ShiftAssignmentConflictKind
{
    None,
    Overlap,
    RestViolation
}

internal static class ShiftAssignmentSafety
{
    public static async Task<ShiftAssignmentConflictKind> FindApprovedAssignmentConflictAsync(
        AppDbContext db,
        Guid personId,
        ShiftSlot targetSlot,
        CancellationToken ct)
    {
        var targetDate = targetSlot.Date;
        var candidateSlots = await db.ShiftRequests
            .AsNoTracking()
            .Where(r => r.SpaceId == targetSlot.SpaceId
                        && r.PersonId == personId
                        && r.Status == ShiftRequestStatus.Approved)
            .Join(
                db.ShiftSlots.AsNoTracking(),
                request => request.ShiftSlotId,
                slot => slot.Id,
                (request, slot) => slot)
            .Where(slot => slot.Id != targetSlot.Id
                           && slot.Date >= targetDate.AddDays(-2)
                           && slot.Date <= targetDate.AddDays(2))
            .ToListAsync(ct);

        if (candidateSlots.Count == 0)
            return ShiftAssignmentConflictKind.None;

        if (candidateSlots.Any(slot => ShiftSlotTimeRange.Overlaps(slot, targetSlot)))
            return ShiftAssignmentConflictKind.Overlap;

        var groupIds = candidateSlots
            .Select(slot => slot.GroupId)
            .Append(targetSlot.GroupId)
            .Distinct()
            .ToList();

        var minRestByGroupId = await db.Groups
            .AsNoTracking()
            .Where(group => groupIds.Contains(group.Id))
            .Select(group => new { group.Id, group.MinRestBetweenShiftsHours })
            .ToDictionaryAsync(group => group.Id, group => group.MinRestBetweenShiftsHours, ct);

        var targetMinRest = minRestByGroupId.GetValueOrDefault(targetSlot.GroupId, 0);
        foreach (var candidateSlot in candidateSlots)
        {
            var candidateMinRest = minRestByGroupId.GetValueOrDefault(candidateSlot.GroupId, 0);
            var requiredRest = Math.Max(targetMinRest, candidateMinRest);
            if (requiredRest <= 0)
                continue;

            var gap = ShiftSlotTimeRange.GapBetween(candidateSlot, targetSlot);
            if (gap < TimeSpan.FromHours(requiredRest))
                return ShiftAssignmentConflictKind.RestViolation;
        }

        return ShiftAssignmentConflictKind.None;
    }
}
