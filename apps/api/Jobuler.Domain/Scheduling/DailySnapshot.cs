using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// Immutable per-person-per-day record of an assignment.
/// Enables historical viewing and incremental statistics computation.
/// Past-dated snapshots are never replaced or modified.
/// </summary>
public class DailySnapshot : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid PersonId { get; private set; }
    public Guid PeriodId { get; private set; }
    public DateOnly SnapshotDate { get; private set; }
    public Guid? TaskTypeId { get; private set; }
    public Guid? SlotId { get; private set; }
    public DateTime? ShiftStart { get; private set; }
    public DateTime? ShiftEnd { get; private set; }
    public string? BurdenLevel { get; private set; }
    public Guid VersionId { get; private set; }

    public bool IsPast => SnapshotDate < DateOnly.FromDateTime(DateTime.UtcNow);

    private DailySnapshot() { }

    public static DailySnapshot Create(
        Guid spaceId,
        Guid groupId,
        Guid personId,
        Guid periodId,
        DateOnly snapshotDate,
        Guid? taskTypeId,
        Guid? slotId,
        DateTime? shiftStart,
        DateTime? shiftEnd,
        string? burdenLevel,
        Guid versionId) => new()
    {
        SpaceId = spaceId,
        GroupId = groupId,
        PersonId = personId,
        PeriodId = periodId,
        SnapshotDate = snapshotDate,
        TaskTypeId = taskTypeId,
        SlotId = slotId,
        ShiftStart = shiftStart,
        ShiftEnd = shiftEnd,
        BurdenLevel = burdenLevel,
        VersionId = versionId
    };
}
