using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Constraints;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobuler.Infrastructure.Scheduling;

public class SolverPayloadNormalizer : ISolverPayloadNormalizer
{
    private readonly AppDbContext _db;
    private readonly ILogger<SolverPayloadNormalizer> _logger;
    private readonly ICumulativeTracker _cumulativeTracker;

    // Default stability weights — sent in every payload per spec Section 5.4
    private static readonly StabilityWeightsDto DefaultWeights = new(
        SchedulingConstants.StabilityWeightTodayTomorrow,
        SchedulingConstants.StabilityWeightDays3To4,
        SchedulingConstants.StabilityWeightDays5To7);

    public SolverPayloadNormalizer(AppDbContext db, ILogger<SolverPayloadNormalizer> logger, ICumulativeTracker cumulativeTracker)
    {
        _db = db;
        _logger = logger;
        _cumulativeTracker = cumulativeTracker;
    }

    public async Task<SolverInputDto> BuildAsync(
        Guid spaceId, Guid runId, string triggerMode,
        Guid? baselineVersionId, Guid? groupId = null, DateTime? startTime = null, CancellationToken ct = default)
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
        // Use the provided startTime if given, otherwise default to midnight (00:00 UTC) of today.
        // This ensures shifts always align to day boundaries for cleaner schedules.
        var nowUtc = startTime.HasValue
            ? DateTime.SpecifyKind(startTime.Value, DateTimeKind.Utc)
            : new DateTime(today.Year, today.Month, today.Day, 0, 0, 0, DateTimeKind.Utc);

