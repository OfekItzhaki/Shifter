using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// A proposal to swap shifts between two members.
/// The initiator offers their shift and requests the target's shift in return.
/// Expires automatically after 72 hours if not accepted or declined.
/// </summary>
public class SwapRequest : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid InitiatorPersonId { get; private set; }
    public Guid TargetPersonId { get; private set; }
    public Guid InitiatorShiftRequestId { get; private set; }
    public Guid TargetShiftRequestId { get; private set; }
    public SwapRequestStatus Status { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    private SwapRequest() { }

    public static SwapRequest Create(
        Guid spaceId,
        Guid groupId,
        Guid initiatorPersonId,
        Guid targetPersonId,
        Guid initiatorShiftRequestId,
        Guid targetShiftRequestId)
    {
        return new()
        {
            SpaceId = spaceId,
            GroupId = groupId,
            InitiatorPersonId = initiatorPersonId,
            TargetPersonId = targetPersonId,
            InitiatorShiftRequestId = initiatorShiftRequestId,
            TargetShiftRequestId = targetShiftRequestId,
            Status = SwapRequestStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddHours(72)
        };
    }

    public void Accept()
    {
        if (Status != SwapRequestStatus.Pending)
            throw new InvalidOperationException("Only pending swap requests can be accepted.");

        Status = SwapRequestStatus.Accepted;
        Touch();
    }

    public void Decline()
    {
        if (Status != SwapRequestStatus.Pending)
            throw new InvalidOperationException("Only pending swap requests can be declined.");

        Status = SwapRequestStatus.Declined;
        Touch();
    }

    public void Cancel()
    {
        if (Status != SwapRequestStatus.Pending)
            throw new InvalidOperationException("Only pending swap requests can be cancelled.");

        Status = SwapRequestStatus.Cancelled;
        Touch();
    }

    public void Expire()
    {
        if (Status != SwapRequestStatus.Pending)
            throw new InvalidOperationException("Only pending swap requests can be expired.");

        Status = SwapRequestStatus.Expired;
        Touch();
    }
}
