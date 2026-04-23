using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

/// <summary>
/// Removes a person from a group. Used for opt-out via token or admin removal.
/// </summary>
public record LeaveGroupByTokenCommand(string OptOutToken) : IRequest<LeaveGroupResult>;
public record RemovePersonFromGroupCommand(Guid SpaceId, Guid GroupId, Guid PersonId) : IRequest;

public record LeaveGroupResult(bool Success, string? GroupName, string? SpaceName);

public class LeaveGroupByTokenCommandHandler : IRequestHandler<LeaveGroupByTokenCommand, LeaveGroupResult>
{
    private readonly AppDbContext _db;
    public LeaveGroupByTokenCommandHandler(AppDbContext db) => _db = db;

    public async Task<LeaveGroupResult> Handle(LeaveGroupByTokenCommand req, CancellationToken ct)
    {
        var invitation = await _db.GroupInvitations
            .FirstOrDefaultAsync(i => i.OptOutToken == req.OptOutToken && i.Status == "active", ct);

        if (invitation is null)
            return new LeaveGroupResult(false, null, null);

        // Mark opted out
        invitation.OptOut();

        // Remove membership
        if (invitation.PersonId.HasValue)
        {
            var membership = await _db.GroupMemberships
                .FirstOrDefaultAsync(m => m.GroupId == invitation.GroupId && m.PersonId == invitation.PersonId, ct);
            if (membership is not null)
                _db.GroupMemberships.Remove(membership);
        }

        var group = await _db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == invitation.GroupId, ct);

        await _db.SaveChangesAsync(ct);
        return new LeaveGroupResult(true, group?.Name, null);
    }
}

public class RemovePersonFromGroupCommandHandler : IRequestHandler<RemovePersonFromGroupCommand>
{
    private readonly AppDbContext _db;
    public RemovePersonFromGroupCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(RemovePersonFromGroupCommand req, CancellationToken ct)
    {
        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == req.GroupId && m.PersonId == req.PersonId
                && m.SpaceId == req.SpaceId, ct);
        if (membership is null) return;

        _db.GroupMemberships.Remove(membership);

        // Mark any active invitations as opted_out so re-invite is required
        var invitations = await _db.GroupInvitations
            .Where(i => i.GroupId == req.GroupId && i.PersonId == req.PersonId && i.Status == "active")
            .ToListAsync(ct);
        foreach (var inv in invitations) inv.OptOut();

        await _db.SaveChangesAsync(ct);
    }
}
