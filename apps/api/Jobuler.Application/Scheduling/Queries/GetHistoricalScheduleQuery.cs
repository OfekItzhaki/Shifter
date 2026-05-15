using Jobuler.Application.Scheduling;
using MediatR;

namespace Jobuler.Application.Scheduling.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record HistoricalScheduleResponseDto(
    List<DailySnapshotDto> Assignments,
    bool RetentionExceeded);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetHistoricalScheduleQuery(
    Guid SpaceId,
    Guid GroupId,
    DateOnly StartDate,
    DateOnly EndDate) : IRequest<HistoricalScheduleResponseDto>;

public class GetHistoricalScheduleQueryHandler : IRequestHandler<GetHistoricalScheduleQuery, HistoricalScheduleResponseDto>
{
    private readonly IAssignmentSnapshotService _snapshotService;

    public GetHistoricalScheduleQueryHandler(IAssignmentSnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
    }

    public async Task<HistoricalScheduleResponseDto> Handle(GetHistoricalScheduleQuery req, CancellationToken ct)
    {
        var snapshots = await _snapshotService.GetHistoricalAsync(
            req.SpaceId, req.GroupId, req.StartDate, req.EndDate, ct);

        // If the service returned empty and the date range is in the past,
        // it might be due to retention limits. We check by seeing if the start date
        // is significantly in the past and no data was returned.
        // The service already handles retention internally — if it returns empty
        // for a past date range, we flag retention_exceeded.
        var retentionExceeded = snapshots.Count == 0
            && req.StartDate < DateOnly.FromDateTime(DateTime.UtcNow);

        return new HistoricalScheduleResponseDto(
            Assignments: snapshots,
            RetentionExceeded: retentionExceeded);
    }
}
