namespace Jobuler.Application.Billing;

/// <summary>
/// Provides cached access to the trial duration configured in LemonSqueezy.
/// Falls back to a default of 14 days when the cache is unavailable.
/// Defined in Application, implemented in Infrastructure so HTTP concerns stay out of the Application layer.
/// </summary>
public interface ITrialDurationCache
{
    /// <summary>
    /// Returns the trial duration in days from the local cache.
    /// If the cache is stale or unavailable, returns the last known value or the 14-day default.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of trial days.</returns>
    Task<int> GetTrialDaysAsync(CancellationToken ct = default);

    /// <summary>
    /// Syncs the trial duration from the LemonSqueezy product variant configuration.
    /// Intended to be called by a background job on a periodic schedule.
    /// On failure, logs a warning and preserves the existing cached value.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task SyncFromLemonSqueezyAsync(CancellationToken ct = default);
}
