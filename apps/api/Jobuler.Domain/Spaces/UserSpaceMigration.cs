using Jobuler.Domain.Common;

namespace Jobuler.Domain.Spaces;

/// <summary>
/// Tracks one-time migration of existing users' groups into a newly created Space.
/// Each user can only be migrated once.
/// </summary>
public class UserSpaceMigration : Entity
{
    public Guid UserId { get; private set; }
    public Guid SpaceId { get; private set; }
    public DateTime MigratedAt { get; private set; } = DateTime.UtcNow;
    public int GroupsMigrated { get; private set; }

    private UserSpaceMigration() { }

    public static UserSpaceMigration Create(Guid userId, Guid spaceId, int groupsMigrated) =>
        new()
        {
            UserId = userId,
            SpaceId = spaceId,
            GroupsMigrated = groupsMigrated
        };
}
