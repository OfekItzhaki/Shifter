using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Analyzes solver output to detect staffing shortfalls and produce
/// double-shift recommendations. Runs after each solver run completes.
/// This class implements the analysis logic AND persistence/lifecycle management.
/// </summary>
public class RecommendationEngine : IRecommendationEngine
{
    private readonly AppDbContext _db;
    private readonly ILogger<RecommendationEngine> _logger;
    private const int MaxRecommendations = 10;

    public RecommendationEngine(AppDbContext db, ILogger<RecommendationEngine> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RecommendationResult> AnalyzeAsync(
        Guid spaceId,
        Guid groupId,
        Guid runId,
        SolverInputDto input,
        SolverOutputDto output,
        CancellationToken ct = default)
    {
        // Early exit: no uncovered slots means no shortfall
        if (output.UncoveredSlotIds.Count == 0)
        {
            // No shortfall — clear all active recommendations for this group (Req 5.1)
            await ClearActiveRecommendationsAsync(spaceId, groupId, ct);
            return new RecommendationResult(HasShortfall: false, Recommendations: new List<RecommendationItem>());
        }

        // Load HomeLeaveConfig for MinPeopleAtBase and EmergencyFreezeActive
        var homeLeaveConfig = await _db.HomeLeaveConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.GroupId == groupId, ct);

        // If emergency freeze is active, skip recommendation generation (Req 6.3)
        if (homeLeaveConfig?.EmergencyFreezeActive == true)
        {
            _logger.LogInformation(
                "Skipping recommendation generation for group {GroupId}: emergency freeze active.", groupId);
            return new RecommendationResult(HasShortfall: false, Recommendations: new List<RecommendationItem>());
        }

        var minPeopleAtBase = homeLeaveConfig?.MinPeopleAtBase ?? 8;

        // ── Step 1: Shortfall detection ──────────────────────────────────────
        // Calculate available personnel per day = total group members − people on home leave that day.
        // Flag days where available < MinPeopleAtBase.
        var totalMembers = input.People.Count;
        var hasShortfall = DetectShortfall(totalMembers, minPeopleAtBase, input, output);

        if (!hasShortfall)
        {
            // No shortfall — clear all active recommendations for this group (Req 5.1)
            await ClearActiveRecommendationsAsync(spaceId, groupId, ct);
            return new RecommendationResult(HasShortfall: false, Recommendations: new List<RecommendationItem>());
        }

        // ── Step 2: Candidate filtering ──────────────────────────────────────
        // Select active GroupTask entities where AllowsDoubleShift == false.
        // Skip if fewer than 2 candidates exist (Req 6.4).
        var candidateTasks = await _db.GroupTasks.AsNoTracking()
            .Where(t => t.SpaceId == spaceId
                && t.GroupId == groupId
                && t.IsActive
                && !t.AllowsDoubleShift)
            .ToListAsync(ct);

        if (candidateTasks.Count < 2)
        {
            _logger.LogInformation(
                "Skipping recommendation generation for group {GroupId}: fewer than 2 eligible tasks ({Count}).",
                groupId, candidateTasks.Count);
            return new RecommendationResult(HasShortfall: true, Recommendations: new List<RecommendationItem>());
        }

        // ── Step 3: Coverage simulation ──────────────────────────────────────
        // Build a lookup of uncovered slot IDs for fast membership checks.
        var uncoveredSlotIds = new HashSet<string>(output.UncoveredSlotIds);

        // Build a mapping from TaskTypeId (GroupTask.Id) to its task slots from the solver input.
        var slotsByTask = input.TaskSlots
            .GroupBy(s => s.TaskTypeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartsAt).ToList());

        var recommendations = new List<RecommendationItem>();

