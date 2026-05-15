using Jobuler.Application.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record CumulativePersonStatsDto(
    Guid PersonId,
    string DisplayName,
    string? ProfileImageUrl,
    int TotalAssignments,
    int HardTasks,
    int KitchenCount,
    int NightMissions,
    int DislikedHatedScore,
    decimal TotalHoursAssigned,
    decimal ConsecutiveHoursAtBase,
    DateTime? LastHomeLeaveEnd);

public record CumulativeStatsResponseDto(
    List<CumulativePersonStatsDto> People,
    Guid? PeriodId,
    DateTime? PeriodStartsAt,
    DateTime? PeriodEndsAt,
    string? PeriodStatus,
    string TimeRange);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetCumulativeStatsQuery(
    Guid SpaceId,
    Guid GroupId,
    string TimeRange,
    Guid? PeriodId = null) : IRequest<CumulativeStatsResponseDto>;

public class GetCumulativeStatsQueryHandler : IRequestHandler<GetCumulativeStatsQuery, CumulativeStatsResponseDto>
{
    private readonly AppDbContext _db;
    private readonly IPeriodManager _periodManager;

    public GetCumulativeStatsQueryHandler(AppDbContext db, IPeriodManager periodManager)
    {
        _db = db;
        _periodManager = periodManager;
    }

    public async Task<CumulativeStatsResponseDto> Handle(GetCumulativeStatsQuery req, CancellationToken ct)
    {
        var spaceId = req.SpaceId;
        var groupId = req.GroupId;

        // Resolve period
        Guid periodId;
        DateTime? periodStartsAt = null;
        DateTime? periodEndsAt = null;
        string? periodStatus = null;

        if (req.PeriodId.HasValue)
        {
            var period = await _db.SubscriptionPeriods.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == req.PeriodId.Value && p.SpaceId == spaceId, ct);
            if (period == null)
                return new CumulativeStatsResponseDto([], req.PeriodId, null, null, null, req.TimeRange);
            periodId = period.Id;
            periodStartsAt = period.StartsAt;
            periodEndsAt = period.EndsAt;
            periodStatus = period.Status;
        }
        else
        {
            var currentPeriod = await _periodManager.GetCurrentPeriodAsync(spaceId, groupId, ct);
            if (currentPeriod == null)
                return new CumulativeStatsResponseDto([], null, null, null, null, req.TimeRange);
            periodId = currentPeriod.Id;
            periodStartsAt = currentPeriod.StartsAt;
            periodEndsAt = currentPeriod.EndsAt;
            periodStatus = currentPeriod.Status;
        }

        // Load cumulative records for this period and group
        var records = await _db.CumulativeRecords.AsNoTracking()
            .Where(r => r.SpaceId == spaceId && r.GroupId == groupId && r.PeriodId == periodId)
            .ToListAsync(ct);

        // Load person info
        var personIds = records.Select(r => r.PersonId).Distinct().ToList();
        var people = await _db.People.AsNoTracking()
            .Where(p => p.SpaceId == spaceId && personIds.Contains(p.Id))
            .Select(p => new { p.Id, p.DisplayName, p.FullName, p.ProfileImageUrl })
            .ToListAsync(ct);

        var personMap = people.ToDictionary(p => p.Id);

        // Map records to DTOs based on time range
        var result = records.Select(r =>
        {
            personMap.TryGetValue(r.PersonId, out var person);
            var displayName = person?.DisplayName ?? person?.FullName ?? "Unknown";
            var profileImage = person?.ProfileImageUrl;

            // Select the appropriate time-window counters
            var (totalAssignments, hardTasks, kitchenCount, nightMissions, dislikedHatedScore) = req.TimeRange switch
            {
                "7d" => (r.TotalAssignments7d, r.HardTasks7d, r.KitchenCount7d, r.NightMissions7d, r.DislikedHatedScore7d),
                "14d" => (r.TotalAssignments14d, r.HardTasks14d, r.KitchenCount14d, r.NightMissions14d, r.DislikedHatedScore14d),
                "30d" => (r.TotalAssignments30d, r.HardTasks30d, r.KitchenCount30d, r.NightMissions30d, r.DislikedHatedScore30d),
                "90d" => (r.TotalAssignments90d, r.HardTasks90d, r.KitchenCount90d, r.NightMissions90d, r.DislikedHatedScore90d),
                _ => (r.TotalAssignmentsPeriod, r.HardTasksPeriod, r.KitchenCountPeriod, r.NightMissionsPeriod, r.DislikedHatedScorePeriod),
            };

            return new CumulativePersonStatsDto(
                PersonId: r.PersonId,
                DisplayName: displayName,
                ProfileImageUrl: profileImage,
                TotalAssignments: totalAssignments,
                HardTasks: hardTasks,
                KitchenCount: kitchenCount,
                NightMissions: nightMissions,
                DislikedHatedScore: dislikedHatedScore,
                TotalHoursAssigned: r.TotalHoursAssignedPeriod,
                ConsecutiveHoursAtBase: r.ConsecutiveHoursAtBase,
                LastHomeLeaveEnd: r.LastHomeLeaveEnd);
        }).ToList();

        return new CumulativeStatsResponseDto(
            People: result,
            PeriodId: periodId,
            PeriodStartsAt: periodStartsAt,
            PeriodEndsAt: periodEndsAt,
            PeriodStatus: periodStatus,
            TimeRange: req.TimeRange);
    }
}
