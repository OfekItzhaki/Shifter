using Jobuler.Application.Common;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace Jobuler.Application.Groups.Commands;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record GroupRoleDto(Guid Id, string Name, string? Description, bool IsActive, string PermissionLevel, bool IsDefault = false);

// ── Create ────────────────────────────────────────────────────────────────────

public record CreateGroupRoleCommand(
    Guid SpaceId,
    Guid GroupId,
    string Name,
    string? Description,
    string PermissionLevel,
    Guid RequestingUserId) : IRequest<Guid>;

public class CreateGroupRoleCommandHandler : IRequestHandler<CreateGroupRoleCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public CreateGroupRoleCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreateGroupRoleCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.PeopleManage, ct);

        // Verify group exists in this space
        var groupExists = await _db.Groups.AsNoTracking()
            .AnyAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct);
        if (!groupExists)
            throw new KeyNotFoundException("Group not found in this space.");

        // Check for duplicate name within the group
        // If a deactivated role with the same name exists, reactivate it instead of creating a new one
        var existingRole = await _db.SpaceRoles
            .FirstOrDefaultAsync(r => r.SpaceId == req.SpaceId
                && r.GroupId == req.GroupId
                && r.Name == req.Name.Trim(), ct);

        if (existingRole is not null)
        {
            if (existingRole.IsActive)
                throw new ConflictException($"A role named '{req.Name.Trim()}' already exists in this group.");

            // Reactivate the deactivated role with the new settings
            var permLevelReactivate = Enum.TryParse<RolePermissionLevel>(req.PermissionLevel, true, out var plr)
                ? plr : RolePermissionLevel.View;
            existingRole.Update(req.Name, req.Description, permLevelReactivate);
            // Re-enable it by setting IsActive back to true via a new method
            existingRole.Reactivate();
            await _db.SaveChangesAsync(ct);
            return existingRole.Id;
        }

        var permLevel = Enum.TryParse<RolePermissionLevel>(req.PermissionLevel, true, out var pl)
            ? pl : RolePermissionLevel.View;

        var role = SpaceRole.CreateForGroup(req.SpaceId, req.GroupId, req.Name, req.RequestingUserId, req.Description, permLevel);
        _db.SpaceRoles.Add(role);
        await _db.SaveChangesAsync(ct);
        return role.Id;
    }
}

// ── Update ────────────────────────────────────────────────────────────────────

public record UpdateGroupRoleCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RoleId,
    string Name,
    string? Description,
    string PermissionLevel,
    Guid RequestingUserId) : IRequest;

public class UpdateGroupRoleCommandHandler : IRequestHandler<UpdateGroupRoleCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public UpdateGroupRoleCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(UpdateGroupRoleCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.PeopleManage, ct);

        var role = await _db.SpaceRoles
            .FirstOrDefaultAsync(r => r.Id == req.RoleId
                && r.SpaceId == req.SpaceId
                && r.GroupId == req.GroupId, ct)
            ?? throw new KeyNotFoundException("Role not found in this group.");

        var permLevel = Enum.TryParse<RolePermissionLevel>(req.PermissionLevel, true, out var pl)
            ? pl : RolePermissionLevel.View;
        role.Update(req.Name, req.Description, permLevel);
        await _db.SaveChangesAsync(ct);
    }
}

// ── Deactivate ────────────────────────────────────────────────────────────────

public record DeactivateGroupRoleCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RoleId,
    Guid RequestingUserId) : IRequest;

public class DeactivateGroupRoleCommandHandler : IRequestHandler<DeactivateGroupRoleCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public DeactivateGroupRoleCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(DeactivateGroupRoleCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.PeopleManage, ct);

        var role = await _db.SpaceRoles
            .FirstOrDefaultAsync(r => r.Id == req.RoleId
                && r.SpaceId == req.SpaceId
                && r.GroupId == req.GroupId, ct)
            ?? throw new KeyNotFoundException("Role not found in this group.");

        role.Deactivate();
        await _db.SaveChangesAsync(ct);
    }
}

// ── Assign / Update member role ───────────────────────────────────────────────

/// <summary>
/// Assigns or replaces a member's role within a group.
/// Only the group owner may call this.
/// Pass RoleId = null to remove the member's current role assignment.
/// </summary>
public record UpdateMemberRoleCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid PersonId,
    Guid? RoleId,
    Guid RequestingUserId) : IRequest;

public class UpdateMemberRoleCommandHandler : IRequestHandler<UpdateMemberRoleCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public UpdateMemberRoleCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(UpdateMemberRoleCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.PeopleManage, ct);

        // Only the group owner may assign roles
        var requestingPerson = await _db.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SpaceId == req.SpaceId && p.LinkedUserId == req.RequestingUserId, ct);

        var isGroupOwner = requestingPerson is not null && await _db.GroupMemberships.AsNoTracking()
            .AnyAsync(m => m.GroupId == req.GroupId && m.PersonId == requestingPerson.Id && m.IsOwner, ct);

        if (!isGroupOwner)
            throw new UnauthorizedAccessException("Only the group owner can assign or change member roles.");

        // Verify the target person is a member of this group
        var membershipExists = await _db.GroupMemberships.AsNoTracking()
            .AnyAsync(m => m.GroupId == req.GroupId && m.PersonId == req.PersonId && m.SpaceId == req.SpaceId, ct);
        if (!membershipExists)
            throw new KeyNotFoundException("Person is not a member of this group.");

        // Validate the new role belongs to this group (if provided)
        if (req.RoleId.HasValue)
        {
            var roleExists = await _db.SpaceRoles.AsNoTracking()
                .AnyAsync(r => r.Id == req.RoleId.Value
                    && r.SpaceId == req.SpaceId
                    && r.GroupId == req.GroupId
                    && r.IsActive, ct);
            if (!roleExists)
                throw new KeyNotFoundException("Role not found in this group.");
        }

        // Remove all existing group-scoped role assignments for this person in this group
        var existing = await _db.PersonRoleAssignments
            .Where(a => a.PersonId == req.PersonId && a.GroupId == req.GroupId)
            .ToListAsync(ct);
        _db.PersonRoleAssignments.RemoveRange(existing);

        // Assign the new role (if not null)
        if (req.RoleId.HasValue)
            _db.PersonRoleAssignments.Add(
                PersonRoleAssignment.Create(req.SpaceId, req.PersonId, req.RoleId.Value, req.GroupId));

        await _db.SaveChangesAsync(ct);
    }
}
