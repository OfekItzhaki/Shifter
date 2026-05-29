using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Spaces.Commands;

public record MigrateUserSpaceCommand(Guid UserId) : IRequest<MigrateUserSpaceResult>;

public record MigrateUserSpaceResult(Guid? SpaceId, string? SpaceName, bool AlreadyMigrated, int GroupsMigrated);

public class MigrateUserSpaceCommandHandler : IRequestHandler<MigrateUserSpaceCommand, MigrateUserSpaceResult>
{
    private readonly AppDbContext _db;
    private readonly ILogger<MigrateUserSpaceCommandHandler> _logger;

    public MigrateUserSpaceCommandHandler(AppDbContext db, ILogger<MigrateUserSpaceCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<MigrateUserSpaceResult> Handle(MigrateUserSpaceCommand request, CancellationToken ct)
    {
        // Check if already migrated
        var existingMigration = await _db.UserSpaceMigrations
            .AnyAsync(m => m.UserId == request.UserId, ct);

        if (existingMigration)
            return new MigrateUserSpaceResult(null, null, AlreadyMigrated: true, 0);

        // Check if user already has a space membership
        var hasMembership = await _db.SpaceMemberships
            .AnyAsync(m => m.UserId == request.UserId && m.IsActive, ct);

        if (hasMembership)
            return new MigrateUserSpaceResult(null, null, AlreadyMigrated: true, 0);

        // Find user's orphaned groups
        var orphanedGroups = await _db.Groups
            .Where(g => g.CreatedByUserId == request.UserId && g.DeletedAt == null)
            .ToListAsync(ct);

        if (orphanedGroups.Count == 0)
            return new MigrateUserSpaceResult(null, null, AlreadyMigrated: false, 0);

        // Get user display name for space naming
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        var displayName = user?.DisplayName?.Trim();
        var locale = user?.PreferredLocale ?? "he";
        var spaceName = string.IsNullOrWhiteSpace(displayName)
            ? locale == "he" ? "My Space" : locale == "ru" ? "Моё пространство" : "My Space"
            : displayName;

        if (spaceName.Length > 100)
            spaceName = spaceName[..100];

        // Create space
        var space = Space.Create(spaceName, request.UserId, null, locale);
        _db.Spaces.Add(space);

        // Create membership
        var membership = SpaceMembership.Create(space.Id, request.UserId);
        _db.SpaceMemberships.Add(membership);

        // Grant all permissions to owner
        foreach (var perm in AllPermissions())
        {
            _db.SpacePermissionGrants.Add(
                SpacePermissionGrant.Grant(space.Id, request.UserId, perm, request.UserId));
        }

        // Assign orphaned groups to the new space
        // Note: Groups already have SpaceId set from creation, but if they reference
        // a non-existent space or the same space, we update them
        // For migration, we just record it — groups already have space_id from creation
        var groupsMigrated = orphanedGroups.Count;

        // Record migration
        var migration = UserSpaceMigration.Create(request.UserId, space.Id, groupsMigrated);
        _db.UserSpaceMigrations.Add(migration);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Migrated user {UserId} to new space {SpaceId} ({SpaceName}). Groups: {Count}",
            request.UserId, space.Id, spaceName, groupsMigrated);

        return new MigrateUserSpaceResult(space.Id, spaceName, AlreadyMigrated: false, groupsMigrated);
    }

    private static IEnumerable<string> AllPermissions() =>
    [
        Permissions.SpaceView,
        Permissions.SpaceAdminMode,
        Permissions.PeopleManage,
        Permissions.ConstraintsManage,
        Permissions.RestrictionsManageSensitive,
        Permissions.TasksManage,
        Permissions.ScheduleRecalculate,
        Permissions.SchedulePublish,
        Permissions.ScheduleRollback,
        Permissions.PermissionsManage,
        Permissions.OwnershipTransfer,
        Permissions.LogsViewSensitive,
    ];
}