        foreach (var task in candidateTasks)
        {
            var taskIdStr = task.Id.ToString();

            if (!slotsByTask.TryGetValue(taskIdStr, out var taskSlots))
                continue;

            // Find uncovered slots for this task
            var uncoveredTaskSlots = taskSlots
                .Where(s => uncoveredSlotIds.Contains(s.SlotId))
                .OrderBy(s => s.StartsAt)
                .ToList();

            if (uncoveredTaskSlots.Count == 0)
                continue;

            // Simulate double-shift coverage: count consecutive pairs of uncovered slots
            // where a single person could serve both shifts (adjacent time slots).
            var additionalSlotsCovered = SimulateDoubleShiftCoverage(uncoveredTaskSlots);

            if (additionalSlotsCovered < 1)
                continue;

            // Determine affected date range
            var affectedDateStart = DateTime.Parse(uncoveredTaskSlots.First().StartsAt).Date;
            var affectedDateEnd = DateTime.Parse(uncoveredTaskSlots.Last().EndsAt).Date;

            recommendations.Add(new RecommendationItem(
                GroupTaskId: task.Id,
                TaskName: task.Name,
                AdditionalSlotsCovered: additionalSlotsCovered,
                AffectedDateStart: affectedDateStart,
                AffectedDateEnd: affectedDateEnd));
        }

        // ── Step 4: Ranking ──────────────────────────────────────────────────
        // Sort by AdditionalSlotsCovered DESC, then TaskName ASC. Cap at 10.
        var rankedRecommendations = recommendations
            .OrderByDescending(r => r.AdditionalSlotsCovered)
            .ThenBy(r => r.TaskName, StringComparer.Ordinal)
            .Take(MaxRecommendations)
            .ToList();

        // ── Step 5: Persist recommendations ──────────────────────────────────
        await PersistRecommendationsAsync(spaceId, groupId, runId, rankedRecommendations,
            output.UncoveredSlotIds.Count, ct);

