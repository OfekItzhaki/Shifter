namespace Jobuler.Domain.Scheduling;

/// <summary>
/// Represents the delta of assignment counts to apply to cumulative records.
/// </summary>
public record AssignmentCountsDelta(
    int TotalAssignments,
    int HardTasks,
    int DislikedHatedScore,
    int KitchenCount,
    int NightMissions,
    decimal TotalHours);

/// <summary>
/// Snapshot diff returned after creating/replacing snapshots.
/// </summary>
public record SnapshotDiff(
    int Added,
    int Replaced,
    int Preserved,
    List<AssignmentCountsDelta> ReplacedDeltas);
