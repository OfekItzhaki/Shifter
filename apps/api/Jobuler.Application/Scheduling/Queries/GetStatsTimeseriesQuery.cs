using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record TimeseriesDataPointDto(
    DateOnly Date,
    int AssignmentsCount,
    int HardCount,
    int NormalCount,
    int EasyCount);

public record TimeseriesResponseDto(
    List<TimeseriesDataPointDto> DataPoints,
    Guid? PeriodId,
    DateTime? PeriodStartsAt,
    DateTime? PeriodEndsAt);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetStatsTimeseriesQuery(
    Guid SpaceId,
    Guid GroupId,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? PeriodId = null) : IRequest<TimeseriesResponseDto>;

public class GetStatsTimeseriesQueryHandler : IRequestHandler<GetStatsTimeseriesQuery, TimeseriesResponseDto>
{
    private readonly AppDbContext _db;
    private readonly IPeriodManager _periodManager;

    public GetStatsTimeseriesQueryHandler(AppDbContext db, IPeriodManager periodManager)
    {
        _db = db;
        _periodManager = periodManager;
    }

    public async Task<TimeseriesResponseDto> Handle(GetStatsTimeseriesQuery req, CancellationToken ct)
    {
        var spaceId = req.SpaceId;
        var groupId = req.GroupId;

        // Resolve period for metadata
        Guid? periodId = null;
        DateTime? periodStartsAt = null;
        DateTime? periodEndsAt = null;

        if (req.PeriodId.HasValue)
        {
            var period = await _db.SubscriptionPeriods.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == req.PeriodId.Value && p.SpaceId == spaceId, ct);
            if (period != null)
            {
                periodId = period.Id;
                periodStartsAt = period.StartsAt;
                periodEndsAt = period.EndsAt;
            }
        }
        else
        {
            var currentPeriod = await _periodManager.GetCurrentPeriodAsync(spaceId, groupId, ct);
            if (currentPeriod != null)
            {
                periodId = currentPeriod.Id;
                periodStartsAt = currentPeriod.StartsAt;
                periodEndsAt = currentPeriod.EndsAt;
            }
        }

        // Query daily_snapshots grouped by date
        var query = _db.DailySnapshots.AsNoTracking()
            .Where(ds => ds.SpaceId == spaceId
                && ds.GroupId == groupId
                && ds.SnapshotDate >= req.StartDate
                && ds.SnapshotDate <= req.EndDate);

        // Scope to period if resolved
        if (periodId.HasValue)
            query = query.Where(ds => ds.PeriodId == periodId.Value);

        var dataPoints = await query
            .GroupBy(ds => ds.SnapshotDate)
            .Select(g => new TimeseriesDataPointDto(
                g.Key,
                g.Count(),
                g.Count(ds => ds.BurdenLevel == "hard"),
                g.Count(ds => ds.BurdenLevel == "normal" || ds.BurdenLevel == null),
                g.Count(ds => ds.BurdenLevel == "easy")))
            .OrderBy(dp => dp.Date)
            .ToListAsync(ct);

        return new TimeseriesResponseDto(
            DataPoints: dataPoints,
            PeriodId: periodId,
            PeriodStartsAt: periodStartsAt,
            PeriodEndsAt: periodEndsAt);
    }
}
