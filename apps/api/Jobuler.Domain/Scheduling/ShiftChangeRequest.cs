using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

public enum ShiftChangeRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

/// <summary>
/// A member request to move an approved self-service shift to another slot.
/// </summary>
public class ShiftChangeRequest : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid SchedulingCycleId { get; private set; }
    public Guid ShiftRequestId { get; private set; }
    public Guid OriginalShiftSlotId { get; private set; }
    public Guid? RequestedShiftSlotId { get; private set; }
    public Guid PersonId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public ShiftChangeRequestStatus Status { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? AdminNote { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    private ShiftChangeRequest() { }

    public static ShiftChangeRequest Create(
        Guid spaceId,
        Guid groupId,
        Guid schedulingCycleId,
        Guid shiftRequestId,
        Guid originalShiftSlotId,
        Guid? requestedShiftSlotId,
        Guid personId,
        string reason,
        DateTime requestedAt)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new InvalidOperationException("Change request reason must be between 1 and 500 characters.");

        if (requestedShiftSlotId == originalShiftSlotId)
            throw new InvalidOperationException("Requested shift must be different from the current shift.");

        return new ShiftChangeRequest
        {
            SpaceId = spaceId,
            GroupId = groupId,
            SchedulingCycleId = schedulingCycleId,
            ShiftRequestId = shiftRequestId,
            OriginalShiftSlotId = originalShiftSlotId,
            RequestedShiftSlotId = requestedShiftSlotId,
            PersonId = personId,
            Reason = reason.Trim(),
            Status = ShiftChangeRequestStatus.Pending,
            RequestedAt = requestedAt
        };
    }

    public void Approve(Guid reviewedByUserId, string? adminNote = null)
    {
        if (Status != ShiftChangeRequestStatus.Pending)
            throw new InvalidOperationException("Only pending change requests can be approved.");

        Status = ShiftChangeRequestStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
        ReviewedAt = DateTime.UtcNow;
        Touch();
    }

    public void Reject(Guid reviewedByUserId, string? adminNote = null)
    {
        if (Status != ShiftChangeRequestStatus.Pending)
            throw new InvalidOperationException("Only pending change requests can be rejected.");

        Status = ShiftChangeRequestStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
        ReviewedAt = DateTime.UtcNow;
        Touch();
    }

    public void Cancel()
    {
        if (Status != ShiftChangeRequestStatus.Pending)
            throw new InvalidOperationException("Only pending change requests can be cancelled.");

        Status = ShiftChangeRequestStatus.Cancelled;
        Touch();
    }
}
