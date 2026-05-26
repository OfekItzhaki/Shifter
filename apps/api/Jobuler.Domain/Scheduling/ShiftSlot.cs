using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// A concrete time slot generated from a ShiftTemplate for a specific date.
/// Tracks capacity and current fill count for self-service scheduling.
/// </summary>
public class ShiftSlot : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid GroupTaskId { get; private set; }
    public Guid ShiftTemplateId { get; private set; }
    public Guid SchedulingCycleId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int Capacity { get; private set; }
    public int CurrentFillCount { get; private set; }
    public ShiftSlotStatus Status { get; private set; }

    private ShiftSlot() { }

    public static ShiftSlot Create(
        Guid spaceId,
        Guid groupId,
        Guid groupTaskId,
        Guid shiftTemplateId,
        Guid schedulingCycleId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        int capacity) =>
        new()
        {
            SpaceId = spaceId,
            GroupId = groupId,
            GroupTaskId = groupTaskId,
            ShiftTemplateId = shiftTemplateId,
            SchedulingCycleId = schedulingCycleId,
            Date = date,
            StartTime = startTime,
            EndTime = endTime,
            Capacity = capacity,
            CurrentFillCount = 0,
            Status = ShiftSlotStatus.Open
        };

    public void IncrementFillCount()
    {
        CurrentFillCount++;
        Touch();
    }

    public void DecrementFillCount()
    {
        if (CurrentFillCount <= 0)
            throw new InvalidOperationException("Cannot decrement fill count below zero.");
        CurrentFillCount--;
        Touch();
    }

    public void Close()
    {
        Status = ShiftSlotStatus.Closed;
        Touch();
    }

    public void Reopen()
    {
        Status = ShiftSlotStatus.Open;
        Touch();
    }

    public bool HasAvailableCapacity() => CurrentFillCount < Capacity;

    /// <summary>
    /// Updates slot properties to match a modified template.
    /// Only called on unprotected slots (those with zero approved requests).
    /// </summary>
    public void UpdateFromTemplate(TimeOnly startTime, TimeOnly endTime, int capacity, Guid? groupTaskId = null)
    {
        StartTime = startTime;
        EndTime = endTime;
        Capacity = capacity;

        if (groupTaskId.HasValue)
            GroupTaskId = groupTaskId.Value;

        Touch();
    }
}
