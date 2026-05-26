using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// A member's position in the waitlist for a full shift slot.
/// Ordered by request timestamp (first-come, first-served).
/// </summary>
public class WaitlistEntry : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid ShiftSlotId { get; private set; }
    public Guid PersonId { get; private set; }
    public int Position { get; private set; }
    public WaitlistEntryStatus Status { get; private set; }
    public DateTime? OfferedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    private WaitlistEntry() { }

    public static WaitlistEntry Create(
        Guid spaceId,
        Guid shiftSlotId,
        Guid personId,
        int position)
    {
        return new()
        {
            SpaceId = spaceId,
            ShiftSlotId = shiftSlotId,
            PersonId = personId,
            Position = position,
            Status = WaitlistEntryStatus.Waiting
        };
    }

    public void Offer(DateTime expiresAt)
    {
        Status = WaitlistEntryStatus.Offered;
        OfferedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
        Touch();
    }

    public void Accept()
    {
        Status = WaitlistEntryStatus.Accepted;
        Touch();
    }

    public void Expire()
    {
        Status = WaitlistEntryStatus.Expired;
        Touch();
    }

    public void Decline()
    {
        Status = WaitlistEntryStatus.Declined;
        Touch();
    }

    public void Remove()
    {
        Status = WaitlistEntryStatus.Removed;
        Touch();
    }
}