        // ── Step 6: Return result ────────────────────────────────────────────
        return new RecommendationResult(
            HasShortfall: true,
            Recommendations: rankedRecommendations);
    }

    /// <summary>
    /// Persists recommendations using an upsert pattern on (space_id, schedule_run_id, group_task_id).
    /// If a recommendation already exists for the same combination, it is updated with fresh data.
    /// New recommendations are inserted with status Active.
    /// </summary>
    private async Task PersistRecommendationsAsync(
        Guid spaceId,
        Guid groupId,
        Guid runId,
        List<RecommendationItem> recommendations,
        int totalUncoveredSlotsInRun,
        CancellationToken ct)
    {
        if (recommendations.Count == 0)
            return;

        // Load existing recommendations for this run to support upsert
        var groupTaskIds = recommendations.Select(r => r.GroupTaskId).ToList();
        var existingRecommendations = await _db.DoubleShiftRecommendations
            .Where(r => r.SpaceId == spaceId
                && r.ScheduleRunId == runId
                && groupTaskIds.Contains(r.GroupTaskId))
            .ToListAsync(ct);

        var existingByTaskId = existingRecommendations
            .ToDictionary(r => r.GroupTaskId);

        foreach (var rec in recommendations)
        {
            if (existingByTaskId.TryGetValue(rec.GroupTaskId, out var existing))
            {
                // Upsert: update existing recommendation with fresh data
                existing.Update(
                    rec.TaskName,
                    rec.AdditionalSlotsCovered,
                    rec.AffectedDateStart,
                    rec.AffectedDateEnd,
                    totalUncoveredSlotsInRun);
            }
            else
            {
                // Insert new recommendation with Active status
                var newRec = DoubleShiftRecommendation.Create(
                    spaceId,
                    groupId,
                    runId,
                    rec.GroupTaskId,
                    rec.TaskName,
                    rec.AdditionalSlotsCovered,
                    rec.AffectedDateStart,
                    rec.AffectedDateEnd,
                    totalUncoveredSlotsInRun);

                _db.DoubleShiftRecommendations.Add(newRec);
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Persisted {Count} recommendation(s) for group {GroupId}, run {RunId}.",
            recommendations.Count, groupId, runId);
    }

    /// <summary>
    /// Transitions all Active recommendations for a group to Cleared status.
    /// Called when a new solver run completes without a staffing shortfall (Req 5.1).
    /// </summary>
    private async Task ClearActiveRecommendationsAsync(
        Guid spaceId,
        Guid groupId,
        CancellationToken ct)
    {
        var activeRecommendations = await _db.DoubleShiftRecommendations
            .Where(r => r.SpaceId == spaceId
                && r.GroupId == groupId
                && r.Status == RecommendationStatus.Active)
            .ToListAsync(ct);

        if (activeRecommendations.Count == 0)
            return;

        foreach (var rec in activeRecommendations)
        {
            rec.Clear();
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Cleared {Count} active recommendation(s) for group {GroupId} — no shortfall detected.",
            activeRecommendations.Count, groupId);
    }

    /// <summary>
    /// Detects whether a staffing shortfall exists by checking if available personnel
    /// at base falls below MinPeopleAtBase on any day in the scheduling horizon.
    /// Available = total members − people on home leave that day.
    /// </summary>
    internal static bool DetectShortfall(
        int totalMembers,
        int minPeopleAtBase,
        SolverInputDto input,
        SolverOutputDto output)
    {
        if (output.HomeLeaveAssignments.Count == 0)
        {
            // No home leave assignments — shortfall only if total members < min
            return totalMembers < minPeopleAtBase;
        }

        // Parse the scheduling horizon
        var horizonStart = DateTime.Parse(input.HorizonStart).Date;
        var horizonEnd = DateTime.Parse(input.HorizonEnd).Date;

        // For each day in the horizon, count how many people are on home leave
        for (var day = horizonStart; day <= horizonEnd; day = day.AddDays(1))
        {
            var onLeaveCount = CountPeopleOnLeave(output.HomeLeaveAssignments, day);
            var availableAtBase = totalMembers - onLeaveCount;

            if (availableAtBase < minPeopleAtBase)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Counts how many distinct people are on home leave on a given day.
    /// A person is considered on leave if their leave window overlaps with the day.
    /// </summary>
    private static int CountPeopleOnLeave(List<HomeLeaveAssignmentDto> homeLeaveAssignments, DateTime day)
    {
        var dayStart = day;
        var dayEnd = day.AddDays(1);

        return homeLeaveAssignments
            .Where(a =>
            {
                var leaveStart = DateTime.Parse(a.StartsAt);
                var leaveEnd = DateTime.Parse(a.EndsAt);
                // Overlap: leave starts before day ends AND leave ends after day starts
                return leaveStart < dayEnd && leaveEnd > dayStart;
            })
            .Select(a => a.PersonId)
            .Distinct()
            .Count();
    }

    /// <summary>
    /// Simulates how many additional slots could be covered by allowing double shifts.
    /// For each pair of adjacent uncovered slots on the same task (consecutive in time),
    /// one additional slot could be covered per available person serving both shifts.
    /// Returns the count of consecutive uncovered slot pairs.
    /// </summary>
    internal static int SimulateDoubleShiftCoverage(List<TaskSlotDto> uncoveredTaskSlots)
    {
        if (uncoveredTaskSlots.Count < 2)
            return 0;

        var consecutivePairs = 0;

        for (var i = 0; i < uncoveredTaskSlots.Count - 1; i++)
        {
            var currentEnd = DateTime.Parse(uncoveredTaskSlots[i].EndsAt);
            var nextStart = DateTime.Parse(uncoveredTaskSlots[i + 1].StartsAt);

            // Consecutive: the next slot starts exactly when the current one ends
            if (currentEnd == nextStart)
            {
                consecutivePairs++;
                // Skip the next slot since it's already part of a pair
                i++;
            }
        }

        return consecutivePairs;
    }
}
