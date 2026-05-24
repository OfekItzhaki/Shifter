using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Platform.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record SolverStatsDto(
    int TotalRunsLast24h,
    int CompletedLast24h,
    int FailedLast24h,
    int AvgDurationMs,
    int QueueDepth);

public record StorageStatsDto(
    int TotalAssignments,
    int TotalConstraints,
    int TotalTasks);

public record PlatformStatsDto(
    int TotalUsers,
    int ActiveUsersLast7d,
    int TotalSpaces,
    int TotalGroups,
    int TotalPeople,
    SolverStatsDto SolverStats,
    StorageStatsDto StorageStats);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetPlatformStatsQuery : IRequest<PlatformStatsDto>;

public class GetPlatformStatsQueryHandler : IRequestHandler<GetPlatformStatsQuery, PlatformStatsDto>
{
    private readonly AppDbContext _db;

    public GetPlatformStatsQueryHandler(AppDbContext db) => _db = db;

    public async Task<PlatformStatsDto> Handle(GetPlatformStatsQuery request, CancellationToken ct)
    {
        // ── User stats ────────────────────────────────────────────────────────
        var totalUsers = await _db.Users.CountAsync(ct);

        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var activeUsersLast7d = await _db.RefreshTokens
            .Where(rt => rt.CreatedAt >= sevenDaysAgo)
            .Select(rt => rt.UserId)
            .Distinct()
            .CountAsync(ct);

        // ── Space / Group / People stats ──────────────────────────────────────
        var totalSpaces = await _db.Spaces.Where(s => s.DeletedAt == null).CountAsync(ct);
        var totalGroups = await _db.Groups.Where(g => g.DeletedAt == null).CountAsync(ct);
        var totalPeople = await _db.People.Where(p => p.IsActive).CountAsync(ct);

        // ── Solver stats (last 24h) ──────────────────────────────────────────
        var twentyFourHoursAgo = DateTime.UtcNow.AddHours(-24);
        var recentRuns = await _db.ScheduleRuns
            .Where(r => r.CreatedAt >= twentyFourHoursAgo)
            .ToListAsync(ct);

        var totalRunsLast24h = recentRuns.Count;
        var completedLast24h = recentRuns.Count(r => r.Status == ScheduleRunStatus.Completed);
        var failedLast24h = recentRuns.Count(r => r.Status == ScheduleRunStatus.Failed);

        var completedWithDuration = recentRuns
            .Where(r => r.Status == ScheduleRunStatus.Completed && r.DurationMs.HasValue)
            .Select(r => r.DurationMs!.Value)
            .ToList();
        var avgDurationMs = completedWithDuration.Count > 0
            ? (int)completedWithDuration.Average()
            : 0;

        var queueDepth = await _db.ScheduleRuns
            .Where(r => r.Status == ScheduleRunStatus.Queued || r.Status == ScheduleRunStatus.Running)
            .CountAsync(ct);

        // ── Storage stats ─────────────────────────────────────────────────────
        var totalAssignments = await _db.Assignments.CountAsync(ct);
        var totalConstraints = await _db.ConstraintRules.CountAsync(ct);
        var totalTasks = await _db.TaskTypes.CountAsync(ct);

        return new PlatformStatsDto(
            TotalUsers: totalUsers,
            ActiveUsersLast7d: activeUsersLast7d,
            TotalSpaces: totalSpaces,
            TotalGroups: totalGroups,
            TotalPeople: totalPeople,
            SolverStats: new SolverStatsDto(
                TotalRunsLast24h: totalRunsLast24h,
                CompletedLast24h: completedLast24h,
                FailedLast24h: failedLast24h,
                AvgDurationMs: avgDurationMs,
                QueueDepth: queueDepth),
            StorageStats: new StorageStatsDto(
                TotalAssignments: totalAssignments,
                TotalConstraints: totalConstraints,
                TotalTasks: totalTasks));
    }
}
