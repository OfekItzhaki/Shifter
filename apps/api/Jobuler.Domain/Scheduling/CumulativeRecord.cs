using System.Text.Json;
using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// Per-person cumulative counters that persist across solver runs.
/// Tracks consecutive hours at base, assignment counts across multiple time windows,
/// and feeds into the solver payload for fairness and home-leave eligibility.
/// </summary>
public class CumulativeRecord : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid PersonId { get; private set; }
    public Guid PeriodId { get; private set; }

    // Consecutive hours tracking (for home-leave eligibility)
    public decimal ConsecutiveHoursAtBase { get; private set; }
    public DateTime? LastHomeLeaveEnd { get; private set; }

    // Multi-window counters: total assignments
    public int TotalAssignments7d { get; private set; }
    public int TotalAssignments14d { get; private set; }
    public int TotalAssignments30d { get; private set; }
    public int TotalAssignments90d { get; private set; }
    public int TotalAssignmentsPeriod { get; private set; }

    // Multi-window counters: hard tasks
    public int HardTasks7d { get; private set; }
    public int HardTasks14d { get; private set; }
    public int HardTasks30d { get; private set; }
    public int HardTasks90d { get; private set; }
    public int HardTasksPeriod { get; private set; }

    // Multi-window counters: night missions
    public int NightMissions7d { get; private set; }
    public int NightMissions14d { get; private set; }
    public int NightMissions30d { get; private set; }
    public int NightMissions90d { get; private set; }
    public int NightMissionsPeriod { get; private set; }

    // Generic task-type counts stored as JSONB (e.g., {"kitchen": {"7d": 2, "14d": 5, ...}})
    public string? TaskTypeCountsJson { get; private set; }

    public decimal TotalHoursAssignedPeriod { get; private set; }
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private CumulativeRecord() { }

    /// <summary>
    /// Creates a new CumulativeRecord with all counters at zero.
    /// </summary>
    public static CumulativeRecord Create(Guid spaceId, Guid groupId, Guid personId, Guid periodId) => new()
    {
        SpaceId = spaceId,
        GroupId = groupId,
        PersonId = personId,
        PeriodId = periodId,
        TaskTypeCountsJson = "{}",
        UpdatedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Resets all period-scoped counters. Called when a new subscription period starts.
    /// </summary>
    public void ResetPeriodCounters()
    {
        TotalAssignmentsPeriod = 0;
        HardTasksPeriod = 0;
        NightMissionsPeriod = 0;
        TotalHoursAssignedPeriod = 0;
        ConsecutiveHoursAtBase = 0;
        LastHomeLeaveEnd = null;
        TaskTypeCountsJson = "{}";
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates consecutive hours at base and last home-leave end timestamp.
    /// Called after recomputation from presence windows.
    /// </summary>
    public void UpdateConsecutiveHours(decimal hours, DateTime? lastLeaveEnd)
    {
        ConsecutiveHoursAtBase = hours;
        LastHomeLeaveEnd = lastLeaveEnd;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Increments all time-window counters by the given delta.
    /// Called after a schedule version is published.
    /// </summary>
    public void IncrementCounters(AssignmentCountsDelta delta)
    {
        TotalAssignments7d += delta.TotalAssignments;
        TotalAssignments14d += delta.TotalAssignments;
        TotalAssignments30d += delta.TotalAssignments;
        TotalAssignments90d += delta.TotalAssignments;
        TotalAssignmentsPeriod += delta.TotalAssignments;

        HardTasks7d += delta.HardTasks;
        HardTasks14d += delta.HardTasks;
        HardTasks30d += delta.HardTasks;
        HardTasks90d += delta.HardTasks;
        HardTasksPeriod += delta.HardTasks;

        NightMissions7d += delta.NightMissions;
        NightMissions14d += delta.NightMissions;
        NightMissions30d += delta.NightMissions;
        NightMissions90d += delta.NightMissions;
        NightMissionsPeriod += delta.NightMissions;

        TotalHoursAssignedPeriod += delta.TotalHours;

        // Merge task-type counts into the JSONB structure
        if (delta.TaskTypeCounts is { Count: > 0 })
        {
            MergeTaskTypeCounts(delta.TaskTypeCounts);
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Merges incoming task-type counts into the existing JSONB structure.
    /// Each task type has per-window counters: 7d, 14d, 30d, 90d, period.
    /// On increment, all windows are incremented (decay is handled externally).
    /// </summary>
    private void MergeTaskTypeCounts(Dictionary<string, int> incoming)
    {
        var existing = DeserializeTaskTypeCounts();

        foreach (var (taskType, count) in incoming)
        {
            if (!existing.TryGetValue(taskType, out var windows))
            {
                windows = new Dictionary<string, int>
                {
                    ["7d"] = 0, ["14d"] = 0, ["30d"] = 0, ["90d"] = 0, ["period"] = 0
                };
                existing[taskType] = windows;
            }

            windows["7d"] = windows.GetValueOrDefault("7d") + count;
            windows["14d"] = windows.GetValueOrDefault("14d") + count;
            windows["30d"] = windows.GetValueOrDefault("30d") + count;
            windows["90d"] = windows.GetValueOrDefault("90d") + count;
            windows["period"] = windows.GetValueOrDefault("period") + count;
        }

        TaskTypeCountsJson = JsonSerializer.Serialize(existing);
    }

    /// <summary>
    /// Deserializes the TaskTypeCountsJson into a nested dictionary structure.
    /// </summary>
    private Dictionary<string, Dictionary<string, int>> DeserializeTaskTypeCounts()
    {
        if (string.IsNullOrWhiteSpace(TaskTypeCountsJson) || TaskTypeCountsJson == "{}")
            return new Dictionary<string, Dictionary<string, int>>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(TaskTypeCountsJson)
                ?? new Dictionary<string, Dictionary<string, int>>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, Dictionary<string, int>>();
        }
    }
}
