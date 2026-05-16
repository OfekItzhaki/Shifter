using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Scheduling.Commands;

/// <summary>
/// One-time backfill command that computes initial cumulative records from existing daily snapshots.
/// For each person in each group that has a subscription period:
///   - Counts assignments from daily_snapshots (total, hard, kitchen, night, disliked/hated)
///     for 7d, 14d, 30d, 90d, and all-time-within-period windows
///   - Computes consecutive_hours_at_base from presence_windows
///     (sum contiguous FreeInBase time since most recent AtHome end or period start)
///   - Finds last_home_leave_end from most recent AtHome presence window
/// Idempotent — uses upsert pattern (updates existing records, creates new ones).
/// </summary>
public record BackfillCumulativeRecordsCommand : IRequest<BackfillCumulativeRecordsResult>;

public record BackfillCumulativeRecordsResult(int Created, int Updated, int Skipped);

public class BackfillCumulativeRecordsCommandHandler
    : IRequestHandler<BackfillCumulativeRecordsCommand, BackfillCumulativeRecordsResult>
{
    private readonly AppDbContext _db;
    private readonly ILogger<BackfillCumulativeRecordsCommandHandler> _logger;

    public BackfillCumulativeRecordsCommandHandler(
        AppDbContext db,
        ILogger<BackfillCumulativeRecordsCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BackfillCumulativeRecordsResult> Handle(
        BackfillCumulativeRecordsCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Starting cumulative records backfill...");

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var cutoff7d = today.AddDays(-7);
        var cutoff14d = today.AddDays(-14);
        var cutoff30d = today.AddDays(-30);
        var cutoff90d = today.AddDays(-90);

        // Load all active subscription periods
        var periods = await _db.SubscriptionPeriods.AsNoTracking()
            .Where(sp => sp.Status == "active")
            .ToListAsync(ct);

        if (periods.Count == 0)
        {
            _logger.LogWarning("No active subscription periods found. Skipping backfill.");
            return new BackfillCumulativeRecordsResult(0, 0, 0);
        }

        var periodByGroup = periods.ToDictionary(p => p.GroupId, p => p);

        // Load group memberships for groups that have periods
        var groupIds = periodByGroup.Keys.ToHashSet();
        var memberships = await _db.GroupMemberships.AsNoTracking()
            .Where(m => groupIds.Contains(m.GroupId))
            .ToListAsync(ct);

        // Load all daily snapshots for relevant groups
        var allSnapshots = await _db.DailySnapshots.AsNoTracking()
            .Where(ds => groupIds.Contains(ds.GroupId))
            .ToListAsync(ct);

        // Load all presence windows for computing consecutive hours
        var personIds = memberships.Select(m => m.PersonId).Distinct().ToHashSet();
        var presenceWindows = await _db.PresenceWindows.AsNoTracking()
            .Where(pw => personIds.Contains(pw.PersonId))
            .OrderBy(pw => pw.StartsAt)
            .ToListAsync(ct);

        // Load existing cumulative records for upsert
        var existingRecords = await _db.CumulativeRecords
            .Where(cr => groupIds.Contains(cr.GroupId))
            .ToListAsync(ct);
        var existingRecordMap = existingRecords
            .ToDictionary(cr => $"{cr.GroupId}|{cr.PersonId}|{cr.PeriodId}");

        int created = 0;
        int updated = 0;
        int skipped = 0;

        // Group memberships by group
        var membershipsByGroup = memberships.GroupBy(m => m.GroupId);

        foreach (var groupMemberships in membershipsByGroup)
        {
            var groupId = groupMemberships.Key;
            if (!periodByGroup.TryGetValue(groupId, out var period))
            {
                skipped += groupMemberships.Count();
                continue;
            }

            var periodStartDate = DateOnly.FromDateTime(period.StartsAt);
            var groupSnapshots = allSnapshots.Where(s => s.GroupId == groupId).ToList();

            foreach (var membership in groupMemberships)
            {
                var personId = membership.PersonId;
                var spaceId = membership.SpaceId;

                // Get snapshots for this person within the period
                var personSnapshots = groupSnapshots
                    .Where(s => s.PersonId == personId && s.SnapshotDate >= periodStartDate)
                    .ToList();

                // Compute assignment counts for each time window
                var total7d = personSnapshots.Count(s => s.SnapshotDate >= cutoff7d);
                var total14d = personSnapshots.Count(s => s.SnapshotDate >= cutoff14d);
                var total30d = personSnapshots.Count(s => s.SnapshotDate >= cutoff30d);
                var total90d = personSnapshots.Count(s => s.SnapshotDate >= cutoff90d);
                var totalPeriod = personSnapshots.Count;

                var hard7d = personSnapshots.Count(s => s.SnapshotDate >= cutoff7d && s.BurdenLevel == "hard");
                var hard14d = personSnapshots.Count(s => s.SnapshotDate >= cutoff14d && s.BurdenLevel == "hard");
                var hard30d = personSnapshots.Count(s => s.SnapshotDate >= cutoff30d && s.BurdenLevel == "hard");
                var hard90d = personSnapshots.Count(s => s.SnapshotDate >= cutoff90d && s.BurdenLevel == "hard");
                var hardPeriod = personSnapshots.Count(s => s.BurdenLevel == "hard");

                // Kitchen detection: replaced by generic task-type counting (handled elsewhere)

                // Night detection: shift starts between 22:00 and 06:00
                bool IsNight(DateTime? shiftStart) =>
                    shiftStart.HasValue && (shiftStart.Value.Hour >= 22 || shiftStart.Value.Hour < 6);

                var night7d = personSnapshots.Count(s => s.SnapshotDate >= cutoff7d && IsNight(s.ShiftStart));
                var night14d = personSnapshots.Count(s => s.SnapshotDate >= cutoff14d && IsNight(s.ShiftStart));
                var night30d = personSnapshots.Count(s => s.SnapshotDate >= cutoff30d && IsNight(s.ShiftStart));
                var night90d = personSnapshots.Count(s => s.SnapshotDate >= cutoff90d && IsNight(s.ShiftStart));
                var nightPeriod = personSnapshots.Count(s => IsNight(s.ShiftStart));

                // Compute total hours assigned in period
                var totalHoursPeriod = personSnapshots
                    .Where(s => s.ShiftStart.HasValue && s.ShiftEnd.HasValue)
                    .Sum(s => (decimal)(s.ShiftEnd!.Value - s.ShiftStart!.Value).TotalHours);

                // Compute consecutive_hours_at_base from presence windows
                var personPresence = presenceWindows
                    .Where(pw => pw.PersonId == personId && pw.SpaceId == spaceId)
                    .OrderBy(pw => pw.StartsAt)
                    .ToList();

                var (consecutiveHours, lastHomeLeaveEnd) = ComputeConsecutiveHoursAtBase(
                    personPresence, period.StartsAt, now);

                // Upsert cumulative record
                var key = $"{groupId}|{personId}|{period.Id}";
                if (existingRecordMap.TryGetValue(key, out var existing))
                {
                    // Update existing record via EF entry
                    var entry = _db.Entry(existing);
                    entry.Property(nameof(CumulativeRecord.TotalAssignments7d)).CurrentValue = total7d;
                    entry.Property(nameof(CumulativeRecord.TotalAssignments14d)).CurrentValue = total14d;
                    entry.Property(nameof(CumulativeRecord.TotalAssignments30d)).CurrentValue = total30d;
                    entry.Property(nameof(CumulativeRecord.TotalAssignments90d)).CurrentValue = total90d;
                    entry.Property(nameof(CumulativeRecord.TotalAssignmentsPeriod)).CurrentValue = totalPeriod;
                    entry.Property(nameof(CumulativeRecord.HardTasks7d)).CurrentValue = hard7d;
                    entry.Property(nameof(CumulativeRecord.HardTasks14d)).CurrentValue = hard14d;
                    entry.Property(nameof(CumulativeRecord.HardTasks30d)).CurrentValue = hard30d;
                    entry.Property(nameof(CumulativeRecord.HardTasks90d)).CurrentValue = hard90d;
                    entry.Property(nameof(CumulativeRecord.HardTasksPeriod)).CurrentValue = hardPeriod;
                    entry.Property(nameof(CumulativeRecord.NightMissions7d)).CurrentValue = night7d;
                    entry.Property(nameof(CumulativeRecord.NightMissions14d)).CurrentValue = night14d;
                    entry.Property(nameof(CumulativeRecord.NightMissions30d)).CurrentValue = night30d;
                    entry.Property(nameof(CumulativeRecord.NightMissions90d)).CurrentValue = night90d;
                    entry.Property(nameof(CumulativeRecord.NightMissionsPeriod)).CurrentValue = nightPeriod;
                    entry.Property(nameof(CumulativeRecord.TotalHoursAssignedPeriod)).CurrentValue = totalHoursPeriod;
                    entry.Property(nameof(CumulativeRecord.ConsecutiveHoursAtBase)).CurrentValue = consecutiveHours;
                    entry.Property(nameof(CumulativeRecord.LastHomeLeaveEnd)).CurrentValue = lastHomeLeaveEnd;
                    entry.Property(nameof(CumulativeRecord.UpdatedAt)).CurrentValue = now;
                    updated++;
                }
                else
                {
                    // Create new record — use the domain factory and override via EF entry
                    CreateCumulativeRecord(
                        spaceId, groupId, personId, period.Id,
                        total7d, total14d, total30d, total90d, totalPeriod,
                        hard7d, hard14d, hard30d, hard90d, hardPeriod,
                        night7d, night14d, night30d, night90d, nightPeriod,
                        totalHoursPeriod, consecutiveHours, lastHomeLeaveEnd);
                    created++;
                }
            }

            // Save per group to manage memory
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Processed group {GroupId}. Running totals — Created: {Created}, Updated: {Updated}",
                groupId, created, updated);
        }

        _logger.LogInformation(
            "Cumulative records backfill complete. Created: {Created}, Updated: {Updated}, Skipped: {Skipped}",
            created, updated, skipped);

        return new BackfillCumulativeRecordsResult(created, updated, skipped);
    }

    /// <summary>
    /// Computes consecutive hours at base by summing contiguous FreeInBase time
    /// since the most recent AtHome window end or period start, whichever is later.
    /// Also returns the last home-leave end timestamp.
    /// </summary>
    private static (decimal ConsecutiveHours, DateTime? LastHomeLeaveEnd) ComputeConsecutiveHoursAtBase(
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
        // Only count contiguous FreeInBase time (no gaps from AtHome or OnMission)
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

        // If no FreeInBase windows exist after reference point, check if the person
        // has been at base since the reference point (implicit FreeInBase)
        if (freeInBaseWindows.Count == 0 && presenceWindows.Count == 0)
        {
            // No presence data at all — assume at base since period start
            totalHours = (decimal)(now - referencePoint).TotalHours;
        }

        return (Math.Round(totalHours, 2), lastHomeLeaveEnd);
    }

    /// <summary>
    /// Creates a CumulativeRecord with all counters set via EF property access.
    /// Since the domain entity uses private setters, we create it via factory and override values.
    /// </summary>
    private CumulativeRecord CreateCumulativeRecord(
        Guid spaceId, Guid groupId, Guid personId, Guid periodId,
        int total7d, int total14d, int total30d, int total90d, int totalPeriod,
        int hard7d, int hard14d, int hard30d, int hard90d, int hardPeriod,
        int night7d, int night14d, int night30d, int night90d, int nightPeriod,
        decimal totalHoursPeriod, decimal consecutiveHours, DateTime? lastHomeLeaveEnd)
    {
        var record = CumulativeRecord.Create(spaceId, groupId, personId, periodId);
        _db.CumulativeRecords.Add(record);
        var entry = _db.Entry(record);
        entry.Property(nameof(CumulativeRecord.TotalAssignments7d)).CurrentValue = total7d;
        entry.Property(nameof(CumulativeRecord.TotalAssignments14d)).CurrentValue = total14d;
        entry.Property(nameof(CumulativeRecord.TotalAssignments30d)).CurrentValue = total30d;
        entry.Property(nameof(CumulativeRecord.TotalAssignments90d)).CurrentValue = total90d;
        entry.Property(nameof(CumulativeRecord.TotalAssignmentsPeriod)).CurrentValue = totalPeriod;
        entry.Property(nameof(CumulativeRecord.HardTasks7d)).CurrentValue = hard7d;
        entry.Property(nameof(CumulativeRecord.HardTasks14d)).CurrentValue = hard14d;
        entry.Property(nameof(CumulativeRecord.HardTasks30d)).CurrentValue = hard30d;
        entry.Property(nameof(CumulativeRecord.HardTasks90d)).CurrentValue = hard90d;
        entry.Property(nameof(CumulativeRecord.HardTasksPeriod)).CurrentValue = hardPeriod;
        entry.Property(nameof(CumulativeRecord.NightMissions7d)).CurrentValue = night7d;
        entry.Property(nameof(CumulativeRecord.NightMissions14d)).CurrentValue = night14d;
        entry.Property(nameof(CumulativeRecord.NightMissions30d)).CurrentValue = night30d;
        entry.Property(nameof(CumulativeRecord.NightMissions90d)).CurrentValue = night90d;
        entry.Property(nameof(CumulativeRecord.NightMissionsPeriod)).CurrentValue = nightPeriod;
        entry.Property(nameof(CumulativeRecord.TotalHoursAssignedPeriod)).CurrentValue = totalHoursPeriod;
        entry.Property(nameof(CumulativeRecord.ConsecutiveHoursAtBase)).CurrentValue = consecutiveHours;
        entry.Property(nameof(CumulativeRecord.LastHomeLeaveEnd)).CurrentValue = lastHomeLeaveEnd;
        entry.Property(nameof(CumulativeRecord.UpdatedAt)).CurrentValue = DateTime.UtcNow;
        return record;
    }
}
