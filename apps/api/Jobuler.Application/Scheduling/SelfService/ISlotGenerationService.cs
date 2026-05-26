namespace Jobuler.Application.Scheduling.SelfService;

/// <summary>
/// Generates shift slots from shift templates for a scheduling cycle.
/// Produces one slot per non-deleted template with an active GroupTask
/// for each date in the cycle that matches the template's day of week.
/// Generation is idempotent — running multiple times produces the same result.
/// </summary>
public interface ISlotGenerationService
{
    /// <summary>
    /// Generates shift slots for the specified group and scheduling cycle.
    /// Skips templates that are deleted or reference inactive GroupTasks.
    /// Skips slots that already exist for the same template and date (idempotent).
    /// </summary>
    /// <param name="groupId">The group to generate slots for.</param>
    /// <param name="schedulingCycleId">The scheduling cycle defining the date range.</param>
    /// <param name="ct">Cancellation token.</param>
    Task GenerateSlotsForCycleAsync(Guid groupId, Guid schedulingCycleId, CancellationToken ct = default);
}
