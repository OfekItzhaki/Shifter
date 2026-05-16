using Jobuler.Application.Scheduling;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record HistoricalScheduleResponseDto(
    List<DailySnapshotDto> Assignments,
    bool RetentionExceeded);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetHistoricalScheduleQuery(
    Guid SpaceId,
    Guid GroupId,
    DateOnly StartDate,
    DateOnly EndDate) : IRequest<HistoricalScheduleResponseDto>;

public class GetHistoricalScheduleQueryHandler : IRequestHandler<GetHistoricalScheduleQuery, HistoricalScheduleResponseDto>
{
    private readonly IAssignmentSnapshotService _snapshotService;
    private readonly AppDbContext _db;

    public GetHistoricalScheduleQueryHandler(
        IAssignmentSnapshotService snapshotService,
        AppDbContext db)
    {
        _snapshotService = snapshotService;
        _db = db;
    }

    public async Task<HistoricalScheduleResponseDto> Handle(GetHistoricalScheduleQuery req, CancellationToken ct)
    {
        var snapshots = await _snapshotService.GetHistoricalAsync(
            req.SpaceId, req.GroupId, req.StartDate, req.EndDate, ct);

        // If snapshots exist, return them directly
        if (snapshots.Count > 0)
        {
            return new HistoricalScheduleResponseDto(
                Assignments: snapshots,
                RetentionExceeded: false);
        }

        // ── Fallback: query assignments from the most recent published version ──
        var fallbackResults = await GetFallbackFromAssignmentsAsync(req, ct);

        if (fallbackResults.Count > 0)
        {
            return new HistoricalScheduleResponseDto(
                Assignments: fallbackResults,
                RetentionExceeded: false);
        }

        // No data from either source — flag retention exceeded if date is in the past
        var retentionExceeded = req.StartDate < DateOnly.FromDateTime(DateTime.UtcNow);

        return new HistoricalScheduleResponseDto(
            Assignments: [],
            RetentionExceeded: retentionExceeded);
    }

    /// <summary>
    /// Fallback: finds the most recent published schedule version whose assignments
    /// overlap with the requested date range, then maps those assignments to DTOs.
    /// </summary>
    private async Task<List<DailySnapshotDto>> GetFallbackFromAssignmentsAsync(
        GetHistoricalScheduleQuery req, CancellationToken ct)
    {
        // Convert date range to DateTime for comparison with slot times
        var rangeStart = req.StartDate.ToDateTime(TimeOnly.MinValue);
        var rangeEnd = req.EndDate.ToDateTime(new TimeOnly(23, 59, 59));

        // Get group members to filter assignments by group
        var groupMemberPersonIds = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.SpaceId == req.SpaceId && m.GroupId == req.GroupId)
            .Select(m => m.PersonId)
            .ToListAsync(ct);

        if (groupMemberPersonIds.Count == 0)
            return [];

