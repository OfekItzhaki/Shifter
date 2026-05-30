using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Spaces.Commands;

public record MigrateUserSpaceCommand(Guid UserId) : IRequest<MigrateUserSpaceResult>;

public record MigrateUserSpaceResult(Guid SpaceId, string SpaceName, int GroupsMigrated);

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
        // Check if already migrated — throw if migration record exists
        var alreadyMigrated = await _db.UserSpaceMigrations
            .AnyAsync(m => m.UserId == request.UserId, ct);

        if (alreadyMigrated)
            throw new InvalidOperationException("User has already been migrated.");

        // Find all groups where this user is a member (via group memberships)
        // Person.LinkedUserId links a Person to an auth User
        var personIds = await _db.People
            .Where(p => p.LinkedUserId == request.UserId)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var groupIds = await _db.GroupMemberships
            .Where(gm => personIds.Contains(gm.PersonId))
            .Select(gm => gm.GroupId)
            .Distinct()
            .ToListAsync(ct);

        var groups = await _db.Groups
            .Where(g => groupIds.Contains(g.Id) && g.DeletedAt == null)
            .ToListAsync(ct);

        // Get user display name for space naming
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        var displayName = user?.DisplayName?.Trim();
        var locale = user?.PreferredLocale ?? "he";
        var spaceName = string.IsNullOrWhiteSpace(displayName)
            ? "My Space"
            : $"{displayName}'s Space";

        if (spaceName.Length > 100)
            spaceName = spaceName[..100];

        // Wrap everything in a transaction
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Create a new Space for the user
                var space = Space.Create(spaceName, request.UserId, null, locale);
                _db.Spaces.Add(space);

                // Create a SpaceMembership for the user in the new space
                var membership = SpaceMembership.Create(space.Id, request.UserId);
                _db.SpaceMemberships.Add(membership);

                // Grant owner permissions
                foreach (var perm in AllPermissions())
                {
                    _db.SpacePermissionGrants.Add(
                        SpacePermissionGrant.Grant(space.Id, request.UserId, perm, request.UserId));
                }

                // Move all groups into the new space (update their SpaceId)
                foreach (var group in groups)
                {
                    group.ReassignToSpace(space.Id);
                }

                // Record the migration
                var migration = UserSpaceMigration.Create(request.UserId, space.Id, groups.Count);
                _db.UserSpaceMigrations.Add(migration);

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Migration completed for user {UserId}. Created space {SpaceId} ({SpaceName}), migrated {GroupCount} groups.",
                    request.UserId, space.Id, spaceName, groups.Count);

                return new MigrateUserSpaceResult(space.Id, spaceName, groups.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);

                _logger.LogError(ex,
                    "Migration failed for user {UserId}. Transaction rolled back. Error: {Message}",
                    request.UserId, ex.Message);

                throw;
            }
        });
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
