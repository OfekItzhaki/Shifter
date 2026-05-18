using Jobuler.Application.Scheduling.Models;

namespace Jobuler.Application.Scheduling;

/// <summary>
/// Analyzes solver output to detect staffing shortfalls and produce
/// double-shift recommendations. Runs after each solver run completes.
/// </summary>
public interface IRecommendationEngine
{
    Task<RecommendationResult> AnalyzeAsync(
        Guid spaceId,
        Guid groupId,
        Guid runId,
        SolverInputDto input,
        SolverOutputDto output,
        CancellationToken ct = default);
}

/// <summary>
/// Result of the recommendation engine analysis.
/// </summary>
/// <param name="HasShortfall">Whether a staffing shortfall was detected in the solver output.</param>
/// <param name="Recommendations">Ranked list of tasks where enabling double shift would reduce uncovered slots.</param>
public record RecommendationResult(
    bool HasShortfall,
    List<RecommendationItem> Recommendations);

/// <summary>
/// A single recommendation to enable double shift on a task.
/// </summary>
/// <param name="GroupTaskId">The task that would benefit from enabling double shift.</param>
/// <param name="TaskName">Display name of the task.</param>
/// <param name="AdditionalSlotsCovered">Number of currently-uncovered slots that would be filled.</param>
/// <param name="AffectedDateStart">Earliest date containing uncovered slots for this task.</param>
/// <param name="AffectedDateEnd">Latest date containing uncovered slots for this task.</param>
public record RecommendationItem(
    Guid GroupTaskId,
    string TaskName,
    int AdditionalSlotsCovered,
    DateTime AffectedDateStart,
    DateTime AffectedDateEnd);
