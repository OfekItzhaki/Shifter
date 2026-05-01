using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Constraints;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobuler.Infrastructure.Scheduling;

public class SolverPayloadNormalizer : ISolverPayloadNormalizer
{
    private readonly AppDbContext _db;
    private readonly ILogger<SolverPayloadNormalizer> _logger;

    // Default stability weights — sent in every payload per spec Section 5.4
    private static readonly StabilityWeightsDto DefaultWeights = new(10.0, 3.0, 1.0);

    public SolverPayloadNormalizer(AppDbContext db, ILogger<SolverPayloadNormalizer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SolverInputDto> BuildAsync(
        Guid spaceId, Guid runId, string triggerMode,
        Guid? baselineVersionId, CancellationToken ct = default)
    {
        // Set PostgreSQL session variable so RLS policies allow queries on this space
        // Skip when using an in-memory provider (e.g. unit/integration tests).
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE)",
                spaceId.ToString());
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizonStart = today;

        // Use the maximum solver horizon across all groups in this space,
        // capped at 7 days to keep the CP-SAT model tractable.
        var maxHorizon = await _db.Groups.AsNoTracking()
            .Where(g => g.SpaceId == spaceId && g.DeletedAt == null)
            .MaxAsync(g => (int?)g.SolverHorizonDays, ct) ?? 7;
        maxHorizon = Math.Min(maxHorizon, 7); // hard cap

        var horizonEnd = today.AddDays(maxHorizon - 1); // inclusive

        // ── People eligibility ────────────────────────────────────────────────
        var people = await _db.People.AsNoTracking()
            .Where(p => p.SpaceId == spaceId && p.IsActive)
            .ToListAsync(ct);

        var roleAssignments = await _db.PersonRoleAssignments.AsNoTracking()
            .Where(r => r.SpaceId == spaceId)
            .ToListAsync(ct);

        var qualifications = await _db.PersonQualifications.AsNoTracking()
            .Where(q => q.SpaceId == spaceId && q.IsActive)
            .ToListAsync(ct);

