using Jobuler.Application.Scheduling.Models;

namespace Jobuler.Application.Scheduling;

/// <summary>
/// Maintains per-person cumulative counters across solver runs.
/// Triggered on schedule publish, rollback, and presence-window edits.
/// </summary>
public interface ICumulativeTracker
{
    /// <summary>
    /// Updates cumulative records after a schedule version is published.
    /// Increments assignment counters and recomputes consecutive_hours_at_base.
    /// </summary>
    Task UpdateOnPublishAsync(Guid spaceId, Guid versionId, CancellationToken ct);

    /// <summary>
    /// Full recomputation from presence_windows. Used on rollback or presence edits.
    /// </summary>
    Task RecomputeForPersonAsync(Guid spaceId, Guid personId, CancellationToken ct);

    /// <summary>
    /// Resets all-time-within-period counters for all persons in a group.
    /// Called when a new subscription period starts.
    /// </summary>
    Task ResetPeriodCountersAsync(Guid spaceId, Guid groupId, Guid newPeriodId, CancellationToken ct);

    /// <summary>
    /// Returns cumulative tracking data for the solver payload.
    /// </summary>
    Task<List<CumulativeTrackingDto>> GetForSolverPayloadAsync(Guid spaceId, Guid groupId, CancellationToken ct);
}
