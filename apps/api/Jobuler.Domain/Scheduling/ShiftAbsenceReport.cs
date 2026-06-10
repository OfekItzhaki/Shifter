using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

public enum ShiftAbsenceReportStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// A member report that they cannot attend an already-approved self-service shift.
/// Late reports are counted against the group's per-cycle absence limit.
/// </summary>
public class ShiftAbsenceReport : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid SchedulingCycleId { get; private set; }
    public Guid ShiftRequestId { get; private set; }
    public Guid ShiftSlotId { get; private set; }
    public Guid PersonId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public bool IsLate { get; private set; }
    public DateTime ReportedAt { get; private set; }
    public ShiftAbsenceReportStatus Status { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? AdminNote { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    private ShiftAbsenceReport() { }

    public static ShiftAbsenceReport Create(
        Guid spaceId,
        Guid groupId,
        Guid schedulingCycleId,
        Guid shiftRequestId,
        Guid shiftSlotId,
        Guid personId,
        string reason,
        bool isLate,
        DateTime reportedAt)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new InvalidOperationException("Absence reason must be between 1 and 500 characters.");

        return new ShiftAbsenceReport
        {
            SpaceId = spaceId,
            GroupId = groupId,
            SchedulingCycleId = schedulingCycleId,
            ShiftRequestId = shiftRequestId,
            ShiftSlotId = shiftSlotId,
            PersonId = personId,
            Reason = reason.Trim(),
            IsLate = isLate,
            ReportedAt = reportedAt,
            Status = ShiftAbsenceReportStatus.Pending
        };
    }

    public void Approve(Guid reviewedByUserId, string? adminNote = null)
    {
        if (Status != ShiftAbsenceReportStatus.Pending)
            throw new InvalidOperationException("Only pending absence reports can be approved.");

        Status = ShiftAbsenceReportStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
        ReviewedAt = DateTime.UtcNow;
        Touch();
    }

    public void Reject(Guid reviewedByUserId, string? adminNote = null)
    {
        if (Status != ShiftAbsenceReportStatus.Pending)
            throw new InvalidOperationException("Only pending absence reports can be rejected.");

        Status = ShiftAbsenceReportStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
        ReviewedAt = DateTime.UtcNow;
        Touch();
    }
}
