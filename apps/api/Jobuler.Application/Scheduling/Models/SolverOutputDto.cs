using System.Text.Json.Serialization;

namespace Jobuler.Application.Scheduling.Models;

/// <summary>
/// Mirrors the Python solver's SolverOutput Pydantic model (solver_output.py).
/// Deserialized from the HTTP POST /solve response.
/// Uses explicit JsonPropertyName attributes to ensure snake_case JSON fields
/// map correctly regardless of the serializer's naming policy.
/// </summary>
public class SolverOutputDto
{
    [JsonPropertyName("run_id")]       public string RunId { get; init; } = "";
    [JsonPropertyName("feasible")]     public bool Feasible { get; init; }
    [JsonPropertyName("timed_out")]    public bool TimedOut { get; init; }
    [JsonPropertyName("assignments")]  public List<AssignmentResultDto> Assignments { get; init; } = new();
    [JsonPropertyName("uncovered_slot_ids")] public List<string> UncoveredSlotIds { get; init; } = new();
    [JsonPropertyName("hard_conflicts")]     public List<HardConflictDto> HardConflicts { get; init; } = new();
    [JsonPropertyName("soft_penalty_total")] public double SoftPenaltyTotal { get; init; }
    [JsonPropertyName("stability_metrics")]  public StabilityMetricsDto StabilityMetrics { get; init; } = new();
    [JsonPropertyName("fairness_metrics")]   public List<FairnessMetricsDto> FairnessMetrics { get; init; } = new();
    [JsonPropertyName("explanation_fragments")] public List<string> ExplanationFragments { get; init; } = new();
    [JsonPropertyName("home_leave_assignments")] public List<HomeLeaveAssignmentDto> HomeLeaveAssignments { get; init; } = new();
    [JsonPropertyName("home_leave_metrics")]     public List<HomeLeaveMetricDto> HomeLeaveMetrics { get; init; } = new();
    [JsonPropertyName("fairness_variance")]      public double? FairnessVariance { get; init; }
}

public class AssignmentResultDto
{
    [JsonPropertyName("slot_id")]   public string SlotId { get; init; } = "";
    [JsonPropertyName("person_id")] public string PersonId { get; init; } = "";
    [JsonPropertyName("source")]    public string Source { get; init; } = "solver";
}

public class HardConflictDto
{
    [JsonPropertyName("constraint_id")]      public string ConstraintId { get; init; } = "";
    [JsonPropertyName("rule_type")]          public string RuleType { get; init; } = "";
    [JsonPropertyName("description")]        public string Description { get; init; } = "";
    [JsonPropertyName("affected_slot_ids")]  public List<string> AffectedSlotIds { get; init; } = new();
    [JsonPropertyName("affected_person_ids")] public List<string> AffectedPersonIds { get; init; } = new();
}

public class StabilityMetricsDto
{
    [JsonPropertyName("today_tomorrow_changes")] public int TodayTomorrowChanges { get; init; }
    [JsonPropertyName("days_3_4_changes")]        public int Days3To4Changes { get; init; }
    [JsonPropertyName("days_5_7_changes")]        public int Days5To7Changes { get; init; }
    [JsonPropertyName("total_stability_penalty")] public double TotalStabilityPenalty { get; init; }
}

public class FairnessMetricsDto
{
    [JsonPropertyName("person_id")]              public string PersonId { get; init; } = "";
    [JsonPropertyName("hated_tasks_assigned")]   public int HatedTasksAssigned { get; init; }
    [JsonPropertyName("disliked_tasks_assigned")] public int DislikedTasksAssigned { get; init; }
    [JsonPropertyName("total_assigned")]         public int TotalAssigned { get; init; }
}

public class HomeLeaveAssignmentDto
{
    [JsonPropertyName("person_id")] public string PersonId { get; init; } = "";
    [JsonPropertyName("starts_at")] public string StartsAt { get; init; } = "";
    [JsonPropertyName("ends_at")]   public string EndsAt { get; init; } = "";
}

public class HomeLeaveMetricDto
{
    [JsonPropertyName("person_id")]        public string PersonId { get; init; } = "";
    [JsonPropertyName("total_base_hours")] public double TotalBaseHours { get; init; }
    [JsonPropertyName("total_home_hours")] public double TotalHomeHours { get; init; }
    [JsonPropertyName("base_time_ratio")]  public double BaseTimeRatio { get; init; }
    [JsonPropertyName("leave_slot_count")] public int LeaveSlotCount { get; init; }
}
