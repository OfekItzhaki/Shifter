using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Per-person burden statistics. Sourced from fairness_counters (rolling ledger)
/// and live assignment data. Designed to be reusable as solver stability weights.
/// </summary>
public record PersonBurdenStatsDto(
    Guid PersonId,
    string DisplayName,
    string? ProfileImageUrl,
    // Rolling counters (from fairness_counters table)
    int TotalAssignments7d,
    int TotalAssignments14d,
    int TotalAssignments30d,
    int HatedTasks7d,
    int HatedTasks14d,
    int DislikedHatedScore7d,
    int KitchenCount7d,
    int NightMissions7d,
    int ConsecutiveBurdenCount,
    // All-time counters (from assignments table, published versions only)
    int TotalAssignmentsAllTime,
    int HatedTasksAllTime,
    int DislikedTasksAllTime,
    int FavorableTasksAllTime,
    // Computed burden score (weighted: hated=3, disliked=1)
    int BurdenScoreAllTime,
    // Extended stats
    int GroupsCount,
    DateTime? LastAssignmentDate,
    float AverageAssignmentsPerWeek,
    int BurdenBalance);

/// <summary>
/// Space-level burden statistics summary with per-person breakdown and leaderboards.
/// </summary>
public record BurdenStatsDto(
    List<PersonBurdenStatsDto> People,
    // Leaderboards — top N per category
    List<LeaderboardEntryDto> MostAssignments,
    List<LeaderboardEntryDto> MostHatedTasks,
    List<LeaderboardEntryDto> HighestBurdenScore,
    List<LeaderboardEntryDto> MostKitchenDuty,
    List<LeaderboardEntryDto> MostNightMissions,
    List<LeaderboardEntryDto> MostFavorableTasks,
    List<LeaderboardEntryDto> BestBurdenBalance,
    List<LeaderboardEntryDto> WorstBurdenBalance,
    List<LeaderboardEntryDto> MostConsecutiveBurden,
    // Space totals
    int TotalPublishedAssignments,
    int TotalPeople,
    int TotalGroups,
    int TotalPublishedVersions,
    float AverageAssignmentsPerPerson,
    Guid? MostBurdenedPersonId,
    Guid? LeastBurdenedPersonId,
    DateTime? LastUpdated);

public record LeaderboardEntryDto(
    Guid PersonId,
    string DisplayName,
    string? ProfileImageUrl,
    int Value,
    string Label);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetBurdenStatsQuery(Guid SpaceId) : IRequest<BurdenStatsDto>;

public class GetBurdenStatsQueryHandler : IRequestHandler<GetBurdenStatsQuery, BurdenStatsDto>
{
    private readonly AppDbContext _db;

    public GetBurdenStatsQueryHandler(AppDbContext db) => _db = db;

