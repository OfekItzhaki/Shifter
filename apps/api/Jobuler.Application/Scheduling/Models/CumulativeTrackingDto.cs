namespace Jobuler.Application.Scheduling.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Per-person cumulative tracking data sent to the solver.
/// Enables cross-run memory for home-leave eligibility and fairness.
/// </summary>
public record CumulativeTrackingDto(
    [property: JsonPropertyName("person_id")] string PersonId,
    [property: JsonPropertyName("consecutive_hours_at_base")] double ConsecutiveHoursAtBase,
    [property: JsonPropertyName("last_home_leave_end")] string? LastHomeLeaveEnd,
    [property: JsonPropertyName("total_assignments_in_period")] int TotalAssignmentsInPeriod,
    [property: JsonPropertyName("hard_tasks_in_period")] int HardTasksInPeriod,
    [property: JsonPropertyName("days_since_last_leave")] int DaysSinceLastLeave);
