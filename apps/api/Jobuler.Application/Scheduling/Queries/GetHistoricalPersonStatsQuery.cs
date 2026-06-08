using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record HistoricalPersonStatsDto(
    List<PersonDailyStatsDto> DataPoints
);

public record PersonDailyStatsDto(
    Guid PersonId,
    string DisplayName,
    DateOnly Date,
    int TotalAssignments,
    int HardCount,
    int NormalCount,
    int EasyCount,
    int BurdenScore
);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetHistoricalPersonStatsQuery(
    Guid SpaceId,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? GroupId = null
) : IRequest<HistoricalPersonStatsDto>;

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetHistoricalPersonStatsQueryHandler
    : IRequestHandler<GetHistoricalPersonStatsQuery, HistoricalPersonStatsDto>
{
    private readonly AppDbContext _db;

    public GetHistoricalPersonStatsQueryHandler(AppDbContext db) => _db = db;

    public async Task<HistoricalPersonStatsDto> Handle(
        GetHistoricalPersonStatsQuery req, CancellationToken ct)
    {
        // Validate: StartDate must be before EndDate
        if (req.StartDate >= req.EndDate)
            throw new InvalidOperationException("Start date must be before end date.");

        // Validate: date range must not exceed 365 days
        var daysDiff = req.EndDate.DayNumber - req.StartDate.DayNumber;
        if (daysDiff > 365)
            throw new InvalidOperationException("Date range cannot exceed 365 days.");

        // Determine which person IDs to include
        IQueryable<Guid> personIdsQuery;

        if (req.GroupId.HasValue)
        {
            // Filter to only people who are members of the specified group
            personIdsQuery = _db.GroupMemberships.AsNoTracking()
                .Where(m => m.GroupId == req.GroupId.Value && m.SpaceId == req.SpaceId)
                .Select(m => m.PersonId);
        }
        else
        {
            personIdsQuery = _db.People.AsNoTracking()
                .Where(p => p.SpaceId == req.SpaceId && p.IsActive)
                .Select(p => p.Id);
        }

        var personIds = await personIdsQuery.ToListAsync(ct);
        var personIdSet = personIds.ToHashSet();

        // Load display names for the people
        var people = await _db.People.AsNoTracking()
            .Where(p => p.SpaceId == req.SpaceId && personIdSet.Contains(p.Id))
            .Select(p => new { p.Id, p.DisplayName, p.FullName })
            .ToListAsync(ct);

        var nameMap = people.ToDictionary(
            p => p.Id,
            p => p.FullName);

        // Query snapshots filtered by space, date range, and person set
        var snapshots = await _db.FairnessCounterSnapshots.AsNoTracking()
            .Where(s => s.SpaceId == req.SpaceId
                && s.SnapshotDate >= req.StartDate
                && s.SnapshotDate <= req.EndDate
                && personIdSet.Contains(s.PersonId))
            .OrderBy(s => s.SnapshotDate)
            .ThenBy(s => s.PersonId)
            .ToListAsync(ct);

        // Map to DTOs
        var dataPoints = snapshots.Select(s => new PersonDailyStatsDto(
            PersonId: s.PersonId,
            DisplayName: nameMap.GetValueOrDefault(s.PersonId, "Unknown"),
            Date: s.SnapshotDate,
            TotalAssignments: s.TotalAssignments,
            HardCount: s.HardCount,
            NormalCount: s.NormalCount,
            EasyCount: s.EasyCount,
            BurdenScore: s.BurdenScore
        )).ToList();

        return new HistoricalPersonStatsDto(dataPoints);
    }
}
