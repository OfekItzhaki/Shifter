using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace Jobuler.Application.Groups.Commands;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record GroupRoleDto(Guid Id, string Name, string? Description, bool IsActive, string PermissionLevel);

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
        var duplicate = await _db.SpaceRoles.AsNoTracking()
            .AnyAsync(r => r.SpaceId == req.SpaceId
                && r.GroupId == req.GroupId
                && r.Name == req.Name.Trim()
                && r.IsActive, ct);
        if (duplicate)
            throw new ConflictException($"A role named '{req.Name.Trim()}' already exists in this group.");

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
