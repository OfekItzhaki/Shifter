using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Maintains per-person cumulative counters across solver runs.
/// Handles incremental updates on publish, full recomputation on rollback/presence edits,
/// period resets, and solver payload generation.
/// </summary>
public class CumulativeTracker : ICumulativeTracker
{
    private readonly AppDbContext _db;
    private readonly ILogger<CumulativeTracker> _logger;

    public CumulativeTracker(AppDbContext db, ILogger<CumulativeTracker> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task UpdateOnPublishAsync(Guid spaceId, Guid versionId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // 1. Load assignments from the published version
        var assignments = await _db.Assignments.AsNoTracking()
            .Where(a => a.ScheduleVersionId == versionId && a.SpaceId == spaceId)
            .ToListAsync(ct);

        if (assignments.Count == 0)
        {
            _logger.LogInformation("No assignments in version {VersionId}, skipping cumulative update.", versionId);
            return;
        }

        // 2. Load task slots and group tasks for resolving shift times and burden levels
        var taskSlots = await _db.TaskSlots.AsNoTracking()
            .Where(s => s.SpaceId == spaceId)
            .ToDictionaryAsync(s => s.Id, ct);

        var groupTasks = await _db.GroupTasks.AsNoTracking()
            .Where(t => t.SpaceId == spaceId)
            .ToListAsync(ct);

        var taskTypes = await _db.TaskTypes.AsNoTracking()
            .Where(t => t.SpaceId == spaceId)
            .ToDictionaryAsync(t => t.Id, ct);

        // 3. Determine group memberships for affected persons
        var personIds = assignments.Select(a => a.PersonId).Distinct().ToList();
        var memberships = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.SpaceId == spaceId && personIds.Contains(m.PersonId))
            .ToListAsync(ct);
        var membershipByPerson = memberships
            .GroupBy(m => m.PersonId)
            .ToDictionary(g => g.Key, g => g.First());

        // 4. Get active subscription periods per group
        var groupIds = memberships.Select(m => m.GroupId).Distinct().ToList();
        var periods = await _db.SubscriptionPeriods.AsNoTracking()
            .Where(sp => sp.SpaceId == spaceId && sp.Status == "active" && groupIds.Contains(sp.GroupId))
            .ToListAsync(ct);
        var periodByGroup = periods.ToDictionary(p => p.GroupId, p => p);

        // 5. Compute AssignmentCountsDelta per person
        var deltasByPerson = new Dictionary<Guid, AssignmentCountsDelta>();

        foreach (var assignment in assignments)
        {
            var resolved = ResolveSlotInfo(assignment.TaskSlotId, taskSlots, groupTasks, taskTypes);
            if (resolved == null)
            {
                _logger.LogWarning(
                    "Could not resolve slot info for assignment {AssignmentId}, slot {SlotId}",
                    assignment.Id, assignment.TaskSlotId);
                continue;
            }

            var (shiftStart, shiftEnd, taskTypeId, burdenLevel, taskName) = resolved.Value;

            int isHard = burdenLevel == "hard" ? 1 : 0;
            int isNight = IsNightShift(shiftStart) ? 1 : 0;
            decimal hours = (decimal)(shiftEnd - shiftStart).TotalHours;

            // Build task-type counts for this assignment
            var taskTypeCounts = new Dictionary<string, int>();
            if (!string.IsNullOrEmpty(taskName))
            {
                taskTypeCounts[taskName.ToLowerInvariant()] = 1;
            }

            var newDelta = new AssignmentCountsDelta(
                TotalAssignments: 1,
                HardTasks: isHard,
                NightMissions: isNight,
                TotalHours: hours,
                TaskTypeCounts: taskTypeCounts);

            if (deltasByPerson.TryGetValue(assignment.PersonId, out var existing))
            {
                // Merge task-type counts
                var mergedTaskTypeCounts = new Dictionary<string, int>(existing.TaskTypeCounts ?? new());
                if (newDelta.TaskTypeCounts != null)
                {
                    foreach (var (key, val) in newDelta.TaskTypeCounts)
                        mergedTaskTypeCounts[key] = mergedTaskTypeCounts.GetValueOrDefault(key) + val;
                }

                deltasByPerson[assignment.PersonId] = new AssignmentCountsDelta(
                    TotalAssignments: existing.TotalAssignments + newDelta.TotalAssignments,
                    HardTasks: existing.HardTasks + newDelta.HardTasks,
                    NightMissions: existing.NightMissions + newDelta.NightMissions,
                    TotalHours: existing.TotalHours + newDelta.TotalHours,
                    TaskTypeCounts: mergedTaskTypeCounts);
            }
            else
            {
                deltasByPerson[assignment.PersonId] = newDelta;
            }
        }

        // 6. Load or create cumulative records and apply deltas
        foreach (var (personId, delta) in deltasByPerson)
        {
            if (!membershipByPerson.TryGetValue(personId, out var membership))
            {
                _logger.LogWarning("Person {PersonId} has no group membership, skipping.", personId);
                continue;
            }

            if (!periodByGroup.TryGetValue(membership.GroupId, out var period))
            {
                _logger.LogWarning("No active period for group {GroupId}, skipping person {PersonId}.",
                    membership.GroupId, personId);
                continue;
            }

            var record = await _db.CumulativeRecords
                .FirstOrDefaultAsync(cr =>
                    cr.SpaceId == spaceId
                    && cr.GroupId == membership.GroupId
                    && cr.PersonId == personId
                    && cr.PeriodId == period.Id, ct);

            if (record == null)
            {
                record = CumulativeRecord.Create(spaceId, membership.GroupId, personId, period.Id);
                _db.CumulativeRecords.Add(record);
            }

            // Increment counters
            record.IncrementCounters(delta);

            // Recompute consecutive_hours_at_base from presence windows
            var presenceWindows = await _db.PresenceWindows.AsNoTracking()
                .Where(pw => pw.PersonId == personId && pw.SpaceId == spaceId)
                .OrderBy(pw => pw.StartsAt)
                .ToListAsync(ct);

            var (consecutiveHours, lastHomeLeaveEnd) = ComputeConsecutiveHoursAtBase(
                presenceWindows, period.StartsAt, now);

            record.UpdateConsecutiveHours(consecutiveHours, lastHomeLeaveEnd);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cumulative records updated for version {VersionId}: {PersonCount} persons affected.",
            versionId, deltasByPerson.Count);
    }

