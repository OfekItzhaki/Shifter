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
    StabilityWeightsDto StabilityWeights,
    List<PersonEligibilityDto> People,
    List<AvailabilityWindowDto> AvailabilityWindows,
    List<PresenceWindowDto> PresenceWindows,
    List<TaskSlotDto> TaskSlots,
    List<HardConstraintDto> HardConstraints,
    List<SoftConstraintDto> SoftConstraints,
    List<BaselineAssignmentDto> BaselineAssignments,
    List<FairnessCountersDto> FairnessCounters);

public record StabilityWeightsDto(
    [property: JsonPropertyName("today_tomorrow")] double TodayTomorrow,
    [property: JsonPropertyName("days_3_4")]       double Days3To4,
    [property: JsonPropertyName("days_5_7")]       double Days5To7);

public record PersonEligibilityDto(
    string PersonId,
    List<string> RoleIds,
    List<string> QualificationIds,
    List<string> GroupIds);

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
    bool AllowsOverlap);

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
    int HatedTasks7d,
    int DislikedHatedScore7d,
    int KitchenCount7d,
    int NightMissions7d,
    int ConsecutiveBurdenCount);
