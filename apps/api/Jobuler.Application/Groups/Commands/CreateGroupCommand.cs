using Jobuler.Application.Scheduling;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

public record CreateGroupTypeCommand(
    Guid SpaceId, string Name, string? Description) : IRequest<Guid>;

public class CreateGroupTypeCommandHandler : IRequestHandler<CreateGroupTypeCommand, Guid>
{
    private readonly AppDbContext _db;
    public CreateGroupTypeCommandHandler(AppDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateGroupTypeCommand req, CancellationToken ct)
    {
        var gt = GroupType.Create(req.SpaceId, req.Name, req.Description);
        _db.GroupTypes.Add(gt);
        await _db.SaveChangesAsync(ct);
        return gt.Id;
    }
}

public record CreateGroupCommand(
    Guid SpaceId, Guid? GroupTypeId, string Name, string? Description,
    Guid CreatedByUserId) : IRequest<Guid>;

public class CreateGroupCommandHandler : IRequestHandler<CreateGroupCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IPeriodManager _periodManager;

    public CreateGroupCommandHandler(AppDbContext db, IPeriodManager periodManager)
    {
        _db = db;
        _periodManager = periodManager;
    }

    public async Task<Guid> Handle(CreateGroupCommand req, CancellationToken ct)
    {
        // Find or create the person linked to the creator's user account
        var person = await _db.People
            .FirstOrDefaultAsync(p => p.SpaceId == req.SpaceId && p.LinkedUserId == req.CreatedByUserId, ct);

        if (person is null)
        {
            // Auto-create a person record linked to this user
            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == req.CreatedByUserId, ct)
                ?? throw new InvalidOperationException("User not found.");
            person = Person.Create(req.SpaceId, user.DisplayName ?? user.Email, null, user.Id);
            _db.People.Add(person);
        }

        var group = Group.Create(req.SpaceId, req.GroupTypeId, req.Name, req.Description, createdByUserId: req.CreatedByUserId);
        _db.Groups.Add(group);
        // Save group first so the FK constraint on group_memberships is satisfied
        await _db.SaveChangesAsync(ct);

        // Auto-create the default "Member" role for this group.
        // This role has no permissions, cannot be deleted, and is assigned to members
        // added by non-owner admins. The owner can rename it but not remove it.
        var defaultRole = SpaceRole.CreateForGroup(
            req.SpaceId, group.Id, "Member", req.CreatedByUserId,
            description: "Default role with no permissions",
            permissionLevel: RolePermissionLevel.View,
            isDefault: true);
        _db.SpaceRoles.Add(defaultRole);

        _db.GroupMemberships.Add(GroupMembership.Create(req.SpaceId, group.Id, person.Id, isOwner: true));

        // Auto-create a trial subscription for the new group
        var subscription = Jobuler.Domain.Billing.GroupSubscription.CreateTrial(req.SpaceId, group.Id);
        _db.GroupSubscriptions.Add(subscription);

        await _db.SaveChangesAsync(ct);

        // Open a subscription period for the new group (trial counts as active)
        await _periodManager.OpenPeriodAsync(req.SpaceId, group.Id, ct);

        return group.Id;
    }
}

public record AddPersonToGroupCommand(
    Guid SpaceId, Guid GroupId, Guid PersonId) : IRequest;

public class AddPersonToGroupCommandHandler : IRequestHandler<AddPersonToGroupCommand>
{
    private readonly AppDbContext _db;
    public AddPersonToGroupCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(AddPersonToGroupCommand req, CancellationToken ct)
    {
        var exists = await _db.GroupMemberships.AnyAsync(
            m => m.GroupId == req.GroupId && m.PersonId == req.PersonId, ct);
        if (exists) return;

        _db.GroupMemberships.Add(GroupMembership.Create(req.SpaceId, req.GroupId, req.PersonId));
        await _db.SaveChangesAsync(ct);

        // Update peak member count for billing
        var memberCount = await _db.GroupMemberships.CountAsync(m => m.GroupId == req.GroupId, ct);
        var sub = await _db.GroupSubscriptions.FirstOrDefaultAsync(s => s.GroupId == req.GroupId, ct);
        if (sub != null)
        {
            sub.UpdatePeakMemberCount(memberCount);
            await _db.SaveChangesAsync(ct);
        }
    }
}
