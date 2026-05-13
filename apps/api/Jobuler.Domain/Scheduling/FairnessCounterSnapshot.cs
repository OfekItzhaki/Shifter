using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// Daily snapshot of fairness metrics per person per space.
/// Used for historical trend graphs and time-series queries.
/// </summary>
public class FairnessCounterSnapshot : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid PersonId { get; private set; }
    public DateOnly SnapshotDate { get; private set; }
    public int TotalAssignments { get; private set; }
    public int HardCount { get; private set; }
    public int NormalCount { get; private set; }
    public int EasyCount { get; private set; }
    public int BurdenScore { get; private set; }

    private FairnessCounterSnapshot() { }

    public static FairnessCounterSnapshot Create(
        Guid spaceId,
        Guid personId,
        DateOnly snapshotDate,
        int totalAssignments,
        int hardCount,
        int normalCount,
        int easyCount,
        int burdenScore) =>
        new()
        {
            SpaceId = spaceId,
            PersonId = personId,
            SnapshotDate = snapshotDate,
            TotalAssignments = totalAssignments,
            HardCount = hardCount,
            NormalCount = normalCount,
            EasyCount = easyCount,
            BurdenScore = burdenScore
        };

    /// <summary>
    /// Updates an existing snapshot with new metric values (upsert scenario).
    /// </summary>
    public void Update(int totalAssignments, int hardCount, int normalCount, int easyCount, int burdenScore)
    {
        TotalAssignments = totalAssignments;
        HardCount = hardCount;
        NormalCount = normalCount;
        EasyCount = easyCount;
        BurdenScore = burdenScore;
    }
}
