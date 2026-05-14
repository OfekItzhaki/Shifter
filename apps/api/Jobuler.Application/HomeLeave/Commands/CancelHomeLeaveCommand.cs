using Jobuler.Application.Common;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Commands;

public record CancelHomeLeaveCommand(
    Guid SpaceId,
    Guid PersonId,
    Guid PresenceWindowId,
    Guid RequestingUserId) : IRequest<CancelHomeLeaveResult>;

public record CancelHomeLeaveResult(
    bool Deleted,
    bool Truncated,
    DateTime? TruncatedAt);

public class CancelHomeLeaveCommandHandler : IRequestHandler<CancelHomeLeaveCommand, CancelHomeLeaveResult>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public CancelHomeLeaveCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<CancelHomeLeaveResult> Handle(CancelHomeLeaveCommand req, CancellationToken ct)
    {
        // Set PostgreSQL session variables for RLS policies.
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                req.SpaceId.ToString(),
                req.RequestingUserId.ToString());
        }

        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.SchedulePublish, ct);

        // Load the presence window — must be AtHome, is_derived=true, belongs to space and person
        var window = await _db.PresenceWindows
            .FirstOrDefaultAsync(pw =>
                pw.Id == req.PresenceWindowId
                && pw.SpaceId == req.SpaceId
                && pw.PersonId == req.PersonId
                && pw.State == PresenceState.AtHome
                && pw.IsDerived == true, ct)
            ?? throw new KeyNotFoundException("Home-leave presence window not found.");

        var now = DateTime.UtcNow;

        if (window.StartsAt > now)
        {
            // Future window: delete entirely
            _db.PresenceWindows.Remove(window);
            await _db.SaveChangesAsync(ct);
            return new CancelHomeLeaveResult(Deleted: true, Truncated: false, TruncatedAt: null);
        }
        else if (window.EndsAt > now)
        {
            // In-progress window (starts_at in past, ends_at in future): truncate to now
            window.Truncate(now);
            await _db.SaveChangesAsync(ct);
            return new CancelHomeLeaveResult(Deleted: false, Truncated: true, TruncatedAt: now);
        }
        else
        {
            // Window is entirely in the past — nothing to cancel
            throw new InvalidOperationException("Cannot cancel a home-leave window that has already ended.");
        }
    }
}
