using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Commands;

/// <summary>
/// Recomputes fairness counters for all people in a space after a solver run completes.
/// Called by the SolverWorkerService after storing a draft version.
/// </summary>
public record UpdateFairnessCountersCommand(
    Guid SpaceId,
    Guid VersionId) : IRequest;

public class UpdateFairnessCountersCommandHandler
    : IRequestHandler<UpdateFairnessCountersCommand>
{
    private readonly AppDbContext _db;

    public UpdateFairnessCountersCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateFairnessCountersCommand req, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff7d  = DateTime.UtcNow.AddDays(-7);
        var cutoff14d = DateTime.UtcNow.AddDays(-14);
        var cutoff30d = DateTime.UtcNow.AddDays(-30);
        var cutoffDate30d = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        // Load all published version IDs for this space
        var publishedVersionIds = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .Select(v => v.Id)
            .ToListAsync(ct);

        // Read from DailySnapshots which store the effective (split-adjusted) burden level.
        // Join to TaskTypes only for the task name (needed for kitchen counting).
        var rawSnapshots = await _db.DailySnapshots.AsNoTracking()
            .Where(ds => ds.SpaceId == req.SpaceId &&
                         publishedVersionIds.Contains(ds.VersionId) &&
                         ds.SnapshotDate >= cutoffDate30d &&
                         ds.TaskTypeId != null &&
                         ds.ShiftStart != null)
            .Join(_db.TaskTypes.AsNoTracking(), ds => ds.TaskTypeId, t => t.Id,
                (ds, t) => new
                {
                    ds.PersonId,
                    StartsAt = ds.ShiftStart!.Value,
                    EndsAt = ds.ShiftEnd,
                    BurdenLevelRaw = ds.BurdenLevel,
                    t.Name
                })
            .ToListAsync(ct);

        var recentAssignments = rawSnapshots.Select(x => new
        {
            x.PersonId,
            x.StartsAt,
            x.EndsAt,
            BurdenLevel = Enum.TryParse<TaskBurdenLevel>(x.BurdenLevelRaw, true, out var parsed)
                ? parsed
                : TaskBurdenLevel.Normal,
            x.Name,
            IsNight = x.StartsAt.Hour >= 22 || x.StartsAt.Hour < 6
        }).ToList();

        var people = await _db.People.AsNoTracking()
            .Where(p => p.SpaceId == req.SpaceId && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var personId in people)
        {
            var mine = recentAssignments.Where(a => a.PersonId == personId).ToList();

            var total7d  = mine.Count(a => a.StartsAt >= cutoff7d);
            var total14d = mine.Count(a => a.StartsAt >= cutoff14d);
            var total30d = mine.Count;

            var hard7d  = mine.Count(a => a.StartsAt >= cutoff7d && a.BurdenLevel == TaskBurdenLevel.Hard);
            var hard14d = mine.Count(a => a.StartsAt >= cutoff14d && a.BurdenLevel == TaskBurdenLevel.Hard);
            var hard30d = mine.Count(a => a.BurdenLevel == TaskBurdenLevel.Hard);

            var easy7d  = mine.Count(a => a.StartsAt >= cutoff7d && a.BurdenLevel == TaskBurdenLevel.Easy);
            var easy14d = mine.Count(a => a.StartsAt >= cutoff14d && a.BurdenLevel == TaskBurdenLevel.Easy);
            var easy30d = mine.Count(a => a.BurdenLevel == TaskBurdenLevel.Easy);

            // Burden score: (hard×3) − (easy×1)
            var burdenScore7d  = (hard7d * 3) - easy7d;
            var burdenScore14d = (hard14d * 3) - easy14d;
            var burdenScore30d = (hard30d * 3) - easy30d;

            var kitchen7d = mine.Count(a => a.StartsAt >= cutoff7d &&
                (a.Name.Contains("מטבח", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("kitchen", StringComparison.OrdinalIgnoreCase)));

            var night7d = mine.Count(a => a.StartsAt >= cutoff7d && a.IsNight);

            // Consecutive burden: count how many of the last N assignments were hard tasks
            var sorted = mine.OrderByDescending(a => a.StartsAt).Take(5).ToList();
            var consecutive = 0;
            foreach (var a in sorted)
            {
                if (a.BurdenLevel is TaskBurdenLevel.Hard)
                    consecutive++;
                else
                    break;
            }

            // Compute generic task-type counts (7d window)
            var taskTypeCounts7d = mine
                .Where(a => a.StartsAt >= cutoff7d)
                .GroupBy(a => a.Name.ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.Count());
            var taskTypeCountsJson = taskTypeCounts7d.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(taskTypeCounts7d)
                : "{}";

            // Upsert fairness counter
            var existing = await _db.FairnessCounters
                .FirstOrDefaultAsync(f => f.SpaceId == req.SpaceId &&
                    f.PersonId == personId && f.AsOfDate == today, ct);

            if (existing is null)
            {
                var counter = FairnessCounter.Create(req.SpaceId, personId, today);
                counter.Update(total7d, total14d, total30d,
                    hard7d, hard14d, hard30d,
                    easy7d, easy14d, easy30d,
                    burdenScore7d, burdenScore14d, burdenScore30d,
                    night7d, consecutive,
                    taskTypeCountsJson);
                _db.FairnessCounters.Add(counter);
            }
            else
            {
                existing.Update(total7d, total14d, total30d,
                    hard7d, hard14d, hard30d,
                    easy7d, easy14d, easy30d,
                    burdenScore7d, burdenScore14d, burdenScore30d,
                    night7d, consecutive,
                    taskTypeCountsJson);
            }

            // Upsert daily snapshot for historical graphs
            // Use 30d window values for the snapshot (total_assignments, hard, normal, easy, burden_score)
            var normal30d = total30d - hard30d - easy30d;
            var existingSnapshot = await _db.FairnessCounterSnapshots
                .FirstOrDefaultAsync(s => s.SpaceId == req.SpaceId &&
                    s.PersonId == personId && s.SnapshotDate == today, ct);

            if (existingSnapshot is null)
            {
                var snapshot = FairnessCounterSnapshot.Create(
                    req.SpaceId, personId, today,
                    total30d, hard30d, normal30d, easy30d, burdenScore30d);
                _db.FairnessCounterSnapshots.Add(snapshot);
            }
            else
            {
                existingSnapshot.Update(total30d, hard30d, normal30d, easy30d, burdenScore30d);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
