using Jobuler.Application.Scheduling;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

public class AssignmentSnapshotService : IAssignmentSnapshotService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AssignmentSnapshotService> _logger;

    public AssignmentSnapshotService(AppDbContext db, ILogger<AssignmentSnapshotService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SnapshotDiff> CreateSnapshotsAsync(Guid spaceId, Guid versionId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int added = 0;
        int replaced = 0;
        int preserved = 0;
        var replacedDeltas = new List<AssignmentCountsDelta>();

        // 1. Load the published version
        var version = await _db.ScheduleVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.SpaceId == spaceId, ct)
            ?? throw new KeyNotFoundException($"Schedule version {versionId} not found.");

        // 2. Load assignments for this version
        var assignments = await _db.Assignments.AsNoTracking()
            .Where(a => a.ScheduleVersionId == versionId && a.SpaceId == spaceId)
            .ToListAsync(ct);

        if (assignments.Count == 0)
        {
            _logger.LogInformation("No assignments found for version {VersionId}", versionId);
            return new SnapshotDiff(0, 0, 0, replacedDeltas);
        }

        // 3. Load task slots and group tasks for resolving shift times
        var taskSlots = await _db.TaskSlots.AsNoTracking()
            .Where(s => s.SpaceId == spaceId)
            .ToDictionaryAsync(s => s.Id, ct);

        var groupTasks = await _db.GroupTasks.AsNoTracking()
            .Where(t => t.SpaceId == spaceId)
            .ToListAsync(ct);

        var taskTypes = await _db.TaskTypes.AsNoTracking()
            .Where(t => t.SpaceId == spaceId)
            .ToDictionaryAsync(t => t.Id, ct);

        // 4. Load group memberships to determine which group a person belongs to
        var personIds = assignments.Select(a => a.PersonId).Distinct().ToList();
        var memberships = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.SpaceId == spaceId && personIds.Contains(m.PersonId))
            .ToListAsync(ct);
        var membershipByPerson = memberships
            .GroupBy(m => m.PersonId)
            .ToDictionary(g => g.Key, g => g.First());

        // 5. Get active subscription periods per group
        var groupIds = memberships.Select(m => m.GroupId).Distinct().ToList();
        var periods = await _db.SubscriptionPeriods.AsNoTracking()
            .Where(sp => sp.SpaceId == spaceId && sp.Status == "active" && groupIds.Contains(sp.GroupId))
            .ToListAsync(ct);
        var periodByGroup = periods.ToDictionary(p => p.GroupId, p => p);

        // 6. Load existing future-dated snapshots that might be replaced
        var existingFutureSnapshots = await _db.DailySnapshots
            .Where(ds => ds.SpaceId == spaceId && ds.SnapshotDate >= today)
            .ToListAsync(ct);
        var existingSnapshotLookup = existingFutureSnapshots
            .GroupBy(ds => $"{ds.GroupId}|{ds.PersonId}|{ds.SnapshotDate}|{ds.SlotId}")
            .ToDictionary(g => g.Key, g => g.First());

        // 7. Process each assignment
        foreach (var assignment in assignments)
        {
            // Resolve slot start/end times
            var resolved = ResolveSlotTimes(assignment.TaskSlotId, taskSlots, groupTasks, taskTypes);
            if (resolved == null)
            {
                _logger.LogWarning(
                    "Could not resolve slot times for assignment {AssignmentId}, slot {SlotId}",
                    assignment.Id, assignment.TaskSlotId);
                continue;
            }

            var (shiftStart, shiftEnd, taskTypeId, burdenLevel) = resolved.Value;

            // Determine which calendar days this assignment covers
            var startDate = DateOnly.FromDateTime(shiftStart);
            var endDate = DateOnly.FromDateTime(shiftEnd);
            // If shift ends exactly at midnight, it doesn't cover the next day
            if (shiftEnd.TimeOfDay == TimeSpan.Zero && shiftEnd > shiftStart)
                endDate = endDate.AddDays(-1);

            // Determine group for this person
            if (!membershipByPerson.TryGetValue(assignment.PersonId, out var membership))
            {
                _logger.LogWarning(
                    "Person {PersonId} has no group membership in space {SpaceId}",
                    assignment.PersonId, spaceId);
                continue;
            }

            var groupId = membership.GroupId;

            // Get the subscription period for this group
            if (!periodByGroup.TryGetValue(groupId, out var period))
            {
                _logger.LogWarning(
                    "No active subscription period for group {GroupId}", groupId);
                continue;
            }

            // Create snapshots for each day covered
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                // Property 9: Past-dated snapshot immutability
                if (date < today)
                {
                    preserved++;
                    continue;
                }

                var lookupKey = $"{groupId}|{assignment.PersonId}|{date}|{assignment.TaskSlotId}";

                if (existingSnapshotLookup.TryGetValue(lookupKey, out var existing))
                {
                    // Replace existing future-dated snapshot
                    existing = _db.DailySnapshots.Find(existing.Id)
                        ?? existing;

                    // Track the delta being replaced for counter adjustment
                    replacedDeltas.Add(new AssignmentCountsDelta(
                        TotalAssignments: 1,
                        HardTasks: existing.BurdenLevel == "hard" ? 1 : 0,
                        NightMissions: IsNightShift(existing.ShiftStart) ? 1 : 0,
                        TotalHours: existing.ShiftStart.HasValue && existing.ShiftEnd.HasValue
                            ? (decimal)(existing.ShiftEnd.Value - existing.ShiftStart.Value).TotalHours
                            : 0));

                    // Remove the old snapshot — we'll create a new one
                    _db.DailySnapshots.Remove(existing);
                    replaced++;
                }
                else
                {
                    added++;
                }

                // Create new snapshot
                var snapshot = DailySnapshot.Create(
                    spaceId: spaceId,
                    groupId: groupId,
                    personId: assignment.PersonId,
                    periodId: period.Id,
                    snapshotDate: date,
                    taskTypeId: taskTypeId,
                    slotId: assignment.TaskSlotId,
                    shiftStart: shiftStart,
                    shiftEnd: shiftEnd,
                    burdenLevel: burdenLevel,
                    versionId: versionId);

                _db.DailySnapshots.Add(snapshot);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Snapshots created for version {VersionId}: Added={Added}, Replaced={Replaced}, Preserved={Preserved}",
            versionId, added, replaced, preserved);

        return new SnapshotDiff(added, replaced, preserved, replacedDeltas);
    }

    public async Task<List<DailySnapshotDto>> GetHistoricalAsync(
        Guid spaceId, Guid groupId, DateOnly startDate, DateOnly endDate, CancellationToken ct)
    {
        // 1. Check the group's schedule_history_retention_days setting
        var group = await _db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId && g.SpaceId == spaceId, ct);

        if (group == null)
            return new List<DailySnapshotDto>();

        // Check retention limit via reflection since the property may not be on the domain entity yet
        // We query the raw column value
        int? retentionDays = null;
        if (_db.Database.IsRelational())
        {
            retentionDays = await _db.Database
                .SqlQueryRaw<int?>(
                    "SELECT schedule_history_retention_days AS \"Value\" FROM groups WHERE id = {0}",
                    groupId)
                .FirstOrDefaultAsync(ct);
        }

        if (retentionDays.HasValue)
        {
            var retentionLimit = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-retentionDays.Value);
            if (startDate < retentionLimit)
            {
                // Return empty — data is outside the retention window
                return new List<DailySnapshotDto>();
            }
        }

        // 2. Query daily_snapshots for the date range
        var snapshots = await _db.DailySnapshots.AsNoTracking()
            .Where(ds => ds.SpaceId == spaceId
                && ds.GroupId == groupId
                && ds.SnapshotDate >= startDate
                && ds.SnapshotDate <= endDate)
            .OrderBy(ds => ds.SnapshotDate)
            .ThenBy(ds => ds.PersonId)
            .ToListAsync(ct);

        // 3. Map to DTOs
        return snapshots.Select(ds => new DailySnapshotDto(
            Id: ds.Id,
            PersonId: ds.PersonId,
            GroupId: ds.GroupId,
            SnapshotDate: ds.SnapshotDate,
            TaskTypeId: ds.TaskTypeId,
            SlotId: ds.SlotId,
            ShiftStart: ds.ShiftStart,
            ShiftEnd: ds.ShiftEnd,
            BurdenLevel: ds.BurdenLevel,
            VersionId: ds.VersionId,
            PeriodId: ds.PeriodId
        )).ToList();
    }

    /// <summary>
    /// Resolves a task slot ID to its shift start/end times, task type, and burden level.
    /// Handles both direct TaskSlot references and derived GroupTask slots (via GUID derivation).
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
            if (taskTypes.TryGetValue(slot.TaskTypeId, out var tt))
                burdenLevel = tt.BurdenLevel.ToString().ToLower();

            return new ResolvedSlot(slot.StartsAt, slot.EndsAt, slot.TaskTypeId, burdenLevel);
        }

        // Try derived slot from GroupTask — reverse the DeriveShiftGuid logic
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

            // DeriveShiftGuid XORs the last 4 bytes of the task GUID with the shift index
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
                var effectiveBurden = BurdenScalingService.ComputeEffectiveBurden(
                    task.BurdenLevel, task.SplitCount, task.ShiftDurationMinutes);
                return new ResolvedSlot(
                    shiftStart, shiftEnd, task.Id,
                    effectiveBurden.ToString().ToLower());
            }
        }

        return null;
    }

    /// <summary>
    /// Determines if a shift is a night shift (starts between 22:00 and 06:00).
    /// </summary>
    private static bool IsNightShift(DateTime? shiftStart)
    {
        if (!shiftStart.HasValue) return false;
        var hour = shiftStart.Value.Hour;
        return hour >= 22 || hour < 6;
    }

    private record struct ResolvedSlot(
        DateTime ShiftStart, DateTime ShiftEnd, Guid? TaskTypeId, string? BurdenLevel);
}