    /// <inheritdoc />
    public async Task RecomputeForPersonAsync(Guid spaceId, Guid personId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Find the person's group membership
        var membership = await _db.GroupMemberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.SpaceId == spaceId && m.PersonId == personId, ct);

        if (membership == null)
        {
            _logger.LogInformation("Person {PersonId} has no group membership, skipping recomputation.", personId);
            return;
        }

        // Find the active period for the group
        var period = await _db.SubscriptionPeriods.AsNoTracking()
            .FirstOrDefaultAsync(sp =>
                sp.SpaceId == spaceId && sp.GroupId == membership.GroupId && sp.Status == "active", ct);

        if (period == null)
        {
            _logger.LogWarning("No active period for group {GroupId}, skipping recomputation.", membership.GroupId);
            return;
        }

        // Load presence windows for this person
        var presenceWindows = await _db.PresenceWindows.AsNoTracking()
            .Where(pw => pw.PersonId == personId && pw.SpaceId == spaceId)
            .OrderBy(pw => pw.StartsAt)
            .ToListAsync(ct);

        // Compute consecutive hours
        var (consecutiveHours, lastHomeLeaveEnd) = ComputeConsecutiveHoursAtBase(
            presenceWindows, period.StartsAt, now);

        // Load or create the cumulative record
        var record = await _db.CumulativeRecords
            .FirstOrDefaultAsync(cr =>
                cr.SpaceId == spaceId
                && cr.GroupId == membership.GroupId
                && cr.PersonId == personId
                && cr.PeriodId == period.Id, ct);

        if (record == null)
        {
            record = CumulativeRecord.Create(spaceId, membership.GroupId, personId, period.Id);
            _db.CumulativeRecords.Add(record);
        }

