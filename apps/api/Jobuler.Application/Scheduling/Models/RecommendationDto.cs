namespace Jobuler.Application.Scheduling.Models;

/// <summary>
/// Represents a single double-shift recommendation returned by queries.
/// Maps from the <see cref="Jobuler.Domain.Scheduling.DoubleShiftRecommendation"/> entity.
/// </summary>
public record RecommendationDto(
    Guid Id,
    Guid GroupTaskId,
    string TaskName,
    string Status,
    int AdditionalSlotsCovered,
    DateTime AffectedDateStart,
    DateTime AffectedDateEnd,
    int TotalUncoveredSlotsInRun,
    DateTime CreatedAt);
