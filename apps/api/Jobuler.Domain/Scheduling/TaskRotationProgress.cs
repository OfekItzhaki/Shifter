using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// Tracks per-person task type rotation progress within an army-template group.
/// Each person cycles through all qualified task types; once all are completed,
/// the cycle resets and a new cycle begins.
/// </summary>
public class TaskRotationProgress : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid PersonId { get; private set; }
    public Guid GroupId { get; private set; }
    public int CycleNumber { get; private set; }
    public List<Guid> CompletedTaskTypeIds { get; private set; } = new();
    public int TotalQualifiedTaskTypes { get; private set; }
    public double CompletionPercentage { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }

    private TaskRotationProgress() { }

    public static TaskRotationProgress Create(Guid spaceId, Guid personId, Guid groupId, int totalQualifiedTaskTypes) =>
        new()
        {
            SpaceId = spaceId,
            PersonId = personId,
            GroupId = groupId,
            CycleNumber = 1,
            CompletedTaskTypeIds = new List<Guid>(),
            TotalQualifiedTaskTypes = totalQualifiedTaskTypes,
            CompletionPercentage = 0,
            LastUpdatedAt = DateTime.UtcNow
        };

    /// <summary>
    /// Records that the person completed a task of the given type.
    /// If all qualified types are now completed, increments the cycle and resets.
    /// </summary>
    public void RecordCompletion(Guid taskTypeId)
    {
        if (!CompletedTaskTypeIds.Contains(taskTypeId))
            CompletedTaskTypeIds.Add(taskTypeId);

        RecalculatePercentage();

        // Cycle reset: if all qualified types completed, start a new cycle
        if (TotalQualifiedTaskTypes > 0 && CompletedTaskTypeIds.Count >= TotalQualifiedTaskTypes)
        {
            CycleNumber++;
            // Keep only the latest task type (the one that triggered the reset)
            CompletedTaskTypeIds = new List<Guid> { taskTypeId };
            RecalculatePercentage();
        }

        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Resets the current cycle: clears completed list and recalculates percentage.
    /// </summary>
    public void ResetCycle()
    {
        CycleNumber++;
        CompletedTaskTypeIds = new List<Guid>();
        RecalculatePercentage();
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the total qualified task types count (e.g. when qualifications change).
    /// Preserves the completed list but recalculates the percentage.
    /// </summary>
    public void UpdateQualifiedCount(int newTotal)
    {
        TotalQualifiedTaskTypes = newTotal;
        RecalculatePercentage();
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Bulk-sets the completed task type IDs (used during upsert from command handler).
    /// </summary>
    public void SetCompletedTaskTypes(List<Guid> completedIds, int cycleNumber)
    {
        CompletedTaskTypeIds = completedIds;
        CycleNumber = cycleNumber;
        RecalculatePercentage();
        LastUpdatedAt = DateTime.UtcNow;
    }

    private void RecalculatePercentage()
    {
        CompletionPercentage = TotalQualifiedTaskTypes > 0
            ? (double)CompletedTaskTypeIds.Count / TotalQualifiedTaskTypes * 100.0
            : 0;
    }
}