    public async Task<BurdenStatsDto> Handle(GetBurdenStatsQuery req, CancellationToken ct)
    {
        var spaceId = req.SpaceId;

        // ── People in this space ──────────────────────────────────────────────
        var people = await _db.People.AsNoTracking()
            .Where(p => p.SpaceId == spaceId && p.IsActive)
            .Select(p => new { p.Id, p.DisplayName, p.FullName, p.ProfileImageUrl })
            .ToListAsync(ct);

        var totalPeople = people.Count;

        if (totalPeople == 0)
            return new BurdenStatsDto([], [], [], [], [], [], [], [], [], [], 0, 0, 0, 0, 0f, null, null, null);

        var personIds = people.Select(p => p.Id).ToHashSet();

        // ── Groups count per person ───────────────────────────────────────────
        var groupMemberships = await _db.GroupMemberships.AsNoTracking()
            .Where(gm => gm.SpaceId == spaceId && personIds.Contains(gm.PersonId))
            .GroupBy(gm => gm.PersonId)
            .Select(g => new { PersonId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var groupsCountMap = groupMemberships.ToDictionary(g => g.PersonId, g => g.Count);

        // ── Total active groups ───────────────────────────────────────────────
        var totalGroups = await _db.Groups.AsNoTracking()
            .Where(g => g.SpaceId == spaceId && g.DeletedAt == null)
            .CountAsync(ct);

        // ── Latest fairness counters per person ───────────────────────────────
        var latestDate = await _db.FairnessCounters.AsNoTracking()
            .Where(f => f.SpaceId == spaceId && personIds.Contains(f.PersonId))
            .MaxAsync(f => (DateOnly?)f.AsOfDate, ct);

        var counters = latestDate.HasValue
            ? await _db.FairnessCounters.AsNoTracking()
                .Where(f => f.SpaceId == spaceId
                    && personIds.Contains(f.PersonId)
                    && f.AsOfDate == latestDate.Value)
                .ToListAsync(ct)
            : [];

        var counterMap = counters.ToDictionary(c => c.PersonId);

        // ── Published versions ────────────────────────────────────────────────
        var publishedVersionIds = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == spaceId && v.Status == Domain.Scheduling.ScheduleVersionStatus.Published)
            .Select(v => v.Id)
            .ToListAsync(ct);

        var totalPublishedVersions = publishedVersionIds.Count;

        // ── All assignments in published versions ─────────────────────────────
        var assignments = publishedVersionIds.Count > 0
            ? await _db.Assignments.AsNoTracking()
                .Where(a => a.SpaceId == spaceId
                    && personIds.Contains(a.PersonId)
                    && publishedVersionIds.Contains(a.ScheduleVersionId))
                .Select(a => new { a.PersonId, a.TaskSlotId })
                .ToListAsync(ct)
            : [];

        var slotIds = assignments.Select(a => a.TaskSlotId).Distinct().ToList();

        // ── Burden levels: task slots (via join to task_types) ────────────────
        // FIX: TaskSlot has no .TaskType navigation — join manually via TaskTypeId
        var slotTypeIds = await _db.TaskSlots.AsNoTracking()
            .Where(s => slotIds.Contains(s.Id))
            .Select(s => new { SlotId = s.Id, s.TaskTypeId })
            .ToListAsync(ct);

        var taskTypeIds = slotTypeIds.Select(s => s.TaskTypeId).Distinct().ToList();

        var taskTypeBurdens = await _db.TaskTypes.AsNoTracking()
            .Where(t => taskTypeIds.Contains(t.Id))
            .Select(t => new { t.Id, t.BurdenLevel })
            .ToListAsync(ct);

        var taskTypeBurdenMap = taskTypeBurdens.ToDictionary(t => t.Id, t => t.BurdenLevel.ToString().ToLower());

        // Build slot → burden map from the join
        var burdenMap = slotTypeIds.ToDictionary(
            s => s.SlotId,
            s => taskTypeBurdenMap.GetValueOrDefault(s.TaskTypeId, "neutral"));

        // Also check group tasks (slot IDs may be group task IDs)
        var groupTaskBurdens = await _db.GroupTasks.AsNoTracking()
            .Where(t => slotIds.Contains(t.Id))
            .Select(t => new { t.Id, BurdenLevel = t.BurdenLevel.ToString().ToLower() })
            .ToListAsync(ct);

        foreach (var gt in groupTaskBurdens)
            burdenMap.TryAdd(gt.Id, gt.BurdenLevel);

        // ── Last assignment date per person ───────────────────────────────────
        // We need StartsAt from task slots for the date; use slot IDs we already have
        // Approximate: use the assignment row's existence — we'll track by slot StartsAt
        var slotDates = await _db.TaskSlots.AsNoTracking()
            .Where(s => slotIds.Contains(s.Id))
            .Select(s => new { s.Id, s.StartsAt })
            .ToListAsync(ct);

        var slotDateMap = slotDates.ToDictionary(s => s.Id, s => s.StartsAt);

        // Also get group task dates
        var groupTaskDates = await _db.GroupTasks.AsNoTracking()
            .Where(t => slotIds.Contains(t.Id))
            .Select(t => new { t.Id, t.StartsAt })
            .ToListAsync(ct);

        foreach (var gt in groupTaskDates)
            slotDateMap.TryAdd(gt.Id, gt.StartsAt);

        // ── Compute per-person all-time stats ─────────────────────────────────
        var allTimeStats = assignments
            .GroupBy(a => a.PersonId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var total = g.Count();
                    var hated = g.Count(a => burdenMap.GetValueOrDefault(a.TaskSlotId, "neutral") == "hated");
                    var disliked = g.Count(a => burdenMap.GetValueOrDefault(a.TaskSlotId, "neutral") == "disliked");
                    var favorable = g.Count(a => burdenMap.GetValueOrDefault(a.TaskSlotId, "neutral") == "favorable");
                    var lastDate = g
                        .Where(a => slotDateMap.ContainsKey(a.TaskSlotId))
                        .Select(a => slotDateMap[a.TaskSlotId])
                        .DefaultIfEmpty()
                        .Max();
                    return (Total: total, Hated: hated, Disliked: disliked, Favorable: favorable,
                            Score: hated * 3 + disliked, LastDate: lastDate == default ? (DateTime?)null : lastDate);
                });

        // ── Build per-person stats ────────────────────────────────────────────
        var personStats = people.Select(p =>
        {
            counterMap.TryGetValue(p.Id, out var c);
            allTimeStats.TryGetValue(p.Id, out var at);
            groupsCountMap.TryGetValue(p.Id, out var gc);

            var avgPerWeek = MathF.Round(((c?.TotalAssignments30d ?? 0) / 4.0f), 1);
            var burdenBalance = at.Favorable - at.Hated;

            return new PersonBurdenStatsDto(
                PersonId: p.Id,
                DisplayName: p.DisplayName ?? p.FullName,
                ProfileImageUrl: p.ProfileImageUrl,
                TotalAssignments7d: c?.TotalAssignments7d ?? 0,
                TotalAssignments14d: c?.TotalAssignments14d ?? 0,
                TotalAssignments30d: c?.TotalAssignments30d ?? 0,
                HatedTasks7d: c?.HatedTasks7d ?? 0,
                HatedTasks14d: c?.HatedTasks14d ?? 0,
                DislikedHatedScore7d: c?.DislikedHatedScore7d ?? 0,
                KitchenCount7d: c?.KitchenCount7d ?? 0,
                NightMissions7d: c?.NightMissions7d ?? 0,
                ConsecutiveBurdenCount: c?.ConsecutiveBurdenCount ?? 0,
                TotalAssignmentsAllTime: at.Total,
                HatedTasksAllTime: at.Hated,
                DislikedTasksAllTime: at.Disliked,
                FavorableTasksAllTime: at.Favorable,
                BurdenScoreAllTime: at.Score,
                GroupsCount: gc,
                LastAssignmentDate: at.LastDate,
                AverageAssignmentsPerWeek: avgPerWeek,
                BurdenBalance: burdenBalance);
        }).ToList();

        // ── Build leaderboards (top 5 per category) ───────────────────────────
        static List<LeaderboardEntryDto> Top5(
            IEnumerable<PersonBurdenStatsDto> src,
            Func<PersonBurdenStatsDto, int> selector,
            string label,
            bool ascending = false) =>
            (ascending
                ? src.OrderBy(selector)
                : src.OrderByDescending(selector))
               .Take(5)
               .Select(p => new LeaderboardEntryDto(p.PersonId, p.DisplayName, p.ProfileImageUrl, selector(p), label))
               .ToList();

        var avgPerPerson = totalPeople > 0 ? (float)assignments.Count / totalPeople : 0f;

        var mostBurdened = personStats.OrderByDescending(p => p.BurdenScoreAllTime).FirstOrDefault();
        var leastBurdened = personStats
            .Where(p => p.TotalAssignmentsAllTime > 0)
            .OrderBy(p => p.BurdenScoreAllTime)
            .FirstOrDefault();

        return new BurdenStatsDto(
            People: personStats,
            MostAssignments: Top5(personStats, p => p.TotalAssignmentsAllTime, "assignments"),
            MostHatedTasks: Top5(personStats, p => p.HatedTasksAllTime, "hated tasks"),
            HighestBurdenScore: Top5(personStats, p => p.BurdenScoreAllTime, "burden score"),
            MostKitchenDuty: Top5(personStats, p => p.KitchenCount7d, "kitchen (7d)"),
            MostNightMissions: Top5(personStats, p => p.NightMissions7d, "night missions (7d)"),
            MostFavorableTasks: Top5(personStats, p => p.FavorableTasksAllTime, "favorable tasks"),
            BestBurdenBalance: Top5(personStats, p => p.BurdenBalance, "burden balance"),
            WorstBurdenBalance: Top5(personStats, p => p.BurdenBalance, "burden balance", ascending: true),
            MostConsecutiveBurden: Top5(personStats, p => p.ConsecutiveBurdenCount, "consecutive burden"),
            TotalPublishedAssignments: assignments.Count,
            TotalPeople: totalPeople,
            TotalGroups: totalGroups,
            TotalPublishedVersions: totalPublishedVersions,
            AverageAssignmentsPerPerson: MathF.Round(avgPerPerson, 1),
            MostBurdenedPersonId: mostBurdened?.PersonId,
            LeastBurdenedPersonId: leastBurdened?.PersonId,
            LastUpdated: latestDate.HasValue ? latestDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null);
    }
}
