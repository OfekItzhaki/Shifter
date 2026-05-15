using Jobuler.Domain.Scheduling;

namespace Jobuler.Application.Scheduling;

/// <summary>
/// Manages subscription period lifecycle — creation, closure, and querying.
/// Periods partition cumulative data into logical time segments tied to billing lifecycle.
/// </summary>
public interface IPeriodManager
{
    /// <summary>
    /// Creates a new subscription period when a subscription becomes active/trialing.
    /// Resets cumulative counters for the group via ICumulativeTracker.
    /// </summary>
    Task<Guid> OpenPeriodAsync(Guid spaceId, Guid groupId, CancellationToken ct);

    /// <summary>
    /// Closes the current period after the grace period elapses.
    /// Preserves all associated snapshots and cumulative records.
    /// </summary>
    Task ClosePeriodAsync(Guid spaceId, Guid groupId, CancellationToken ct);

    /// <summary>
    /// Returns the current active period for a group, or null if none.
    /// </summary>
    Task<SubscriptionPeriod?> GetCurrentPeriodAsync(Guid spaceId, Guid groupId, CancellationToken ct);
}
