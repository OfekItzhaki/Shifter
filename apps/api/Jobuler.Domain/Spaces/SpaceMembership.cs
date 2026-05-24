using Jobuler.Domain.Common;

namespace Jobuler.Domain.Spaces;

public class SpaceMembership : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime JoinedAt { get; private set; } = DateTime.UtcNow;
    public bool IsActive { get; private set; } = true;
    public SpacePermissionLevel PermissionLevel { get; private set; } = SpacePermissionLevel.Member;

    private SpaceMembership() { }

    public static SpaceMembership Create(Guid spaceId, Guid userId) =>
        new() { SpaceId = spaceId, UserId = userId };

    public void Deactivate() => IsActive = false;

    public void SetPermissionLevel(SpacePermissionLevel level)
    {
        PermissionLevel = level;
    }
}
