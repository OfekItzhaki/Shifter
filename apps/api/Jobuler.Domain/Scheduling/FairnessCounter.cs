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
    public int DislikedHatedScore7d { get; private set; }
    public int KitchenCount7d { get; private set; }
    public int NightMissions7d { get; private set; }
    public int ConsecutiveHardCount { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private FairnessCounter() { }

    public static FairnessCounter Create(Guid spaceId, Guid personId, DateOnly asOfDate) =>
        new()
        {
            SpaceId = spaceId,
            PersonId = personId,
            AsOfDate = asOfDate,
            UpdatedAt = DateTime.UtcNow
        };

    public void Update(
        int total7d, int total14d, int total30d,
        int hard7d, int hard14d, int dislikedHated7d,
        int kitchen7d, int night7d, int consecutiveHard)
    {
        TotalAssignments7d = total7d;
        TotalAssignments14d = total14d;
        TotalAssignments30d = total30d;
        HardTasks7d = hard7d;
        HardTasks14d = hard14d;
        DislikedHatedScore7d = dislikedHated7d;
        KitchenCount7d = kitchen7d;
        NightMissions7d = night7d;
        ConsecutiveHardCount = consecutiveHard;
        UpdatedAt = DateTime.UtcNow;
    }
}
