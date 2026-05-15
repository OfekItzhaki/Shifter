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

    // Multi-window counters: disliked/hated score
    public int DislikedHatedScore7d { get; private set; }
    public int DislikedHatedScore14d { get; private set; }
    public int DislikedHatedScore30d { get; private set; }
    public int DislikedHatedScore90d { get; private set; }
    public int DislikedHatedScorePeriod { get; private set; }

    // Multi-window counters: kitchen count
    public int KitchenCount7d { get; private set; }
    public int KitchenCount14d { get; private set; }
    public int KitchenCount30d { get; private set; }
    public int KitchenCount90d { get; private set; }
    public int KitchenCountPeriod { get; private set; }

    // Multi-window counters: night missions
    public int NightMissions7d { get; private set; }
    public int NightMissions14d { get; private set; }
    public int NightMissions30d { get; private set; }
    public int NightMissions90d { get; private set; }
    public int NightMissionsPeriod { get; private set; }

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
        UpdatedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Resets all period-scoped counters. Called when a new subscription period starts.
    /// </summary>
    public void ResetPeriodCounters()
    {
        TotalAssignmentsPeriod = 0;
        HardTasksPeriod = 0;
        DislikedHatedScorePeriod = 0;
        KitchenCountPeriod = 0;
        NightMissionsPeriod = 0;
        TotalHoursAssignedPeriod = 0;
        ConsecutiveHoursAtBase = 0;
        LastHomeLeaveEnd = null;
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

        DislikedHatedScore7d += delta.DislikedHatedScore;
        DislikedHatedScore14d += delta.DislikedHatedScore;
        DislikedHatedScore30d += delta.DislikedHatedScore;
        DislikedHatedScore90d += delta.DislikedHatedScore;
        DislikedHatedScorePeriod += delta.DislikedHatedScore;

        KitchenCount7d += delta.KitchenCount;
        KitchenCount14d += delta.KitchenCount;
        KitchenCount30d += delta.KitchenCount;
        KitchenCount90d += delta.KitchenCount;
        KitchenCountPeriod += delta.KitchenCount;

        NightMissions7d += delta.NightMissions;
        NightMissions14d += delta.NightMissions;
        NightMissions30d += delta.NightMissions;
        NightMissions90d += delta.NightMissions;
        NightMissionsPeriod += delta.NightMissions;

        TotalHoursAssignedPeriod += delta.TotalHours;
        UpdatedAt = DateTime.UtcNow;
    }
}
