using Jobuler.Domain.Common;

namespace Jobuler.Domain.Spaces;

/// <summary>
/// Known permission keys. Stored as strings in DB so new permissions
/// can be added without a migration.
/// </summary>
public static class Permissions
{
    public const string SpaceView              = "space.view";
    public const string SpaceAdminMode         = "space.admin_mode";
    public const string PeopleManage           = "people.manage";
    public const string ConstraintsManage      = "constraints.manage";
    public const string RestrictionsManageSensitive = "restrictions.manage_sensitive";
    public const string TasksManage            = "tasks.manage";
    public const string ScheduleRecalculate    = "schedule.recalculate";
    public const string SchedulePublish        = "schedule.publish";
    public const string ScheduleRollback       = "schedule.rollback";
    public const string PermissionsManage      = "permissions.manage";
    public const string OwnershipTransfer      = "ownership.transfer";
    public const string LogsViewSensitive      = "logs.view_sensitive";
    public const string BillingManage           = "billing.manage";
}

public class SpacePermissionGrant : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid UserId { get; private set; }
    public string PermissionKey { get; private set; } = default!;
    public Guid? GrantedByUserId { get; private set; }
    public DateTime GrantedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt == null;

    private SpacePermissionGrant() { }

    public static SpacePermissionGrant Grant(Guid spaceId, Guid userId, string permissionKey, Guid grantedByUserId) =>
        new()
        {
            SpaceId = spaceId,
            UserId = userId,
            PermissionKey = permissionKey,
            GrantedByUserId = grantedByUserId
        };

    public void Revoke() => RevokedAt = DateTime.UtcNow;
}
