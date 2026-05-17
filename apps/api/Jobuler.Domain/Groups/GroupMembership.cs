using Jobuler.Domain.Common;

namespace Jobuler.Domain.Groups;

public class GroupMembership : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid PersonId { get; private set; }
    public bool IsOwner { get; private set; }
    public DateTime JoinedAt { get; private set; } = DateTime.UtcNow;
    /// <summary>
    /// Home-leave priority multiplier. Default 1.0 = normal.
    /// Higher values (1.5, 2.0, 3.0) = more home time (parents, students).
    /// Lower values (0.5) = stays at base more (critical roles).
    /// </summary>
    public decimal HomeLeavePriority { get; private set; } = 1.0m;

    private GroupMembership() { }

    public static GroupMembership Create(Guid spaceId, Guid groupId, Guid personId, bool isOwner = false) =>
        new() { SpaceId = spaceId, GroupId = groupId, PersonId = personId, IsOwner = isOwner };

    public void SetOwner(bool isOwner) { IsOwner = isOwner; }

    public void SetHomeLeavePriority(decimal priority)
    {
        if (priority < 0.5m || priority > 3.0m)
            throw new InvalidOperationException("Home leave priority must be between 0.5 and 3.0.");
        HomeLeavePriority = priority;
    }
}