        record.UpdateConsecutiveHours(consecutiveHours, lastHomeLeaveEnd);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Recomputed consecutive hours for person {PersonId}: {Hours}h",
            personId, consecutiveHours);
    }

    /// <inheritdoc />
    public async Task ResetPeriodCountersAsync(Guid spaceId, Guid groupId, Guid newPeriodId, CancellationToken ct)
    {
        // Load all cumulative records for the group (any period — we reset and reassign)
        var records = await _db.CumulativeRecords
            .Where(cr => cr.SpaceId == spaceId && cr.GroupId == groupId)
            .ToListAsync(ct);

        foreach (var record in records)
        {
            record.ResetPeriodCounters();

            // Update period_id to the new period via EF entry (private setter)
            var entry = _db.Entry(record);
            entry.Property(nameof(CumulativeRecord.PeriodId)).CurrentValue = newPeriodId;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Reset period counters for group {GroupId}: {Count} records updated to period {PeriodId}.",
            groupId, records.Count, newPeriodId);
    }

    /// <inheritdoc />
    public async Task<List<CumulativeTrackingDto>> GetForSolverPayloadAsync(
        Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Find the active period for the group (use tracking so we can create if needed)
        var period = await _db.SubscriptionPeriods
            .FirstOrDefaultAsync(sp =>
                sp.SpaceId == spaceId && sp.GroupId == groupId && sp.Status == "active", ct);

        if (period == null)
        {
            // Auto-create a period for groups that don't have one yet (trial, legacy, etc.)
            _logger.LogInformation(
                "No active period for group {GroupId} — auto-creating one for cumulative tracking.",
                groupId);

            period = SubscriptionPeriod.Create(spaceId, groupId);
            _db.SubscriptionPeriods.Add(period);
            await _db.SaveChangesAsync(ct);
        }

        // Load cumulative records for this group's current period
        var records = await _db.CumulativeRecords.AsNoTracking()
            .Where(cr => cr.SpaceId == spaceId && cr.GroupId == groupId && cr.PeriodId == period.Id)
            .ToListAsync(ct);

        // Load all group members to ensure new members get zero-valued DTOs
        var members = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.SpaceId == spaceId && m.GroupId == groupId)
            .Select(m => m.PersonId)
            .ToListAsync(ct);

        var recordByPerson = records.ToDictionary(r => r.PersonId);
        var result = new List<CumulativeTrackingDto>(members.Count);

        foreach (var personId in members)
        {
            if (recordByPerson.TryGetValue(personId, out var record))
            {
                var daysSinceLastLeave = record.LastHomeLeaveEnd.HasValue
                    ? (int)(now - record.LastHomeLeaveEnd.Value).TotalDays
                    : (int)(now - period.StartsAt).TotalDays;

                result.Add(new CumulativeTrackingDto(
                    PersonId: personId.ToString(),
                    ConsecutiveHoursAtBase: (double)record.ConsecutiveHoursAtBase,
                    LastHomeLeaveEnd: record.LastHomeLeaveEnd?.ToString("o"),
                    TotalAssignmentsInPeriod: record.TotalAssignmentsPeriod,
                    HardTasksInPeriod: record.HardTasksPeriod,
                    DaysSinceLastLeave: daysSinceLastLeave));
            }
            else
            {
                // New member with no cumulative record — return zero-valued DTO
                var daysSincePeriodStart = (int)(now - period.StartsAt).TotalDays;

                result.Add(new CumulativeTrackingDto(
                    PersonId: personId.ToString(),
                    ConsecutiveHoursAtBase: 0,
                    LastHomeLeaveEnd: null,
                    TotalAssignmentsInPeriod: 0,
                    HardTasksInPeriod: 0,
                    DaysSinceLastLeave: daysSincePeriodStart));
            }
        }

        return result;
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Computes consecutive hours at base by summing contiguous FreeInBase time
    /// since the most recent AtHome window end or period start, whichever is later.
    /// Also returns the last home-leave end timestamp.
    /// </summary>
    internal static (decimal ConsecutiveHours, DateTime? LastHomeLeaveEnd) ComputeConsecutiveHoursAtBase(
        List<PresenceWindow> presenceWindows, DateTime periodStart, DateTime now)
    {
        // Find the most recent AtHome window that has ended
        var lastAtHome = presenceWindows
            .Where(pw => pw.State == PresenceState.AtHome && pw.EndsAt <= now)
            .OrderByDescending(pw => pw.EndsAt)
            .FirstOrDefault();

        DateTime? lastHomeLeaveEnd = lastAtHome?.EndsAt;

        // The reference point is the later of: last AtHome end or period start
        var referencePoint = lastHomeLeaveEnd.HasValue && lastHomeLeaveEnd.Value > periodStart
            ? lastHomeLeaveEnd.Value
            : periodStart;

        // Sum all FreeInBase hours from the reference point forward
        var freeInBaseWindows = presenceWindows
            .Where(pw => pw.State == PresenceState.FreeInBase && pw.EndsAt > referencePoint)
            .OrderBy(pw => pw.StartsAt)
            .ToList();

        decimal totalHours = 0;

        foreach (var window in freeInBaseWindows)
        {
            // Clamp window start to reference point
            var effectiveStart = window.StartsAt < referencePoint ? referencePoint : window.StartsAt;
            var effectiveEnd = window.EndsAt > now ? now : window.EndsAt;

            if (effectiveEnd > effectiveStart)
            {
                totalHours += (decimal)(effectiveEnd - effectiveStart).TotalHours;
            }
        }

        // If no FreeInBase windows exist after reference point and no presence data at all,
        // assume at base since reference point
        if (freeInBaseWindows.Count == 0 && presenceWindows.Count == 0)
        {
            totalHours = (decimal)(now - referencePoint).TotalHours;
        }

        return (Math.Round(totalHours, 2), lastHomeLeaveEnd);
    }

    /// <summary>
    /// Resolves a task slot ID to its shift start/end times, task type, burden level, and task name.
    /// </summary>
    private static ResolvedSlotInfo? ResolveSlotInfo(
        Guid slotId,
        Dictionary<Guid, Domain.Tasks.TaskSlot> taskSlots,
        List<Domain.Tasks.GroupTask> groupTasks,
        Dictionary<Guid, Domain.Tasks.TaskType> taskTypes)
    {
        // Try direct task slot reference first
        if (taskSlots.TryGetValue(slotId, out var slot))
        {
            string? burdenLevel = null;
            string? taskName = null;
            if (taskTypes.TryGetValue(slot.TaskTypeId, out var tt))
            {
                burdenLevel = tt.BurdenLevel.ToString().ToLower();
                taskName = tt.Name;
            }

            return new ResolvedSlotInfo(slot.StartsAt, slot.EndsAt, slot.TaskTypeId, burdenLevel, taskName);
        }

        // Try derived slot from GroupTask
        return ResolveGroupTaskSlot(slotId, groupTasks);
    }

    /// <summary>
    /// Attempts to resolve a derived task slot ID back to its GroupTask and shift times.
    /// Uses the DeriveShiftGuid algorithm in reverse by trying each GroupTask.
    /// </summary>
    private static ResolvedSlotInfo? ResolveGroupTaskSlot(
        Guid slotId, IEnumerable<Domain.Tasks.GroupTask> groupTasks)
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
                return new ResolvedSlotInfo(
                    shiftStart, shiftEnd, task.Id,
                    task.BurdenLevel.ToString().ToLower(),
                    task.Name);
            }
        }

        return null;
    }

    /// <summary>
    /// Determines if a shift is a night shift (starts between 22:00 and 06:00).
    /// </summary>
    private static bool IsNightShift(DateTime shiftStart)
    {
        var hour = shiftStart.Hour;
        return hour >= 22 || hour < 6;
    }

    private record struct ResolvedSlotInfo(
        DateTime ShiftStart, DateTime ShiftEnd, Guid? TaskTypeId, string? BurdenLevel, string? TaskName);
}
