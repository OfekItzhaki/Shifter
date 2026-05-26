using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// A member's request to claim a specific shift slot.
/// Transitions through states: Pending → Approved or Rejected, and Approved → Cancelled.
/// </summary>
public class ShiftRequest : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid ShiftSlotId { get; private set; }
    public Guid PersonId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid SchedulingCycleId { get; private set; }
    public ShiftRequestStatus Status { get; private set; }
    public bool IsAdminOverride { get; private set; }
    public Guid? ProcessedByUserId { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    private ShiftRequest() { }

    public static ShiftRequest Create(
        Guid spaceId,
        Guid shiftSlotId,
        Guid personId,
        Guid groupId,
        Guid schedulingCycleId,
        bool isAdminOverride = false,
        Guid? processedByUserId = null) =>
        new()
        {
            SpaceId = spaceId,
            ShiftSlotId = shiftSlotId,
            PersonId = personId,
            GroupId = groupId,
            SchedulingCycleId = schedulingCycleId,
            Status = ShiftRequestStatus.Pending,
            IsAdminOverride = isAdminOverride,
            ProcessedByUserId = processedByUserId
        };

    public void Approve(Guid? processedByUserId = null)
    {
        if (Status != ShiftRequestStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be approved.");

        Status = ShiftRequestStatus.Approved;
        ProcessedByUserId = processedByUserId;
        Touch();
    }

    public void Reject(string reason, Guid? processedByUserId = null)
    {
        if (Status != ShiftRequestStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be rejected.");

        Status = ShiftRequestStatus.Rejected;
        RejectionReason = reason;
        ProcessedByUserId = processedByUserId;
        Touch();
    }

    public void Cancel(string reason)
    {
        if (Status != ShiftRequestStatus.Approved)
            throw new InvalidOperationException("Only approved requests can be cancelled.");

        Status = ShiftRequestStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
        Touch();
    }

    /// <summary>
    /// Reassigns this shift request to a different person and slot as part of a swap operation.
    /// Only approved requests can be reassigned.
    /// </summary>
    public void ReassignTo(Guid newPersonId, Guid newShiftSlotId)
    {
        if (Status != ShiftRequestStatus.Approved)
            throw new InvalidOperationException("Only approved requests can be reassigned.");

        PersonId = newPersonId;
        ShiftSlotId = newShiftSlotId;
        Touch();
    }
}
