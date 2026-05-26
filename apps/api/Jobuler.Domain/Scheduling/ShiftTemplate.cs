using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// Defines a recurring weekly shift pattern for slot generation in self-service scheduling.
/// Each template specifies a day of week, time window, and required headcount.
/// Soft-deleted templates are excluded from future slot generation.
/// </summary>
public class ShiftTemplate : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid GroupTaskId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int RequiredHeadcount { get; private set; }
    public bool IsDeleted { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private ShiftTemplate() { }

    public static ShiftTemplate Create(
        Guid spaceId,
        Guid groupId,
        Guid groupTaskId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        int requiredHeadcount,
        Guid? createdByUserId)
    {
        ValidateTimeRange(startTime, endTime);
        ValidateHeadcount(requiredHeadcount);

        return new ShiftTemplate
        {
            SpaceId = spaceId,
            GroupId = groupId,
            GroupTaskId = groupTaskId,
            DayOfWeek = dayOfWeek,
            StartTime = startTime,
            EndTime = endTime,
            RequiredHeadcount = requiredHeadcount,
            IsDeleted = false,
            CreatedByUserId = createdByUserId
        };
    }

    public void Update(
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        int requiredHeadcount,
        Guid? groupTaskId = null)
    {
        ValidateTimeRange(startTime, endTime);
        ValidateHeadcount(requiredHeadcount);

        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        RequiredHeadcount = requiredHeadcount;

        if (groupTaskId.HasValue)
            GroupTaskId = groupTaskId.Value;

        Touch();
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        Touch();
    }

    private static void ValidateTimeRange(TimeOnly startTime, TimeOnly endTime)
    {
        if (startTime >= endTime)
            throw new InvalidOperationException("Shift template start time must be before end time.");
    }

    private static void ValidateHeadcount(int requiredHeadcount)
    {
        if (requiredHeadcount < 1 || requiredHeadcount > 999)
            throw new InvalidOperationException("Required headcount must be between 1 and 999.");
    }
}
