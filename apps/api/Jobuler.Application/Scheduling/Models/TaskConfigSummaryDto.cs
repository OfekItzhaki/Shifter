namespace Jobuler.Application.Scheduling.Models;

/// <summary>
/// Lightweight summary of a GroupTask's configuration, returned as part of the schedule response
/// so the frontend can display task info badges without additional API calls.
/// </summary>
public record TaskConfigSummaryDto(
    string TaskId,
    bool AllowsDoubleShift,
    bool AllowsOverlap,
    string? DailyStartTime,
    string? DailyEndTime,
    string BurdenLevel,
    List<string> RequiredQualificationNames,
    int SplitCount);
