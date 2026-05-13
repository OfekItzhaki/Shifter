using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Identity;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

// ── Get Join Code ─────────────────────────────────────────────────────────────

public record GetJoinCodeQuery(Guid SpaceId, Guid GroupId) : IRequest<string?>;

public class GetJoinCodeQueryHandler : IRequestHandler<GetJoinCodeQuery, string?>
{
    private readonly AppDbContext _db;
    public GetJoinCodeQueryHandler(AppDbContext db) => _db = db;

    public async Task<string?> Handle(GetJoinCodeQuery req, CancellationToken ct)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(
            g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct);
        return group?.JoinCode;
    }
}

// ── Regenerate Join Code ──────────────────────────────────────────────────────

public record RegenerateJoinCodeCommand(Guid SpaceId, Guid GroupId) : IRequest<string>;

public class RegenerateJoinCodeCommandHandler : IRequestHandler<RegenerateJoinCodeCommand, string>
{
    private readonly AppDbContext _db;
    public RegenerateJoinCodeCommandHandler(AppDbContext db) => _db = db;

    public async Task<string> Handle(RegenerateJoinCodeCommand req, CancellationToken ct)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(
            g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Group not found");

        var newCode = group.RegenerateJoinCode();
        await _db.SaveChangesAsync(ct);
        return newCode;
    }
}

// ── Join Group by Code ────────────────────────────────────────────────────────

public record JoinGroupByCodeCommand(string Code, Guid UserId) : IRequest<JoinGroupResult>;

public record JoinGroupResult(Guid GroupId, Guid SpaceId, string GroupName);

public class JoinGroupByCodeCommandHandler : IRequestHandler<JoinGroupByCodeCommand, JoinGroupResult>
{
    private readonly AppDbContext _db;
    public JoinGroupByCodeCommandHandler(AppDbContext db) => _db = db;

    public async Task<JoinGroupResult> Handle(JoinGroupByCodeCommand req, CancellationToken ct)
    {
        var code = req.Code.Trim().ToUpperInvariant();

        var group = await _db.Groups.FirstOrDefaultAsync(
            g => g.JoinCode == code && g.DeletedAt == null && g.IsActive, ct)
            ?? throw new KeyNotFoundException("Invalid join code");

        // Find or create the person for this user in this space
        var person = await _db.People.FirstOrDefaultAsync(
            p => p.SpaceId == group.SpaceId && p.LinkedUserId == req.UserId, ct);

        if (person == null)
        {
            // Get user display name
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct)
                ?? throw new UnauthorizedAccessException("User not found");

            person = Person.Create(group.SpaceId, user.DisplayName ?? user.Email, linkedUserId: req.UserId);
            _db.People.Add(person);
            await _db.SaveChangesAsync(ct);
        }

        // Check if already a member
        var alreadyMember = await _db.GroupMemberships.AnyAsync(
            gm => gm.GroupId == group.Id && gm.PersonId == person.Id, ct);

        if (!alreadyMember)
        {
            _db.GroupMemberships.Add(GroupMembership.Create(group.SpaceId, group.Id, person.Id));
        }

        // Ensure SpaceMembership exists (idempotent — skip if already present)
        var hasSpaceMembership = await _db.SpaceMemberships.AnyAsync(
            sm => sm.UserId == req.UserId && sm.SpaceId == group.SpaceId, ct);

        if (!hasSpaceMembership)
        {
            _db.SpaceMemberships.Add(SpaceMembership.Create(group.SpaceId, req.UserId));
            _db.SpacePermissionGrants.Add(
                SpacePermissionGrant.Grant(group.SpaceId, req.UserId, Permissions.SpaceView, req.UserId));
        }

        await _db.SaveChangesAsync(ct);

        return new JoinGroupResult(group.Id, group.SpaceId, group.Name);
    }
}
