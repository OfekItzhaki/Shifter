using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public class JoinSpaceByInviteCodeCommandHandler : IRequestHandler<JoinSpaceByInviteCodeCommand, JoinSpaceResult>
{
    private readonly AppDbContext _db;

    public JoinSpaceByInviteCodeCommandHandler(AppDbContext db) => _db = db;

    public async Task<JoinSpaceResult> Handle(JoinSpaceByInviteCodeCommand request, CancellationToken ct)
    {
        var code = request.InviteCode.Trim().ToUpperInvariant();

        var space = await _db.Spaces
            .FirstOrDefaultAsync(s => s.InviteCode == code && s.IsActive && s.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Invalid invite code.");

        // Check if already a member
        var existingMembership = await _db.SpaceMemberships
            .FirstOrDefaultAsync(m => m.SpaceId == space.Id && m.UserId == request.UserId && m.IsActive, ct);

        if (existingMembership is not null)
            return new JoinSpaceResult(space.Id, space.Name, AlreadyMember: true);

        // Create membership
        var membership = SpaceMembership.Create(space.Id, request.UserId);
        _db.SpaceMemberships.Add(membership);

        // Grant space.view permission
        var grant = SpacePermissionGrant.Grant(space.Id, request.UserId, Permissions.SpaceView, space.OwnerUserId);
        _db.SpacePermissionGrants.Add(grant);

        await _db.SaveChangesAsync(ct);

        return new JoinSpaceResult(space.Id, space.Name, AlreadyMember: false);
    }
}
