using Jobuler.Application.Scheduling;
using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.Commands;

public record AddPresenceWindowCommand(
    Guid SpaceId, Guid PersonId,
    string State,  // free_in_base | at_home
    DateTime StartsAt, DateTime EndsAt,
    string? Note, Guid RequestingUserId,
    Guid? ReasonId = null) : IRequest<Guid>;

public class AddPresenceWindowCommandHandler
    : IRequestHandler<AddPresenceWindowCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICumulativeTracker _cumulativeTracker;

    public AddPresenceWindowCommandHandler(AppDbContext db, ICumulativeTracker cumulativeTracker)
    {
        _db = db;
        _cumulativeTracker = cumulativeTracker;
    }

    public async Task<Guid> Handle(AddPresenceWindowCommand req, CancellationToken ct)
    {
        var state = req.State switch
        {
            "at_home"      => PresenceState.AtHome,
            "free_in_base" => PresenceState.FreeInBase,
            _ => throw new ArgumentException($"Invalid presence state: {req.State}")
        };

        // Validate ReasonId if provided
        if (req.ReasonId.HasValue)
        {
            var reasonExists = await _db.UnavailabilityReasons.AsNoTracking()
                .AnyAsync(r => r.Id == req.ReasonId.Value
                    && r.SpaceId == req.SpaceId
                    && r.IsActive, ct);

            if (!reasonExists)
                throw new KeyNotFoundException(
                    $"Unavailability reason '{req.ReasonId.Value}' not found in space.");
        }

        var window = PresenceWindow.CreateManual(
            req.SpaceId, req.PersonId, state,
            req.StartsAt, req.EndsAt, req.Note,
            req.ReasonId);

        _db.PresenceWindows.Add(window);
        await _db.SaveChangesAsync(ct);

        // Recompute cumulative hours when an AtHome window is created
        if (state == PresenceState.AtHome)
        {
            await _cumulativeTracker.RecomputeForPersonAsync(req.SpaceId, req.PersonId, ct);
        }

        return window.Id;
    }
}
