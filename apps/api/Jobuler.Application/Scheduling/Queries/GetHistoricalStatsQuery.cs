using MediatR;

namespace Jobuler.Application.Scheduling.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record HistoricalStatsDto(
    List<DailyStatPoint> AssignmentsPerDay,
    List<DailyStatPoint> SolverRunsPerDay,
    List<WeeklyStatPoint> BurdenScorePerWeek,
    int TotalAssignments,
    int TotalSolverRuns,
    int TotalVersionsPublished
);

public record DailyStatPoint(string Date, int Count);
public record WeeklyStatPoint(string WeekStart, double AverageScore);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetHistoricalStatsQuery(Guid SpaceId, int Days = 30) : IRequest<HistoricalStatsDto>;
