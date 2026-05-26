namespace Jobuler.Application.Scheduling.SelfService.Models;

/// <summary>
/// Detailed view of a single shift slot, including read-only state.
/// </summary>
/// <param name="Id">Unique identifier of the shift slot.</param>
/// <param name="GroupId">The group this slot belongs to.</param>
/// <param name="GroupTaskId">The associated group task ID.</param>
/// <param name="TaskName">Display name of the associated group task.</param>
/// <param name="ShiftTemplateId">The source template that generated this slot.</param>
/// <param name="SchedulingCycleId">The scheduling cycle this slot belongs to.</param>
/// <param name="Date">The date of the shift slot.</param>
/// <param name="StartTime">Start time of the shift.</param>
/// <param name="EndTime">End time of the shift.</param>
/// <param name="Capacity">Maximum number of members this slot can hold.</param>
/// <param name="CurrentFillCount">Number of members currently assigned to this slot.</param>
/// <param name="Status">Current status of the slot (Open or Closed).</param>
/// <param name="IsReadOnly">True when the request window is closed and requests are not accepted.</param>
public record ShiftSlotDetailDto(
    Guid Id,
    Guid GroupId,
    Guid GroupTaskId,
    string TaskName,
    Guid ShiftTemplateId,
    Guid SchedulingCycleId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int Capacity,
    int CurrentFillCount,
    string Status,
    bool IsReadOnly);
