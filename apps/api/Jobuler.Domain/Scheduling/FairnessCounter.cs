using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// Rolling fairness ledger per person per space.
/// Updated after each solver run and sent as input to the next run.
/// Preserved across versions so fairness history survives rollbacks.
/// </summary>
public class FairnessCounter : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid PersonId { get; private set; }
    public DateOnly AsOfDate { get; private set; }
    public int TotalAssignments7d { get; private set; }
    public int TotalAssignments14d { get; private set; }
    public int TotalAssignments30d { get; private set; }
    public int HardTasks7d { get; private set; }
    public int HardTasks14d { get; private set; }
    public int HardTasks30d { get; private set; }
    public int EasyTasks7d { get; private set; }
    public int EasyTasks14d { get; private set; }
    public int EasyTasks30d { get; private set; }
    public int BurdenScore7d { get; private set; }
    public int BurdenScore14d { get; private set; }
    public int BurdenScore30d { get; private set; }
    public int NightMissions7d { get; private set; }
    public int ConsecutiveHardCount { get; private set; }

    // Generic task-type counts stored as JSONB (e.g., {"kitchen": 2, "guard": 3})
    public string? TaskTypeCountsJson { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private FairnessCounter() { }

    public static FairnessCounter Create(Guid spaceId, Guid personId, DateOnly asOfDate) =>
        new()
        {
            SpaceId = spaceId,
            PersonId = personId,
            AsOfDate = asOfDate,
            TaskTypeCountsJson = "{}",
            UpdatedAt = DateTime.UtcNow
        };

    public void Update(
        int total7d, int total14d, int total30d,
        int hard7d, int hard14d, int hard30d,
        int easy7d, int easy14d, int easy30d,
        int burdenScore7d, int burdenScore14d, int burdenScore30d,
        int night7d, int consecutiveHard,
        string? taskTypeCountsJson = null)
    {
        if (total7d < 0 || total14d < 0 || total30d < 0)
            throw new InvalidOperationException("Total assignment counts must be non-negative.");
        if (hard7d < 0 || hard14d < 0 || hard30d < 0)
            throw new InvalidOperationException("Hard task counts must be non-negative.");
        if (easy7d < 0 || easy14d < 0 || easy30d < 0)
            throw new InvalidOperationException("Easy task counts must be non-negative.");
        if (night7d < 0)
            throw new InvalidOperationException("Night missions count must be non-negative.");
        if (consecutiveHard < 0)
            throw new InvalidOperationException("Consecutive hard count must be non-negative.");

        TotalAssignments7d = total7d;
        TotalAssignments14d = total14d;
        TotalAssignments30d = total30d;
        HardTasks7d = hard7d;
        HardTasks14d = hard14d;
        HardTasks30d = hard30d;
        EasyTasks7d = easy7d;
        EasyTasks14d = easy14d;
        EasyTasks30d = easy30d;
        BurdenScore7d = burdenScore7d;
        BurdenScore14d = burdenScore14d;
        BurdenScore30d = burdenScore30d;
        NightMissions7d = night7d;
        ConsecutiveHardCount = consecutiveHard;
        TaskTypeCountsJson = taskTypeCountsJson ?? "{}";
        UpdatedAt = DateTime.UtcNow;
    }
}
