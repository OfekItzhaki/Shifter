using Jobuler.Application.Scheduling;
using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.Commands;

public record DeletePresenceWindowCommand(
    Guid SpaceId, Guid PersonId, Guid WindowId) : IRequest;

public class DeletePresenceWindowCommandHandler : IRequestHandler<DeletePresenceWindowCommand>
{
    private readonly AppDbContext _db;
    private readonly ICumulativeTracker _cumulativeTracker;

    public DeletePresenceWindowCommandHandler(AppDbContext db, ICumulativeTracker cumulativeTracker)
    {
        _db = db;
        _cumulativeTracker = cumulativeTracker;
    }

    public async Task Handle(DeletePresenceWindowCommand req, CancellationToken ct)
    {
        var window = await _db.PresenceWindows
            .FirstOrDefaultAsync(w =>
                w.Id == req.WindowId &&
                w.PersonId == req.PersonId &&
                w.SpaceId == req.SpaceId &&
                !w.IsDerived, ct)
            ?? throw new KeyNotFoundException("Presence window not found.");

        var wasAtHome = window.State == PresenceState.AtHome;

        _db.PresenceWindows.Remove(window);
        await _db.SaveChangesAsync(ct);

        // Recompute cumulative hours when an AtHome window is deleted
        if (wasAtHome)
        {
            await _cumulativeTracker.RecomputeForPersonAsync(req.SpaceId, req.PersonId, ct);
        }
    }
}