        // Find the most recent published version for this space that has assignments
        // overlapping with the requested date range (via task slots or group tasks)
        var publishedVersions = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId
                && (v.Status == ScheduleVersionStatus.Published
                    || v.Status == ScheduleVersionStatus.Archived))
            .OrderByDescending(v => v.PublishedAt)
            .Select(v => v.Id)
            .ToListAsync(ct);

        if (publishedVersions.Count == 0)
            return [];

        // Load task slots that overlap with the requested date range
        var taskSlots = await _db.TaskSlots.AsNoTracking()
            .Where(s => s.SpaceId == req.SpaceId
                && s.StartsAt < rangeEnd
                && s.EndsAt > rangeStart)
            .ToDictionaryAsync(s => s.Id, ct);

        // Load group tasks for this group (for derived slot resolution)
        var groupTasks = await _db.GroupTasks.AsNoTracking()
            .Where(t => t.SpaceId == req.SpaceId && t.GroupId == req.GroupId)
            .ToListAsync(ct);

        // Load task types for name resolution
        var taskTypes = await _db.TaskTypes.AsNoTracking()
            .Where(t => t.SpaceId == req.SpaceId)
            .ToDictionaryAsync(t => t.Id, ct);

        // Get active subscription period for this group (needed for PeriodId)
        var period = await _db.SubscriptionPeriods.AsNoTracking()
            .Where(sp => sp.SpaceId == req.SpaceId && sp.GroupId == req.GroupId && sp.Status == "active")
            .FirstOrDefaultAsync(ct);

        var periodId = period?.Id ?? Guid.Empty;

        // Try each published version (most recent first) until we find one with matching assignments
        foreach (var versionId in publishedVersions)
        {
            var assignments = await _db.Assignments.AsNoTracking()
                .Where(a => a.ScheduleVersionId == versionId
                    && a.SpaceId == req.SpaceId
                    && groupMemberPersonIds.Contains(a.PersonId))
                .ToListAsync(ct);

            if (assignments.Count == 0)
                continue;

            var results = new List<DailySnapshotDto>();

            foreach (var assignment in assignments)
            {
                var resolved = ResolveSlotTimes(assignment.TaskSlotId, taskSlots, groupTasks, taskTypes);

                DateTime shiftStart;
                DateTime shiftEnd;
                string? burdenLevel;
                string? taskTypeName;

                if (resolved != null)
                {
                    shiftStart = resolved.Value.ShiftStart;
                    shiftEnd = resolved.Value.ShiftEnd;
                    burdenLevel = resolved.Value.BurdenLevel;
                    taskTypeName = resolved.Value.TaskTypeName;
                }
                else
                {
                    // Could not resolve — skip this assignment (no shift times to determine date overlap)
                    continue;
                }

                // Check if this assignment overlaps with the requested date range
                if (shiftEnd <= rangeStart || shiftStart >= rangeEnd)
                    continue;

                // Determine the snapshot date (the day the shift starts)
                var snapshotDate = DateOnly.FromDateTime(shiftStart);

                results.Add(new DailySnapshotDto(
                    Id: assignment.Id,
                    PersonId: assignment.PersonId,
                    GroupId: req.GroupId,
                    SnapshotDate: snapshotDate,
                    TaskTypeId: resolved?.TaskTypeId,
                    SlotId: assignment.TaskSlotId,
                    ShiftStart: shiftStart,
                    ShiftEnd: shiftEnd,
                    BurdenLevel: burdenLevel,
                    VersionId: versionId,
                    PeriodId: periodId,
                    TaskTypeName: taskTypeName));
            }

            if (results.Count > 0)
                return results.OrderBy(r => r.SnapshotDate).ThenBy(r => r.PersonId).ToList();
        }

        return [];
    }

    /// <summary>
    /// Resolves a task slot ID to its shift start/end times, task type, burden level, and task name.
    /// Handles both direct TaskSlot references and derived GroupTask slots.
    /// </summary>
    private static ResolvedSlot? ResolveSlotTimes(
        Guid slotId,
        Dictionary<Guid, TaskSlot> taskSlots,
        List<GroupTask> groupTasks,
        Dictionary<Guid, TaskType> taskTypes)
    {
        // Try direct task slot reference first
        if (taskSlots.TryGetValue(slotId, out var slot))
        {
            string? burdenLevel = null;
            string? taskTypeName = null;
            if (taskTypes.TryGetValue(slot.TaskTypeId, out var tt))
            {
                burdenLevel = tt.BurdenLevel.ToString().ToLower();
                taskTypeName = tt.Name;
            }

            return new ResolvedSlot(slot.StartsAt, slot.EndsAt, slot.TaskTypeId, burdenLevel, taskTypeName);
        }

        // Try derived slot from GroupTask
        return ResolveGroupTaskSlot(slotId, groupTasks);
    }

    /// <summary>
    /// Attempts to resolve a derived task slot ID back to its GroupTask and shift times.
    /// Uses the DeriveShiftGuid algorithm in reverse by trying each GroupTask.
    /// </summary>
    private static ResolvedSlot? ResolveGroupTaskSlot(Guid slotId, IEnumerable<GroupTask> groupTasks)
    {
        foreach (var task in groupTasks)
        {
            var shiftDuration = TimeSpan.FromMinutes(task.ShiftDurationMinutes);
            if (shiftDuration.TotalMinutes < 1) continue;

            var taskBytes = task.Id.ToByteArray();
            var slotBytes = slotId.ToByteArray();

            // Check if the first 12 bytes match (they should for derived GUIDs)
            bool first12Match = true;
            for (int i = 0; i < 12; i++)
            {
                if (taskBytes[i] != slotBytes[i])
                {
                    first12Match = false;
                    break;
                }
            }

            if (!first12Match) continue;

            // Extract the shift index from the XOR of the last 4 bytes
            var indexBytes = new byte[4];
            for (int i = 0; i < 4; i++)
                indexBytes[i] = (byte)(slotBytes[12 + i] ^ taskBytes[12 + i]);
            var shiftIndex = BitConverter.ToInt32(indexBytes, 0);

            if (shiftIndex < 0) continue;

            // Compute the shift start/end from the index
            var shiftStart = task.StartsAt + TimeSpan.FromMinutes((double)shiftIndex * task.ShiftDurationMinutes);
            var shiftEnd = shiftStart + shiftDuration;

            // Validate the shift is within the task's time window
            if (shiftStart >= task.StartsAt && shiftEnd <= task.EndsAt.AddMinutes(1))
            {
                return new ResolvedSlot(
                    shiftStart, shiftEnd, task.Id,
                    task.BurdenLevel.ToString().ToLower(),
                    task.Name);
            }
        }

        return null;
    }

    private record struct ResolvedSlot(
        DateTime ShiftStart, DateTime ShiftEnd, Guid? TaskTypeId, string? BurdenLevel, string? TaskTypeName);
}
