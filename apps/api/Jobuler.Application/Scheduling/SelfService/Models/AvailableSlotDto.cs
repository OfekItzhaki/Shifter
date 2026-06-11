namespace Jobuler.Application.Scheduling.SelfService.Models;

/// <summary>
/// Represents an available shift slot returned by the availability engine.
/// </summary>
/// <param name="ShiftSlotId">Unique identifier of the shift slot.</param>
/// <param name="Date">The date of the shift slot.</param>
/// <param name="StartTime">Start time of the shift.</param>
/// <param name="EndTime">End time of the shift.</param>
/// <param name="TaskName">Display name of the associated group task.</param>
/// <param name="CurrentFillCount">Number of members currently assigned to this slot.</param>
/// <param name="Capacity">Maximum number of members this slot can hold.</param>
/// <param name="IsSpecialDay">True when the slot date matches a marked space special day.</param>
/// <param name="SpecialDayName">Display name of the matching special day, when any.</param>
/// <param name="SpecialDayKind">Special day kind, when any.</param>
public record AvailableSlotDto(
    Guid ShiftSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string TaskName,
    int CurrentFillCount,
    int Capacity,
    bool IsSpecialDay = false,
    string? SpecialDayName = null,
    string? SpecialDayKind = null);
