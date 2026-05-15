using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// Logical time segment tied to billing lifecycle that partitions cumulative data.
/// Allows clean resets when groups restart after billing lapses.
/// </summary>
public class SubscriptionPeriod : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public string Status { get; private set; } = "active";
    public DateTime StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }

    private SubscriptionPeriod() { }

    public static SubscriptionPeriod Create(Guid spaceId, Guid groupId) => new()
    {
        SpaceId = spaceId,
        GroupId = groupId,
        StartsAt = DateTime.UtcNow,
        Status = "active"
    };

    public void Close()
    {
        if (Status != "active")
            throw new InvalidOperationException("Can only close an active period.");
        Status = "closed";
        EndsAt = DateTime.UtcNow;
    }

    public bool IsActive => Status == "active";
}
