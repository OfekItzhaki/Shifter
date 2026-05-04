using Jobuler.Application.Scheduling;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Commands;

public record TriggerSolverCommand(
    Guid SpaceId,
    string TriggerMode,        // standard | emergency
    Guid? RequestedByUserId,
    Guid? GroupId = null,
    DateTime? StartTime = null) : IRequest<Guid>;

public class TriggerSolverCommandHandler : IRequestHandler<TriggerSolverCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ISolverJobQueue _queue;

    public TriggerSolverCommandHandler(AppDbContext db, ISolverJobQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task<Guid> Handle(TriggerSolverCommand request, CancellationToken ct)
    {
        // Set PostgreSQL session variable so RLS policies allow queries on this space.
        // Skipped when using an in-memory provider (e.g. unit tests).
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                request.SpaceId.ToString(),
                request.RequestedByUserId?.ToString() ?? "");
        }

        // Find the current published version to use as baseline
        var baseline = await _db.ScheduleVersions
            .AsNoTracking()
            .Where(v => v.SpaceId == request.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        var trigger = request.TriggerMode == "emergency"
            ? ScheduleRunTrigger.Emergency
            : ScheduleRunTrigger.Standard;

        var run = ScheduleRun.Create(
            request.SpaceId, trigger, baseline?.Id, request.RequestedByUserId);

        _db.ScheduleRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        // Enqueue — worker picks it up asynchronously
        await _queue.EnqueueAsync(new SolverJobMessage(
            run.Id, request.SpaceId, request.TriggerMode,
            baseline?.Id, request.RequestedByUserId, request.GroupId, request.StartTime), ct);

        return run.Id;
    }
}
