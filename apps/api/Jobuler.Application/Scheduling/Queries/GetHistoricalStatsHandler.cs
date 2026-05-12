using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

public class GetHistoricalStatsHandler : IRequestHandler<GetHistoricalStatsQuery, HistoricalStatsDto>
{
    private readonly AppDbContext _db;

    public GetHistoricalStatsHandler(AppDbContext db) => _db = db;

    public async Task<HistoricalStatsDto> Handle(GetHistoricalStatsQuery req, CancellationToken ct)
    {
        var spaceId = req.SpaceId;
        var since = DateTime.UtcNow.AddDays(-req.Days);

        // ── Assignments per day ───────────────────────────────────────────────
        // Assignments don't have their own date — use the parent ScheduleVersion's CreatedAt.
        var publishedVersionIds = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Published)
            .Select(v => v.Id)
            .ToListAsync(ct);

        var assignmentsRaw = await _db.Assignments.AsNoTracking()
            .Where(a => a.SpaceId == spaceId
                && publishedVersionIds.Contains(a.ScheduleVersionId)
                && a.CreatedAt >= since)
            .Select(a => new { a.CreatedAt })
            .ToListAsync(ct);

        var assignmentsPerDay = assignmentsRaw
            .GroupBy(a => a.CreatedAt.Date)
            .Select(g => new DailyStatPoint(g.Key.ToString("yyyy-MM-dd"), g.Count()))
            .OrderBy(p => p.Date)
            .ToList();

        // ── Solver runs per day ───────────────────────────────────────────────
        var runsRaw = await _db.ScheduleRuns.AsNoTracking()
            .Where(r => r.SpaceId == spaceId && r.CreatedAt >= since)
            .Select(r => new { r.CreatedAt })
            .ToListAsync(ct);

        var solverRunsPerDay = runsRaw
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new DailyStatPoint(g.Key.ToString("yyyy-MM-dd"), g.Count()))
            .OrderBy(p => p.Date)
            .ToList();

        // ── Burden score trend per week ───────────────────────────────────────
        // Approximate: average assignments per person per week (from published versions)
        var totalPeople = await _db.People.AsNoTracking()
            .Where(p => p.SpaceId == spaceId && p.IsActive)
            .CountAsync(ct);

        var weeklyAssignments = assignmentsRaw
            .GroupBy(a => StartOfWeek(a.CreatedAt))
            .Select(g => new WeeklyStatPoint(
                g.Key.ToString("yyyy-MM-dd"),
                totalPeople > 0 ? Math.Round((double)g.Count() / totalPeople, 2) : 0))
            .OrderBy(p => p.WeekStart)
            .ToList();

        // ── Totals ────────────────────────────────────────────────────────────
        var totalAssignments = await _db.Assignments.AsNoTracking()
            .Where(a => a.SpaceId == spaceId && publishedVersionIds.Contains(a.ScheduleVersionId))
            .CountAsync(ct);

        var totalSolverRuns = await _db.ScheduleRuns.AsNoTracking()
            .Where(r => r.SpaceId == spaceId)
            .CountAsync(ct);

        var totalVersionsPublished = publishedVersionIds.Count;

        return new HistoricalStatsDto(
            AssignmentsPerDay: assignmentsPerDay,
            SolverRunsPerDay: solverRunsPerDay,
            BurdenScorePerWeek: weeklyAssignments,
            TotalAssignments: totalAssignments,
            TotalSolverRuns: totalSolverRuns,
            TotalVersionsPublished: totalVersionsPublished);
    }

    private static DateTime StartOfWeek(DateTime dt)
    {
        var diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
        return dt.AddDays(-diff).Date;
    }
}
