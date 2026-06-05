using Jobuler.Application.Common;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Commands;

public record CancelScheduleRunCommand(Guid SpaceId, Guid RunId, Guid RequestedByUserId) : IRequest;

public class CancelScheduleRunCommandHandler : IRequestHandler<CancelScheduleRunCommand>
{
    private readonly AppDbContext _db;

    public CancelScheduleRunCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task Handle(CancelScheduleRunCommand request, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                request.SpaceId.ToString(),
                request.RequestedByUserId.ToString());
        }

        var run = await _db.ScheduleRuns
            .FirstOrDefaultAsync(r => r.Id == request.RunId && r.SpaceId == request.SpaceId, ct);

        if (run is null)
            throw new KeyNotFoundException("Schedule run not found.");

        if (run.Status is ScheduleRunStatus.Completed or ScheduleRunStatus.Failed or ScheduleRunStatus.TimedOut)
        {
            return;
        }

        var drafts = await _db.ScheduleVersions
            .Where(v => v.SpaceId == request.SpaceId
                && v.SourceRunId == request.RunId
                && v.Status == ScheduleVersionStatus.Draft)
            .ToListAsync(ct);

        foreach (var draft in drafts)
        {
            draft.Discard();
        }

        run.MarkFailed("Scheduler run cancelled by admin.");
        await _db.SaveChangesAsync(ct);
    }
}
