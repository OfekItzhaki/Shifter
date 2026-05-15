using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Scheduling.Commands;

/// <summary>
/// One-time backfill command that generates daily snapshots from existing published schedule versions.
/// For each published version, joins with assignments and resolves slot start/end times
/// (from task_slots or group_tasks via GUID derivation) to determine which calendar days
/// each assignment covers. Creates DailySnapshot rows for each person × day × slot combination.
/// Only creates snapshots for the most recent published version covering each date.
/// Idempotent — uses ON CONFLICT DO NOTHING pattern (skips existing rows).
/// </summary>
public record BackfillDailySnapshotsCommand : IRequest<BackfillDailySnapshotsResult>;

public record BackfillDailySnapshotsResult(int Created, int Skipped);

public class BackfillDailySnapshotsCommandHandler
    : IRequestHandler<BackfillDailySnapshotsCommand, BackfillDailySnapshotsResult>
{
    private readonly AppDbContext _db;
    private readonly ILogger<BackfillDailySnapshotsCommandHandler> _logger;

    public BackfillDailySnapshotsCommandHandler(
        AppDbContext db,
        ILogger<BackfillDailySnapshotsCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BackfillDailySnapshotsResult> Handle(
        BackfillDailySnapshotsCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Starting daily snapshots backfill...");

        // Get all published schedule versions ordered by PublishedAt descending
        // so we process the most recent first and skip older versions for the same date
        var publishedVersions = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.Status == ScheduleVersionStatus.Published && v.PublishedAt != null)
            .OrderByDescending(v => v.PublishedAt)
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} published versions to process", publishedVersions.Count);

        // Load all task slots (for resolving start/end times)
        var taskSlots = await _db.TaskSlots.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, ct);

        // Load all group tasks (for resolving derived slot times)
        var groupTasks = await _db.GroupTasks.AsNoTracking()
            .ToDictionaryAsync(t => t.Id, ct);

        // Load all task types (for burden level)
        var taskTypes = await _db.TaskTypes.AsNoTracking()
            .ToDictionaryAsync(t => t.Id, ct);

        // Load subscription periods per group (active ones)
        var periods = await _db.SubscriptionPeriods.AsNoTracking()
            .Where(sp => sp.Status == "active")
            .ToListAsync(ct);
        var periodByGroup = periods.ToDictionary(p => p.GroupId, p => p);

        // Load group memberships to determine which group a person belongs to
        var memberships = await _db.GroupMemberships.AsNoTracking()
            .ToListAsync(ct);
        var membershipsByPerson = memberships
            .GroupBy(m => m.PersonId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Track which (person, date, slot) combinations already have snapshots
        // to implement "most recent version wins" logic
        var coveredKeys = new HashSet<string>();

        // Also load existing snapshots to skip duplicates (idempotent)
        var existingSnapshots = await _db.DailySnapshots.AsNoTracking()
            .Select(ds => new { ds.SpaceId, ds.GroupId, ds.PersonId, ds.SnapshotDate, ds.SlotId })
            .ToListAsync(ct);
        var existingKeys = existingSnapshots
            .Select(e => $"{e.SpaceId}|{e.GroupId}|{e.PersonId}|{e.SnapshotDate}|{e.SlotId}")
            .ToHashSet();

        int created = 0;
        int skipped = 0;

        foreach (var version in publishedVersions)
        {
            // Load assignments for this version
            var assignments = await _db.Assignments.AsNoTracking()
                .Where(a => a.ScheduleVersionId == version.Id)
                .ToListAsync(ct);

            _logger.LogInformation(
                "Processing version {VersionId} with {AssignmentCount} assignments",
                version.Id, assignments.Count);

            foreach (var assignment in assignments)
            {
                // Resolve slot start/end times
                DateTime? shiftStart = null;
                DateTime? shiftEnd = null;
                Guid? taskTypeId = null;
                string? burdenLevel = null;

                if (taskSlots.TryGetValue(assignment.TaskSlotId, out var slot))
                {
                    // Direct task slot reference
                    shiftStart = slot.StartsAt;
                    shiftEnd = slot.EndsAt;
                    taskTypeId = slot.TaskTypeId;
                    if (taskTypes.TryGetValue(slot.TaskTypeId, out var tt))
                        burdenLevel = tt.BurdenLevel.ToString().ToLower();
                }
                else
                {
                    // Derived slot from GroupTask — reverse the DeriveShiftGuid logic
                    // Try to find the GroupTask that generated this slot ID
                    var resolved = ResolveGroupTaskSlot(assignment.TaskSlotId, groupTasks.Values);
                    if (resolved != null)
                    {
                        shiftStart = resolved.Value.ShiftStart;
                        shiftEnd = resolved.Value.ShiftEnd;
                        taskTypeId = resolved.Value.TaskId;
                        burdenLevel = resolved.Value.BurdenLevel;
                    }
                    else
                    {
                        // Cannot resolve slot — skip this assignment
                        skipped++;
                        continue;
                    }
                }

                if (shiftStart == null || shiftEnd == null)
                {
                    skipped++;
                    continue;
                }

                // Determine which calendar days this assignment covers
                var startDate = DateOnly.FromDateTime(shiftStart.Value);
                var endDate = DateOnly.FromDateTime(shiftEnd.Value);
                // If shift ends exactly at midnight, it doesn't cover the next day
                if (shiftEnd.Value.TimeOfDay == TimeSpan.Zero && shiftEnd.Value > shiftStart.Value)
                    endDate = endDate.AddDays(-1);

                // Determine group for this person
                var personGroups = membershipsByPerson.GetValueOrDefault(assignment.PersonId);
                if (personGroups == null || personGroups.Count == 0)
                {
                    skipped++;
                    continue;
                }

                // Use the first group membership in the same space
                var membership = personGroups.FirstOrDefault(m => m.SpaceId == assignment.SpaceId);
                if (membership == null)
                {
                    skipped++;
                    continue;
                }

                var groupId = membership.GroupId;

                // Get the subscription period for this group
                if (!periodByGroup.TryGetValue(groupId, out var period))
                {
                    skipped++;
                    continue;
                }

                // Create snapshots for each day covered
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    var coverKey = $"{assignment.SpaceId}|{groupId}|{assignment.PersonId}|{date}|{assignment.TaskSlotId}";

                    // Skip if a more recent version already covers this combination
                    if (coveredKeys.Contains(coverKey))
                    {
                        skipped++;
                        continue;
                    }

                    // Skip if already exists in DB (idempotent)
                    if (existingKeys.Contains(coverKey))
                    {
                        coveredKeys.Add(coverKey);
                        skipped++;
                        continue;
                    }

                    coveredKeys.Add(coverKey);

                    var snapshot = DailySnapshot.Create(
                        spaceId: assignment.SpaceId,
                        groupId: groupId,
                        personId: assignment.PersonId,
                        periodId: period.Id,
                        snapshotDate: date,
                        taskTypeId: taskTypeId,
                        slotId: assignment.TaskSlotId,
                        shiftStart: shiftStart,
                        shiftEnd: shiftEnd,
                        burdenLevel: burdenLevel,
                        versionId: version.Id);

                    _db.DailySnapshots.Add(snapshot);
                    created++;
                }
            }

            // Save in batches per version to avoid memory pressure
            if (created > 0)
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Saved batch for version {VersionId}. Running total: {Created} created",
                    version.Id, created);
            }
        }

        _logger.LogInformation(
            "Daily snapshots backfill complete. Created: {Created}, Skipped: {Skipped}",
            created, skipped);

        return new BackfillDailySnapshotsResult(created, skipped);
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

            // Try to find the shift index that produces this slotId
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
                return new ResolvedSlot(
                    shiftStart, shiftEnd, task.Id,
                    task.BurdenLevel.ToString().ToLower());
            }
        }

        return null;
    }

    private record struct ResolvedSlot(
        DateTime ShiftStart, DateTime ShiftEnd, Guid TaskId, string BurdenLevel);
}
