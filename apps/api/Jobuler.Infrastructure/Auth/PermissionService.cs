using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Infrastructure.Auth;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Permission keys that require Space Owner level (destructive / billing actions).
    /// </summary>
    private static readonly HashSet<string> OwnerOnlyPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        Permissions.OwnershipTransfer,
        Permissions.BillingManage,
        Permissions.PermissionsManage,
    };

    /// <summary>
    /// Permission keys granted to Admin level (management actions).
    /// </summary>
    private static readonly HashSet<string> AdminPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        Permissions.SpaceView,
        Permissions.SpaceAdminMode,
        Permissions.PeopleManage,
        Permissions.ConstraintsManage,
        Permissions.TasksManage,
        Permissions.ScheduleRecalculate,
        Permissions.SchedulePublish,
        Permissions.ScheduleRollback,
        Permissions.LogsViewSensitive,
        Permissions.RestrictionsManageSensitive,
    };

    public PermissionService(AppDbContext db) => _db = db;

    public async Task<bool> HasPermissionAsync(Guid userId, Guid spaceId, string permissionKey, CancellationToken ct = default)
    {
        // Tier 1: Space Owner — all permissions granted implicitly
        var isOwner = await _db.Spaces
            .AnyAsync(s => s.Id == spaceId && s.OwnerUserId == userId && s.IsActive, ct);
        if (isOwner) return true;

        // Load the user's space membership to determine their permission level
        var membership = await _db.SpaceMemberships
            .Where(m => m.SpaceId == spaceId && m.UserId == userId && m.IsActive)
            .Select(m => new { m.PermissionLevel })
            .FirstOrDefaultAsync(ct);

        if (membership == null) return false;

        // Tier 2: Admin — management permissions granted
        if (membership.PermissionLevel == SpacePermissionLevel.Admin)
        {
            // Admins get all admin-level permissions
            if (AdminPermissions.Contains(permissionKey))
                return true;

            // Admins do NOT get owner-only permissions
            if (OwnerOnlyPermissions.Contains(permissionKey))
                return false;

            // For any other permission key, fall through to explicit grants
        }

        // Tier 3: GroupOwner — group-scoped permissions for owned groups
        if (membership.PermissionLevel == SpacePermissionLevel.GroupOwner)
        {
            // GroupOwners get admin-level permissions scoped to their owned groups.
            // Since HasPermissionAsync doesn't have a groupId parameter, we grant
            // admin-level permissions if the user owns at least one group in the space.
            if (AdminPermissions.Contains(permissionKey))
            {
                var ownsGroup = await IsGroupOwnerInSpaceAsync(userId, spaceId, ct);
                if (ownsGroup) return true;
            }

            // GroupOwners do NOT get owner-only permissions
            if (OwnerOnlyPermissions.Contains(permissionKey))
                return false;

            // For any other permission key, fall through to explicit grants
        }

        // Tier 4: Member — check explicit SpacePermissionGrant rows
        return await _db.SpacePermissionGrants
            .AnyAsync(g =>
                g.SpaceId == spaceId &&
                g.UserId == userId &&
                g.PermissionKey == permissionKey &&
                g.RevokedAt == null, ct);
    }

    public async Task RequirePermissionAsync(Guid userId, Guid spaceId, string permissionKey, CancellationToken ct = default)
    {
        if (!await HasPermissionAsync(userId, spaceId, permissionKey, ct))
            throw new UnauthorizedAccessException($"Permission '{permissionKey}' is required.");
    }

    /// <summary>
    /// Checks whether the user owns at least one group in the given space.
    /// Group ownership is determined by GroupMembership.IsOwner where the
    /// Person.LinkedUserId matches the user.
    /// </summary>
    private async Task<bool> IsGroupOwnerInSpaceAsync(Guid userId, Guid spaceId, CancellationToken ct)
    {
        return await _db.GroupMemberships
            .Join(
                _db.People.Where(p => p.LinkedUserId == userId && p.SpaceId == spaceId),
                gm => gm.PersonId,
                p => p.Id,
                (gm, p) => gm)
            .AnyAsync(gm => gm.SpaceId == spaceId && gm.IsOwner, ct);
    }
}