        // Round nowUtc DOWN to the nearest whole hour so shifts start on clean boundaries.
        nowUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, DateTimeKind.Utc);

        // horizonStart is the DATE sent to the solver as horizon_start.
        // When a custom startTime is provided, use that date; otherwise use today.
        var horizonStart = startTime.HasValue
            ? DateOnly.FromDateTime(nowUtc)
            : today;

        // Use the solver horizon for the specific group (if scoped), otherwise max across all groups.
        // Capped at 7 days to keep the CP-SAT model tractable.
        int maxHorizon;
        bool isClosedBase = false;
        if (groupId.HasValue)
        {
            var groupData = await _db.Groups.AsNoTracking()
                .Where(g => g.Id == groupId.Value && g.SpaceId == spaceId && g.DeletedAt == null)
                .Select(g => new { g.SolverHorizonDays, g.IsClosedBase })
                .FirstOrDefaultAsync(ct);
            maxHorizon = groupData?.SolverHorizonDays ?? 7;
            isClosedBase = groupData?.IsClosedBase ?? false;
        }
        else
        {
            maxHorizon = await _db.Groups.AsNoTracking()
                .Where(g => g.SpaceId == spaceId && g.DeletedAt == null)
                .MaxAsync(g => (int?)g.SolverHorizonDays, ct) ?? 7;
        }
        maxHorizon = Math.Max(SchedulingConstants.MinHorizonDays, maxHorizon);

        // ── Home-leave config (closed-base groups only) ───────────────────────
        HomeLeaveConfigDto? homeLeaveConfigDto = null;
        if (groupId.HasValue && isClosedBase)
        {
            var hlConfig = await _db.HomeLeaveConfigs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.GroupId == groupId.Value && c.SpaceId == spaceId, ct);

            if (hlConfig is not null
                && hlConfig.LeaveDurationHours > 0)
            {
                // Compute member count for deriving leave_capacity from min_people_at_base
                var hlMemberCount = await _db.GroupMemberships.AsNoTracking()
                    .CountAsync(m => m.GroupId == groupId.Value && m.SpaceId == spaceId, ct);
                homeLeaveConfigDto = BuildHomeLeaveConfigDto(hlConfig, hlMemberCount);
            }
            else
            {
                _logger.LogWarning(
                    "Closed-base group {GroupId} has no complete home-leave configuration; omitting home_leave_config from solver payload.",
                    groupId.Value);
            }
        }

        var horizonEnd = horizonStart.AddDays(maxHorizon); // exclusive end — full N days from start

        // ── People eligibility ────────────────────────────────────────────────
        // When group-scoped, only include members of that group.
        HashSet<Guid>? groupMemberIdSet = null;
        if (groupId.HasValue)
        {
            var memberIds = await _db.GroupMemberships.AsNoTracking()
                .Where(m => m.GroupId == groupId.Value && m.SpaceId == spaceId)
                .Select(m => m.PersonId)
                .ToListAsync(ct);
            groupMemberIdSet = memberIds.ToHashSet();
        }

        var people = await _db.People.AsNoTracking()
            .Where(p => p.SpaceId == spaceId && p.IsActive
                && (groupMemberIdSet == null || groupMemberIdSet.Contains(p.Id)))
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
            groupMemberships.Where(m => m.PersonId == p.Id).Select(m => m.GroupId.ToString()).ToList(),
            (double)(groupMemberships.FirstOrDefault(m => m.PersonId == p.Id)?.HomeLeavePriority ?? 1.0m)
        )).ToList();

        // ── Availability windows ──────────────────────────────────────────────
        var horizonStartDt = nowUtc; // start from NOW, not midnight
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

        // Load qualification name lookup: id → name
        // TaskSlot.RequiredQualificationIds contains UUIDs but PersonEligibility.qualification_ids contains names.
        // Resolve UUIDs to names so the solver can match them correctly.
        var qualificationNameLookup = await _db.PersonQualifications.AsNoTracking()
            .Where(q => q.SpaceId == spaceId)
            .Select(q => new { q.Id, q.Qualification })
            .Distinct()
            .ToDictionaryAsync(q => q.Id.ToString(), q => q.Qualification, ct);

        var slotsDto = slots
            .Where(s => s.EndsAt >= horizonStartDt) // double-check: exclude any past slots
            .Select(s =>
            {
                taskTypes.TryGetValue(s.TaskTypeId, out var tt);
                return new TaskSlotDto(
                    s.Id.ToString(),
                    s.TaskTypeId.ToString(),
                    tt?.Name ?? "Unknown",
                    (tt?.BurdenLevel ?? Domain.Tasks.TaskBurdenLevel.Normal).ToString().ToLower(),
                    s.StartsAt.ToString("o"),
                    s.EndsAt.ToString("o"),
                    s.RequiredHeadcount,
                    s.Priority,
                    s.RequiredRoleIds.Select(id => id.ToString()).ToList(),
                    // Resolve qualification UUIDs to names so the solver can match against person.qualification_ids
                    s.RequiredQualificationIds
                        .Select(id => qualificationNameLookup.TryGetValue(id.ToString(), out var name) ? name : id.ToString())
                        .ToList(),
                    tt?.AllowsOverlap ?? false);
            }).ToList();

        // ── Group tasks → shift slots ─────────────────────────────────────────
        // Each GroupTask defines a window + shift duration. Expand into individual
        // shift slots so the solver assigns people to specific time windows.
        // Use the full solver horizon (up to 7 days) so 24/7 tasks are fully covered.
        // When group-scoped, only include tasks belonging to that group.
        // Only include tasks whose EndsAt is in the future (or open-ended / MinValue).
        var groupTasks = await _db.GroupTasks.AsNoTracking()
            .Where(t => t.SpaceId == spaceId
                && t.IsActive
                && t.StartsAt <= horizonEndDt
                && t.EndsAt > horizonStartDt   // ← skip tasks that have already ended
                && (groupId == null || t.GroupId == groupId.Value))
            .ToListAsync(ct);

        foreach (var task in groupTasks)
        {
            var shiftDuration = TimeSpan.FromMinutes(task.ShiftDurationMinutes);
            if (shiftDuration.TotalMinutes < 1) continue;

            // Clamp to the solver horizon — start from horizonStartDt (user's chosen time, rounded to hour).
            // task.StartsAt is only used to prevent generating shifts before the task existed.
            // If the task started in the past, use horizonStartDt. If it starts in the future
            // (after horizonStartDt), use task.StartsAt — but only for tasks that haven't started yet.
            var windowStart = horizonStartDt;
            // Only use task.StartsAt if it's genuinely in the future AND the task hasn't been running yet
            if (task.StartsAt > horizonEndDt)
                continue; // task hasn't started yet and won't start within the horizon — skip entirely
            var windowEnd = task.EndsAt > horizonEndDt ? horizonEndDt : task.EndsAt;

            // For full-day tasks (1440 min = 24h), align the first shift to the task's
            // configured start time-of-day. Start from the NEXT occurrence after horizon start.
            if (task.ShiftDurationMinutes == 1440 && task.StartsAt <= horizonStartDt)
            {
                var taskTimeOfDay = task.StartsAt.TimeOfDay;
                var candidateStart = horizonStartDt.Date + taskTimeOfDay;
                // If today's occurrence hasn't started yet, use it; otherwise use tomorrow's
                if (candidateStart <= horizonStartDt)
                    candidateStart = candidateStart.AddDays(1);
                windowStart = DateTime.SpecifyKind(candidateStart, DateTimeKind.Utc);
            }

            // Apply daily time window if configured (e.g. task only runs 08:00–22:00 each day)
            var dailyStart = task.DailyStartTime;
            var dailyEnd   = task.DailyEndTime;

            // Safety cap: never generate more than SlotsPerDay shifts per day × horizon days
            int maxShiftsPerTask = Math.Max(SchedulingConstants.BaseMaxShiftsPerTask, maxHorizon * SchedulingConstants.SlotsPerDay);
            // Use ABSOLUTE shift index counting from the task's original start.
            // This ensures DeriveShiftGuid produces the same GUID for the same time slot
            // regardless of when the solver runs — critical for the display query to resolve
            // GUIDs back to correct times.
            var shiftIndex = (int)Math.Floor((windowStart - task.StartsAt).TotalMinutes / task.ShiftDurationMinutes);
            if (shiftIndex < 0) shiftIndex = 0;
            // Align shiftStart to the exact shift boundary from task's original start
            var shiftStart = task.StartsAt + TimeSpan.FromMinutes((double)shiftIndex * task.ShiftDurationMinutes);
            if (shiftStart < windowStart)
            {
                shiftIndex++;
                shiftStart += shiftDuration;
            }
            var maxIndex = shiftIndex + maxShiftsPerTask;
            while (shiftStart + shiftDuration <= windowEnd && shiftIndex < maxIndex)
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
                    task.RequiredQualificationNames,
                    task.AllowsOverlap,
                    task.AllowsDoubleShift,
                    task.QualificationRequirements
                        .Select(r => new QualificationRequirementSolverDto(r.QualificationName, r.Count, r.Mandatory))
                        .ToList()));

                shiftStart = shiftEnd;
                shiftIndex++;
            }
        }

        // ── Constraints ───────────────────────────────────────────────────────
        // Filter by effective date window: exclude constraints that have expired
        // before the horizon starts, or haven't started yet by the horizon end.
        // When group-scoped, include space-level constraints + constraints scoped to this group.
        var constraints = await _db.ConstraintRules.AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.IsActive
                && (c.EffectiveUntil == null || c.EffectiveUntil >= horizonStart)
                && (c.EffectiveFrom == null || c.EffectiveFrom <= horizonEnd)
                && (groupId == null
                    || c.ScopeType == ConstraintScopeType.Space
                    || c.ScopeType == ConstraintScopeType.Person
                    || c.ScopeType == ConstraintScopeType.Role
                    || (c.ScopeType == ConstraintScopeType.Group && c.ScopeId == groupId)
                    || c.ScopeType == ConstraintScopeType.TaskType))
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
        // Only include baseline assignments whose slot IDs appear in the current
        // slotsDto — this prevents past assignments from leaking into the new run.
        var baselineAssignments = new List<BaselineAssignmentDto>();
        var lockedSlotIds = new List<string>();
        if (baselineVersionId.HasValue)
        {
            var baselineRows = await _db.Assignments.AsNoTracking()
                .Where(a => a.ScheduleVersionId == baselineVersionId.Value && a.SpaceId == spaceId)
                .ToListAsync(ct);

            // Build a set of slot IDs that are actually in the current horizon payload
            var currentSlotIds = slotsDto.Select(s => s.SlotId).ToHashSet();

            baselineAssignments = baselineRows
                .Where(a => currentSlotIds.Contains(a.TaskSlotId.ToString()))
                .Select(a => new BaselineAssignmentDto(a.TaskSlotId.ToString(), a.PersonId.ToString()))
                .ToList();

            // Slots with manual overrides are locked — solver must not reassign them
            lockedSlotIds = baselineRows
                .Where(a => a.Source == AssignmentSource.Override
                         && currentSlotIds.Contains(a.TaskSlotId.ToString()))
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
            f.TotalAssignments7d, f.HardTasks7d,
            f.DislikedHatedScore7d, f.KitchenCount7d,
            f.NightMissions7d, f.ConsecutiveHardCount)).ToList();

        // ── Space locale ──────────────────────────────────────────────────────
        var space = await _db.Spaces.AsNoTracking()
            .Where(s => s.Id == spaceId)
            .Select(s => new { s.Locale })
            .FirstOrDefaultAsync(ct);
        var locale = space?.Locale ?? "en";

        _logger.LogInformation(
            "Solver payload built: group={Group} people={People} slots={Slots} hard={Hard} soft={Soft} horizon={Start}→{End} horizonStartDt={HorizonStartDt}",
            groupId?.ToString() ?? "all", peopleDto.Count, slotsDto.Count, hardConstraints.Count, softConstraints.Count,
            horizonStart, horizonEnd, horizonStartDt.ToString("o"));

        // ── Task rotation data (for army-template groups) ─────────────────────
        List<TaskRotationDto>? taskRotationDto = null;
        if (groupId.HasValue)
        {
            var rotationRecords = await _db.TaskRotationProgress.AsNoTracking()
                .Where(r => r.SpaceId == spaceId && r.GroupId == groupId.Value)
                .ToListAsync(ct);

            if (rotationRecords.Count > 0)
            {
                taskRotationDto = rotationRecords.Select(r => new TaskRotationDto(
                    r.PersonId.ToString(),
                    r.CompletedTaskTypeIds.Select(id => id.ToString()).ToList()
                )).ToList();
            }
        }

        // ── Cumulative tracking data ─────────────────────────────────────────
        List<CumulativeTrackingDto> cumulativeTrackingDto = [];
        if (groupId.HasValue)
        {
            var cumulativeData = await _cumulativeTracker.GetForSolverPayloadAsync(spaceId, groupId.Value, ct);
            if (cumulativeData.Count > 0)
            {
                cumulativeTrackingDto = cumulativeData;
            }
        }

        return new SolverInputDto(
            spaceId.ToString(), runId.ToString(), triggerMode,
            horizonStart.ToString("yyyy-MM-dd"),
            horizonEnd.ToString("yyyy-MM-dd"),
            locale,
            DefaultWeights,
            peopleDto, availabilityDto, presenceDto, slotsDto,
            hardConstraints, softConstraints, emergencyConstraints,
            baselineAssignments, fairnessDto,
            lockedSlotIds,
            homeLeaveConfigDto,
            taskRotationDto,
            CumulativeTracking: cumulativeTrackingDto);
    }

    public async Task<SolverInputDto> BuildPreviewAsync(
        Guid spaceId, Guid groupId, int balanceValue, CancellationToken ct)
    {
        // Build the full payload using the normal path (preview uses a synthetic runId)
        // Use "standard" trigger_mode — preview_mode is a separate boolean flag
        var payload = await BuildAsync(
            spaceId,
            runId: Guid.NewGuid(),
            triggerMode: "standard",
            baselineVersionId: null,
            groupId: groupId,
            startTime: null,
            ct: ct);

        // For preview, always construct a valid home-leave config regardless of emergency freeze state.
        // The preview shows what the schedule would look like with the given balance_value.
        if (payload.HomeLeaveConfig is not null)
        {
            payload = payload with
            {
                HomeLeaveConfig = payload.HomeLeaveConfig with { BalanceValue = balanceValue },
                PreviewMode = true
            };
        }
        else
        {
            // If home-leave config was omitted (e.g. due to emergency freeze with no scheduling),
            // rebuild it from the stored config for preview purposes.
            var hlConfig = await _db.HomeLeaveConfigs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.GroupId == groupId && c.SpaceId == spaceId, ct);

            if (hlConfig is not null && hlConfig.LeaveDurationHours > 0)
            {
                var previewMemberCount = await _db.GroupMemberships.AsNoTracking()
                    .CountAsync(m => m.GroupId == groupId && m.SpaceId == spaceId, ct);
                var previewLeaveCapacity = Math.Max(1, previewMemberCount - hlConfig.MinPeopleAtBase);

                var previewDto = new HomeLeaveConfigDto(
                    Enabled: true,
                    MinRestHours: 0,
                    EligibilityThresholdHours: (double)(hlConfig.BaseDays * 24),
                    LeaveCapacity: previewLeaveCapacity,
                    LeaveDurationHours: (double)hlConfig.LeaveDurationHours,
                    BalanceValue: balanceValue);

                payload = payload with
                {
                    HomeLeaveConfig = previewDto,
                    PreviewMode = true
                };
            }
            else
            {
                payload = payload with { PreviewMode = true };
            }
        }

        return payload;
    }

    /// <summary>
    /// Builds the HomeLeaveConfigDto based on the mode and emergency freeze state.
    /// Computes leave_capacity = memberCount - minPeopleAtBase.
    /// - Emergency freeze + don't use for scheduling: returns null (omit from payload)
    /// - Emergency freeze + use for scheduling: balance_value = 0, eligibility = 9999
    /// - Automatic mode: eligibility = baseDays × 24, balance from stored slider value
    /// - Manual mode: eligibility = baseDays × 24, balance = 50 (neutral)
    /// Always sets min_rest_hours = 0.
    /// </summary>
    private static HomeLeaveConfigDto? BuildHomeLeaveConfigDto(HomeLeaveConfig hlConfig, int memberCount)
    {
        // Derive leave_capacity from min_people_at_base
        var leaveCapacity = Math.Max(1, memberCount - hlConfig.MinPeopleAtBase);

        // Emergency freeze: don't use for scheduling → omit entirely
        if (hlConfig.EmergencyFreezeActive && !hlConfig.EmergencyUseForScheduling)
        {
            return null;
        }

        // Emergency freeze: use for scheduling → balance=0, threshold=9999
        if (hlConfig.EmergencyFreezeActive && hlConfig.EmergencyUseForScheduling)
        {
            return new HomeLeaveConfigDto(
                Enabled: true,
                MinRestHours: 0,
                EligibilityThresholdHours: 9999,
                LeaveCapacity: leaveCapacity,
                LeaveDurationHours: (double)hlConfig.LeaveDurationHours,
                BalanceValue: 0);
        }

        // Normal operation: mode-based construction
        var eligibilityThresholdHours = (double)(hlConfig.BaseDays * 24);
        var balanceValue = hlConfig.Mode == HomeLeaveMode.Manual
            ? 50
            : hlConfig.BalanceValue;

        return new HomeLeaveConfigDto(
            Enabled: true,
            MinRestHours: 0,
            EligibilityThresholdHours: eligibilityThresholdHours,
            LeaveCapacity: leaveCapacity,
            LeaveDurationHours: (double)hlConfig.LeaveDurationHours,
            BalanceValue: balanceValue);
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