        var groupMemberships = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.SpaceId == spaceId)
            .ToListAsync(ct);

        var peopleDto = people.Select(p => new PersonEligibilityDto(
            p.Id.ToString(),
            roleAssignments.Where(r => r.PersonId == p.Id).Select(r => r.RoleId.ToString()).ToList(),
            qualifications.Where(q => q.PersonId == p.Id).Select(q => q.Qualification).ToList(),
            groupMemberships.Where(m => m.PersonId == p.Id).Select(m => m.GroupId.ToString()).ToList()
        )).ToList();

        // ── Availability windows ──────────────────────────────────────────────
        var horizonStartDt = horizonStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var horizonEndDt   = horizonEnd.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var availability = await _db.AvailabilityWindows.AsNoTracking()
            .Where(a => a.SpaceId == spaceId
                && a.EndsAt >= horizonStartDt
                && a.StartsAt <= horizonEndDt)
            .ToListAsync(ct);

        var availabilityDto = availability.Select(a => new AvailabilityWindowDto(
            a.PersonId.ToString(),
            a.StartsAt.ToString("o"),
            a.EndsAt.ToString("o"))).ToList();

        // ── Presence windows ──────────────────────────────────────────────────
        var presence = await _db.PresenceWindows.AsNoTracking()
            .Where(p => p.SpaceId == spaceId
                && p.EndsAt >= horizonStartDt
                && p.StartsAt <= horizonEndDt)
            .ToListAsync(ct);

        var presenceDto = presence.Select(p => new PresenceWindowDto(
            p.PersonId.ToString(),
            p.State.ToString().ToSnakeCase(),
            p.StartsAt.ToString("o"),
            p.EndsAt.ToString("o"))).ToList();

        // ── Task slots ────────────────────────────────────────────────────────
        var slots = await _db.TaskSlots.AsNoTracking()
            .Where(s => s.SpaceId == spaceId
                && s.Status == TaskSlotStatus.Active
                && s.EndsAt >= horizonStartDt
                && s.StartsAt <= horizonEndDt)
            .ToListAsync(ct);

        var taskTypes = await _db.TaskTypes.AsNoTracking()
            .Where(t => t.SpaceId == spaceId)
            .ToDictionaryAsync(t => t.Id, ct);

        var slotsDto = slots.Select(s =>
        {
            taskTypes.TryGetValue(s.TaskTypeId, out var tt);
            return new TaskSlotDto(
                s.Id.ToString(),
                s.TaskTypeId.ToString(),
                tt?.Name ?? "Unknown",
                (tt?.BurdenLevel ?? Domain.Tasks.TaskBurdenLevel.Neutral).ToString().ToLower(),
                s.StartsAt.ToString("o"),
                s.EndsAt.ToString("o"),
                s.RequiredHeadcount,
                s.Priority,
                s.RequiredRoleIds.Select(id => id.ToString()).ToList(),
                s.RequiredQualificationIds.Select(id => id.ToString()).ToList(),
                tt?.AllowsOverlap ?? false);
        }).ToList();

        // ── Group tasks → shift slots ─────────────────────────────────────────
        // Each GroupTask defines a window + shift duration. Expand into individual
        // shift slots so the solver assigns people to specific time windows.
        // Use the full solver horizon (up to 7 days) so 24/7 tasks are fully covered.
        var groupTasks = await _db.GroupTasks.AsNoTracking()
            .Where(t => t.SpaceId == spaceId
                && t.IsActive
                && t.StartsAt <= horizonEndDt)  // task must start before the horizon ends
            .ToListAsync(ct);

        foreach (var task in groupTasks)
        {
            var shiftDuration = TimeSpan.FromMinutes(task.ShiftDurationMinutes);
            if (shiftDuration.TotalMinutes < 1) continue;

            // If the task has no meaningful end date (MinValue or past), treat it as ongoing
            // and schedule it through the full horizon
            var effectiveEnd = task.EndsAt <= horizonStartDt
                ? horizonEndDt
                : task.EndsAt;

            // Clamp to the full solver horizon
            var windowStart = task.StartsAt < horizonStartDt ? horizonStartDt : task.StartsAt;
            var windowEnd   = effectiveEnd > horizonEndDt ? horizonEndDt : effectiveEnd;

            // Apply daily time window if configured (e.g. task only runs 08:00–22:00 each day)
            var dailyStart = task.DailyStartTime;
            var dailyEnd   = task.DailyEndTime;

            // Safety cap: never generate more than 336 shifts per task (7 days × 48 half-hour slots)
            const int MaxShiftsPerTask = 336;
            var shiftStart = windowStart;
            var shiftIndex = 0;
            while (shiftStart + shiftDuration <= windowEnd && shiftIndex < MaxShiftsPerTask)
            {
                var shiftEnd = shiftStart + shiftDuration;

                // If a daily time window is set, skip shifts that fall outside it
                if (dailyStart.HasValue && dailyEnd.HasValue)
                {
                    var shiftStartTime = TimeOnly.FromDateTime(shiftStart);
                    var shiftEndTime   = TimeOnly.FromDateTime(shiftEnd);
                    // Skip if shift starts before daily window or ends after it
                    if (shiftStartTime < dailyStart.Value || shiftEndTime > dailyEnd.Value)
                    {
                        // Advance to the next day's window start if we've passed today's window
                        if (shiftStartTime >= dailyEnd.Value)
                        {
                            var nextDay = shiftStart.Date.AddDays(1);
                            shiftStart = nextDay + dailyStart.Value.ToTimeSpan();
                        }
                        else
                        {
                            shiftStart = shiftEnd;
                        }
                        shiftIndex++;
                        continue;
                    }
                }

                var shiftGuid = DeriveShiftGuid(task.Id, shiftIndex);
                var slotId = shiftGuid.ToString();

                slotsDto.Add(new TaskSlotDto(
                    slotId,
                    task.Id.ToString(),
                    task.Name,
                    task.BurdenLevel.ToString().ToLower(),
                    shiftStart.ToString("o"),
                    shiftEnd.ToString("o"),
                    task.RequiredHeadcount,
                    5,
                    [],
                    [],
                    task.AllowsOverlap,
                    task.AllowsDoubleShift));

                shiftStart = shiftEnd;
                shiftIndex++;
            }
        }

        // ── Constraints ───────────────────────────────────────────────────────
        // Filter by effective date window: exclude constraints that have expired
        // before the horizon starts, or haven't started yet by the horizon end.
        var constraints = await _db.ConstraintRules.AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.IsActive
                && (c.EffectiveUntil == null || c.EffectiveUntil >= horizonStart)
                && (c.EffectiveFrom == null || c.EffectiveFrom <= horizonEnd))
            .ToListAsync(ct);

        var hardConstraints = constraints
            .Where(c => c.Severity == ConstraintSeverity.Hard)
            .Select(c => new HardConstraintDto(
                c.Id.ToString(), c.RuleType,
                c.ScopeType.ToString().ToLower(),
                c.ScopeId?.ToString(),
                DeserializePayload(c.RulePayloadJson)))
            .ToList();

        var softConstraints = constraints
            .Where(c => c.Severity == ConstraintSeverity.Soft)
            .Select(c => new SoftConstraintDto(
                c.Id.ToString(), c.RuleType,
                c.ScopeType.ToString().ToLower(),
                c.ScopeId?.ToString(),
                1.0,
                DeserializePayload(c.RulePayloadJson)))
            .ToList();

        // Emergency constraints bypass all hard/soft constraints in the solver.
        var emergencyConstraints = constraints
            .Where(c => c.Severity == ConstraintSeverity.Emergency)
            .Select(c => new HardConstraintDto(
                c.Id.ToString(), c.RuleType,
                c.ScopeType.ToString().ToLower(),
                c.ScopeId?.ToString(),
                DeserializePayload(c.RulePayloadJson)))
            .ToList();

        // ── Baseline assignments ──────────────────────────────────────────────
        var baselineAssignments = new List<BaselineAssignmentDto>();
        var lockedSlotIds = new List<string>();
        if (baselineVersionId.HasValue)
        {
            var baselineRows = await _db.Assignments.AsNoTracking()
                .Where(a => a.ScheduleVersionId == baselineVersionId.Value && a.SpaceId == spaceId)
                .ToListAsync(ct);

            baselineAssignments = baselineRows
                .Select(a => new BaselineAssignmentDto(a.TaskSlotId.ToString(), a.PersonId.ToString()))
                .ToList();

            // Slots with manual overrides are locked — solver must not reassign them
            lockedSlotIds = baselineRows
                .Where(a => a.Source == AssignmentSource.Override)
                .Select(a => a.TaskSlotId.ToString())
                .Distinct()
                .ToList();
        }

        // ── Fairness counters ─────────────────────────────────────────────────
        var fairness = await _db.FairnessCounters.AsNoTracking()
            .Where(f => f.SpaceId == spaceId && f.AsOfDate == today)
            .ToListAsync(ct);

        var fairnessDto = fairness.Select(f => new FairnessCountersDto(
            f.PersonId.ToString(),
            f.TotalAssignments7d, f.HatedTasks7d,
            f.DislikedHatedScore7d, f.KitchenCount7d,
            f.NightMissions7d, f.ConsecutiveBurdenCount)).ToList();

        // ── Space locale ──────────────────────────────────────────────────────
        var space = await _db.Spaces.AsNoTracking()
            .Where(s => s.Id == spaceId)
            .Select(s => new { s.Locale })
            .FirstOrDefaultAsync(ct);
        var locale = space?.Locale ?? "en";

        _logger.LogInformation(
            "Solver payload built: people={People} slots={Slots} hard={Hard} soft={Soft} horizon={Start}→{End}",
            peopleDto.Count, slotsDto.Count, hardConstraints.Count, softConstraints.Count,
            horizonStart, horizonEnd);

        return new SolverInputDto(
            spaceId.ToString(), runId.ToString(), triggerMode,
            horizonStart.ToString("yyyy-MM-dd"),
            horizonEnd.ToString("yyyy-MM-dd"),
            locale,
            DefaultWeights,
            peopleDto, availabilityDto, presenceDto, slotsDto,
            hardConstraints, softConstraints, emergencyConstraints,
            baselineAssignments, fairnessDto,
            lockedSlotIds.Count > 0 ? lockedSlotIds : null);
    }

    private static Dictionary<string, object> DeserializePayload(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Derives a deterministic unique GUID for a specific shift within a task.
    /// XORs the task GUID bytes with the shift index so each shift gets its own stable ID.
    /// </summary>
    private static Guid DeriveShiftGuid(Guid taskId, int shiftIndex)
    {
        var bytes = taskId.ToByteArray();
        // XOR the last 4 bytes with the shift index
        var indexBytes = BitConverter.GetBytes(shiftIndex);
        for (int i = 0; i < 4; i++)
            bytes[12 + i] ^= indexBytes[i];
        return new Guid(bytes);
    }
}

// Local helper — mirrors the one in Configurations but scoped to this file
file static class StringHelper
{
    internal static string ToSnakeCase(this string s) =>
        string.Concat(s.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));
}
