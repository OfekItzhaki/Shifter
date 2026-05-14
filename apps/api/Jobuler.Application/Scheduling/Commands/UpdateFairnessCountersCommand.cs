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

        // Load all published assignments for this space within the 30-day window
        var publishedVersionIds = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .Select(v => v.Id)
            .ToListAsync(ct);

        var recentAssignments = await _db.Assignments.AsNoTracking()
            .Where(a => a.SpaceId == req.SpaceId &&
                        publishedVersionIds.Contains(a.ScheduleVersionId))
            .Join(_db.TaskSlots.AsNoTracking(), a => a.TaskSlotId, s => s.Id,
                (a, s) => new { a.PersonId, s.StartsAt, s.EndsAt, s.TaskTypeId })
            .Join(_db.TaskTypes.AsNoTracking(), x => x.TaskTypeId, t => t.Id,
                (x, t) => new
                {
                    x.PersonId,
                    x.StartsAt,
                    x.EndsAt,
                    t.BurdenLevel,
                    t.Name,
                    IsNight = x.StartsAt.Hour >= 22 || x.StartsAt.Hour < 6
                })
            .Where(x => x.StartsAt >= cutoff30d)
            .ToListAsync(ct);

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
                    kitchen7d, night7d, consecutive);
                _db.FairnessCounters.Add(counter);
            }
            else
            {
                existing.Update(total7d, total14d, total30d,
                    hard7d, hard14d, hard30d,
                    easy7d, easy14d, easy30d,
                    burdenScore7d, burdenScore14d, burdenScore30d,
                    kitchen7d, night7d, consecutive);
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
