using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Constraints;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Jobuler.Infrastructure.Scheduling;

public class SolverPayloadNormalizer : ISolverPayloadNormalizer
{
    private readonly AppDbContext _db;

    // Default stability weights — sent in every payload per spec Section 5.4
    private static readonly StabilityWeightsDto DefaultWeights = new(10.0, 3.0, 1.0);

    public SolverPayloadNormalizer(AppDbContext db) => _db = db;

    public async Task<SolverInputDto> BuildAsync(
        Guid spaceId, Guid runId, string triggerMode,
        Guid? baselineVersionId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizonStart = today;

        // Use the maximum solver horizon across all groups in this space,
        // falling back to 7 days if no groups are configured.
        var maxHorizon = await _db.Groups.AsNoTracking()
            .Where(g => g.SpaceId == spaceId && g.DeletedAt == null)
            .MaxAsync(g => (int?)g.SolverHorizonDays, ct) ?? 7;

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

        // ── Group tasks (flat model — merged into solver slots) ───────────────
        // GroupTask is the newer model used by the UI. The solver treats each
        // active GroupTask as a TaskSlot. GroupId is used as the TaskTypeId so
        // the solver can group related slots; the task's own Id is the SlotId.
        var groupTasks = await _db.GroupTasks.AsNoTracking()
            .Where(t => t.SpaceId == spaceId
                && t.IsActive
                && t.EndsAt >= horizonStartDt
                && t.StartsAt <= horizonEndDt)
            .ToListAsync(ct);

        var groupTaskSlots = groupTasks.Select(t => new TaskSlotDto(
            t.Id.ToString(),
            t.GroupId.ToString(),   // use GroupId as the "task type" bucket
            t.Name,
            t.BurdenLevel.ToString().ToLower(),
            t.StartsAt.ToString("o"),
            t.EndsAt.ToString("o"),
            t.RequiredHeadcount,
            5,                      // default priority
            [],                     // no role requirements on group tasks
            [],
            t.AllowsOverlap)).ToList();

        slotsDto.AddRange(groupTaskSlots);

        // ── Constraints ───────────────────────────────────────────────────────
        var constraints = await _db.ConstraintRules.AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.IsActive)
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
                1.0, // default weight — Phase 3 extension point for per-rule weights
                DeserializePayload(c.RulePayloadJson)))
            .ToList();

        // ── Baseline assignments ──────────────────────────────────────────────
        var baselineAssignments = new List<BaselineAssignmentDto>();
        if (baselineVersionId.HasValue)
        {
            baselineAssignments = await _db.Assignments.AsNoTracking()
                .Where(a => a.ScheduleVersionId == baselineVersionId.Value && a.SpaceId == spaceId)
                .Select(a => new BaselineAssignmentDto(a.TaskSlotId.ToString(), a.PersonId.ToString()))
                .ToListAsync(ct);
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

        return new SolverInputDto(
            spaceId.ToString(), runId.ToString(), triggerMode,
            horizonStart.ToString("yyyy-MM-dd"),
            horizonEnd.ToString("yyyy-MM-dd"),
            DefaultWeights,
            peopleDto, availabilityDto, presenceDto, slotsDto,
            hardConstraints, softConstraints,
            baselineAssignments, fairnessDto);
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
}

// Local helper — mirrors the one in Configurations but scoped to this file
file static class StringHelper
{
    internal static string ToSnakeCase(this string s) =>
        string.Concat(s.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));
}
