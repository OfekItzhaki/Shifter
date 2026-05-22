namespace Jobuler.Application.Scheduling.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Mirrors the Python solver's SolverInput Pydantic model (solver_input.py).
/// Serialized to JSON and sent via HTTP POST /solve.
/// </summary>
public record SolverInputDto(
    string SpaceId,
    string RunId,
    string TriggerMode,
    string HorizonStart,
    string HorizonEnd,
    string Locale,
    StabilityWeightsDto StabilityWeights,
    List<PersonEligibilityDto> People,
    List<AvailabilityWindowDto> AvailabilityWindows,
    List<PresenceWindowDto> PresenceWindows,
    List<TaskSlotDto> TaskSlots,
    List<HardConstraintDto> HardConstraints,
    List<SoftConstraintDto> SoftConstraints,
    List<HardConstraintDto> EmergencyConstraints,
    List<BaselineAssignmentDto> BaselineAssignments,
    List<FairnessCountersDto> FairnessCounters,
    List<string>? LockedSlotIds = null,
    HomeLeaveConfigDto? HomeLeaveConfig = null,
    List<TaskRotationDto>? TaskRotation = null,
    [property: JsonPropertyName("preview_mode")] bool PreviewMode = false,
    [property: JsonPropertyName("cumulative_tracking")] List<CumulativeTrackingDto>? CumulativeTracking = null,
    [property: JsonPropertyName("parent_schedule")] List<ParentAssignmentDto>? ParentSchedule = null);


public record StabilityWeightsDto(
    [property: JsonPropertyName("today_tomorrow")] double TodayTomorrow,
    [property: JsonPropertyName("days_3_4")]       double Days3To4,
    [property: JsonPropertyName("days_5_7")]       double Days5To7);

public record PersonEligibilityDto(
    string PersonId,
    List<string> RoleIds,
    List<string> QualificationIds,
    List<string> GroupIds,
    [property: JsonPropertyName("home_leave_priority")] double HomeLeavePriority = 1.0);

public record AvailabilityWindowDto(
    string PersonId,
    string StartsAt,
    string EndsAt);

public record PresenceWindowDto(
    string PersonId,
    string State,
    string StartsAt,
    string EndsAt);

public record TaskSlotDto(
    string SlotId,
    string TaskTypeId,
    string TaskTypeName,
    string BurdenLevel,
    string StartsAt,
    string EndsAt,
    int RequiredHeadcount,
    int Priority,
    List<string> RequiredRoleIds,
    List<string> RequiredQualificationIds,
    bool AllowsOverlap,
    bool AllowsDoubleShift = false,
    List<QualificationRequirementSolverDto>? QualificationRequirements = null);

public record QualificationRequirementSolverDto(
    [property: JsonPropertyName("qualification_name")] string QualificationName,
    [property: JsonPropertyName("count")]              int Count,
    [property: JsonPropertyName("mandatory")]          bool Mandatory);

public record HardConstraintDto(
    string ConstraintId,
    string RuleType,
    string ScopeType,
    string? ScopeId,
    Dictionary<string, object> Payload);

public record SoftConstraintDto(
    string ConstraintId,
    string RuleType,
    string ScopeType,
    string? ScopeId,
    double Weight,
    Dictionary<string, object> Payload);

public record BaselineAssignmentDto(
    string SlotId,
    string PersonId);

public record FairnessCountersDto(
    string PersonId,
    int TotalAssignments7d,
    int HardTasks7d,
    int NightMissions7d,
    int ConsecutiveHardCount,
    Dictionary<string, int>? TaskTypeCounts7d = null);

public record HomeLeaveConfigDto(
    [property: JsonPropertyName("enabled")]                    bool Enabled,
    [property: JsonPropertyName("min_rest_hours")]             double MinRestHours,
    [property: JsonPropertyName("eligibility_threshold_hours")] double EligibilityThresholdHours,
    [property: JsonPropertyName("leave_capacity")]             int LeaveCapacity,
    [property: JsonPropertyName("leave_duration_hours")]       double LeaveDurationHours,
    [property: JsonPropertyName("balance_value")]              int BalanceValue = 50);

public record TaskRotationDto(
    [property: JsonPropertyName("person_id")]                  string PersonId,
    [property: JsonPropertyName("completed_task_type_ids")]    List<string> CompletedTaskTypeIds);

public record ParentAssignmentDto(
    [property: JsonPropertyName("person_id")] string PersonId,
    [property: JsonPropertyName("starts_at")] string StartsAt,
    [property: JsonPropertyName("ends_at")] string EndsAt);
