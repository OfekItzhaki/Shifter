using Jobuler.Application.Common;
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
    private readonly ICacheService _cache;

    public AddPresenceWindowCommandHandler(AppDbContext db, ICumulativeTracker cumulativeTracker, ICacheService cache)
    {
        _db = db;
        _cumulativeTracker = cumulativeTracker;
        _cache = cache;
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

        // Invalidate live status cache for all groups in this space
        // (presence windows affect live status across groups)
        await _cache.RemoveByPatternAsync($"status:{req.SpaceId}:*", ct);

        return window.Id;
    }
}
