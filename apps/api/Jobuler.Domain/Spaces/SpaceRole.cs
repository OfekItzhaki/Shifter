using Jobuler.Domain.Common;

namespace Jobuler.Domain.Spaces;

/// <summary>
/// Permission level for a group role.
/// View = read-only access to group data.
/// ViewAndEdit = can edit schedule, tasks, and constraints.
/// Owner = full control including member management and settings.
/// </summary>
public enum RolePermissionLevel { View, ViewAndEdit, Owner }

/// <summary>
/// Dynamic operational role within a space or group (Soldier, Medic, Squad Commander, etc.).
/// Roles are data, not hardcoded enums.
/// When GroupId is set, the role belongs to that group only.
/// When GroupId is null, the role is space-level (legacy / shared).
///
/// IsDefault = true marks the system-created "Member" role that every group gets on creation.
/// It cannot be deleted (only renamed) and is automatically assigned to members added by non-owners.
/// </summary>
public class SpaceRole : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid? GroupId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsDefault { get; private set; } = false;
    public Guid? CreatedByUserId { get; private set; }
    public RolePermissionLevel PermissionLevel { get; private set; } = RolePermissionLevel.View;

    private SpaceRole() { }

    /// <summary>Creates a space-level role (not scoped to a group).</summary>
    public static SpaceRole Create(Guid spaceId, string name, Guid createdByUserId, string? description = null) =>
        new()
        {
            SpaceId = spaceId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedByUserId = createdByUserId
        };

    /// <summary>Creates a group-scoped role visible only within that group.</summary>
    public static SpaceRole CreateForGroup(
        Guid spaceId, Guid groupId, string name, Guid createdByUserId,
        string? description = null,
        RolePermissionLevel permissionLevel = RolePermissionLevel.View,
        bool isDefault = false) =>
        new()
        {
            SpaceId = spaceId,
            GroupId = groupId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedByUserId = createdByUserId,
            PermissionLevel = permissionLevel,
            IsDefault = isDefault
        };

    public void Update(string name, string? description, RolePermissionLevel? permissionLevel = null)
    {
        Name = name.Trim();
        Description = description?.Trim();
        if (permissionLevel.HasValue) PermissionLevel = permissionLevel.Value;
        Touch();
    }

    public void Deactivate()
    {
        if (IsDefault)
            throw new InvalidOperationException("The default member role cannot be deleted. You may rename it.");
        IsActive = false;
        Touch();
    }

    public void Reactivate() { IsActive = true; Touch(); }
}
