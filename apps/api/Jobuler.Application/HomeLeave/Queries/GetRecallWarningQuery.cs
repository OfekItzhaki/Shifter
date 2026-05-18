using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Queries;

public record RecallWarningResult(string Message);

public record GetRecallWarningQuery(
    Guid SpaceId,
    Guid PersonId,
    Guid PresenceWindowId) : IRequest<RecallWarningResult>;

public class GetRecallWarningQueryHandler : IRequestHandler<GetRecallWarningQuery, RecallWarningResult>
{
    private readonly AppDbContext _db;

    public GetRecallWarningQueryHandler(AppDbContext db) => _db = db;

    public async Task<RecallWarningResult> Handle(GetRecallWarningQuery req, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var window = await _db.PresenceWindows.AsNoTracking()
            .FirstOrDefaultAsync(pw =>
                pw.Id == req.PresenceWindowId
                && pw.SpaceId == req.SpaceId
                && pw.PersonId == req.PersonId
                && pw.State == PresenceState.AtHome, ct);

        if (window is null)
            throw new KeyNotFoundException("Home-leave presence window not found.");

        if (window.EndsAt <= now)
            throw new InvalidOperationException("Cannot recall a home-leave window that has already ended.");

        // Determine if the window is currently active (in-progress) vs future
        var isActive = window.StartsAt <= now && window.EndsAt > now;

        if (isActive)
        {
            return new RecallWarningResult(
                "This person is currently at home on approved leave. " +
                "Recalling them will require travel time before they can return to base. " +
                "Please confirm to proceed with the recall.");
        }

        return new RecallWarningResult(
            "This person has a scheduled future home leave. " +
            "Confirming will cancel the approved leave window.");
    }
}
