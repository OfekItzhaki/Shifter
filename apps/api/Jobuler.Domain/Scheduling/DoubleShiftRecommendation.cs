using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

public enum RecommendationStatus { Active, Dismissed, Resolved, Cleared }

public class DoubleShiftRecommendation : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid ScheduleRunId { get; private set; }
    public Guid GroupTaskId { get; private set; }
    public string TaskName { get; private set; } = string.Empty;
    public RecommendationStatus Status { get; private set; } = RecommendationStatus.Active;
    public int AdditionalSlotsCovered { get; private set; }
    public DateTime AffectedDateStart { get; private set; }
    public DateTime AffectedDateEnd { get; private set; }
    public int TotalUncoveredSlotsInRun { get; private set; }
    public DateTime? DismissedAt { get; private set; }
    public Guid? DismissedByUserId { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClearedAt { get; private set; }

    private DoubleShiftRecommendation() { }

    public static DoubleShiftRecommendation Create(
        Guid spaceId,
        Guid groupId,
        Guid scheduleRunId,
        Guid groupTaskId,
        string taskName,
        int additionalSlotsCovered,
        DateTime affectedDateStart,
        DateTime affectedDateEnd,
        int totalUncoveredSlotsInRun) =>
        new()
        {
            SpaceId = spaceId,
            GroupId = groupId,
            ScheduleRunId = scheduleRunId,
            GroupTaskId = groupTaskId,
            TaskName = taskName,
            AdditionalSlotsCovered = additionalSlotsCovered,
            AffectedDateStart = affectedDateStart,
            AffectedDateEnd = affectedDateEnd,
            TotalUncoveredSlotsInRun = totalUncoveredSlotsInRun
        };

    /// <summary>
    /// Admin dismisses this recommendation. It will not be shown again for this solver run.
    /// </summary>
    public void Dismiss(Guid userId)
    {
        if (Status != RecommendationStatus.Active)
            throw new InvalidOperationException(
                $"Cannot dismiss recommendation in status '{Status}'. Only active recommendations can be dismissed.");

        Status = RecommendationStatus.Dismissed;
        DismissedAt = DateTime.UtcNow;
        DismissedByUserId = userId;
    }

    /// <summary>
    /// The recommended action was taken (double shift enabled on the task).
    /// </summary>
    public void Resolve()
    {
        if (Status != RecommendationStatus.Active)
            throw new InvalidOperationException(
                $"Cannot resolve recommendation in status '{Status}'. Only active recommendations can be resolved.");

        Status = RecommendationStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// A new solver run completed without a staffing shortfall, clearing stale recommendations.
    /// </summary>
    public void Clear()
    {
        if (Status != RecommendationStatus.Active)
            throw new InvalidOperationException(
                $"Cannot clear recommendation in status '{Status}'. Only active recommendations can be cleared.");

        Status = RecommendationStatus.Cleared;
        ClearedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates an existing recommendation with new analysis data (upsert scenario on re-run).
    /// Resets the recommendation to Active status with fresh metrics.
    /// </summary>
    public void Update(
        string taskName,
        int additionalSlotsCovered,
        DateTime affectedDateStart,
        DateTime affectedDateEnd,
        int totalUncoveredSlotsInRun)
    {
        TaskName = taskName;
        AdditionalSlotsCovered = additionalSlotsCovered;
        AffectedDateStart = affectedDateStart;
        AffectedDateEnd = affectedDateEnd;
        TotalUncoveredSlotsInRun = totalUncoveredSlotsInRun;
        Status = RecommendationStatus.Active;
        DismissedAt = null;
        DismissedByUserId = null;
        ResolvedAt = null;
        ClearedAt = null;
    }
}
