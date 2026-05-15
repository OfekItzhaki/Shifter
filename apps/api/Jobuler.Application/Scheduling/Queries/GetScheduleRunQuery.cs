using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

public record ScheduleRunDto(
    Guid Id, string TriggerType, string Status,
    Guid? BaselineVersionId, DateTime CreatedAt,
    DateTime? StartedAt, DateTime? FinishedAt,
    int? DurationMs, string? ResultSummaryJson, string? ErrorSummary,
    string? ProgressPhase);

public record GetScheduleRunQuery(Guid SpaceId, Guid RunId) : IRequest<ScheduleRunDto?>;

public class GetScheduleRunQueryHandler : IRequestHandler<GetScheduleRunQuery, ScheduleRunDto?>
{
    private readonly AppDbContext _db;
    public GetScheduleRunQueryHandler(AppDbContext db) => _db = db;

    public async Task<ScheduleRunDto?> Handle(GetScheduleRunQuery req, CancellationToken ct)
    {
        var run = await _db.ScheduleRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == req.RunId && r.SpaceId == req.SpaceId, ct);

        return run is null ? null : new ScheduleRunDto(
            run.Id, run.TriggerType.ToString(), run.Status.ToString(),
            run.BaselineVersionId, run.CreatedAt,
            run.StartedAt, run.FinishedAt, run.DurationMs,
            run.ResultSummaryJson, run.ErrorSummary,
            run.ProgressPhase);
    }
}
