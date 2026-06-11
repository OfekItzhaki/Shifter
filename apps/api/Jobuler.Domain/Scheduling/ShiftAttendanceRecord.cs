using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

public enum ShiftAttendanceStatus
{
    Present,
    NoShow,
    Excused
}

/// <summary>
/// Admin-confirmed attendance outcome for one approved self-service shift assignment.
/// </summary>
public class ShiftAttendanceRecord : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid SchedulingCycleId { get; private set; }
    public Guid ShiftRequestId { get; private set; }
    public Guid ShiftSlotId { get; private set; }
    public Guid PersonId { get; private set; }
    public ShiftAttendanceStatus Status { get; private set; }
    public string? Note { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTime RecordedAt { get; private set; }

    private ShiftAttendanceRecord() { }

    public static ShiftAttendanceRecord Create(
        Guid spaceId,
        Guid groupId,
        Guid schedulingCycleId,
        Guid shiftRequestId,
        Guid shiftSlotId,
        Guid personId,
        ShiftAttendanceStatus status,
        Guid recordedByUserId,
        string? note = null)
    {
        var record = new ShiftAttendanceRecord
        {
            SpaceId = spaceId,
            GroupId = groupId,
            SchedulingCycleId = schedulingCycleId,
            ShiftRequestId = shiftRequestId,
            ShiftSlotId = shiftSlotId,
            PersonId = personId,
            RecordedByUserId = recordedByUserId
        };

        record.Update(status, recordedByUserId, note);
        return record;
    }

    public void Update(ShiftAttendanceStatus status, Guid recordedByUserId, string? note = null)
    {
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalizedNote?.Length > 500)
            throw new InvalidOperationException("Attendance note must be 500 characters or less.");

        Status = status;
        Note = normalizedNote;
        RecordedByUserId = recordedByUserId;
        RecordedAt = DateTime.UtcNow;
        Touch();
    }
}
