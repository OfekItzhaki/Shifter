using System.Text.Json;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// Represents the delta of assignment counts to apply to cumulative records.
/// </summary>
public record AssignmentCountsDelta(
    int TotalAssignments,
    int HardTasks,
    int NightMissions,
    decimal TotalHours,
    Dictionary<string, int>? TaskTypeCounts = null);

/// <summary>
/// Snapshot diff returned after creating/replacing snapshots.
/// </summary>
public record SnapshotDiff(
    int Added,
    int Replaced,
    int Preserved,
    List<AssignmentCountsDelta> ReplacedDeltas);
