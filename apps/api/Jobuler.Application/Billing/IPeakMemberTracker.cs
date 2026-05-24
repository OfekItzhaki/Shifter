namespace Jobuler.Application.Billing;

/// <summary>
/// Lightweight service to track peak member count for space-level billing.
/// After a member is added to a space, call <see cref="TrackAsync"/> to update
/// the peak member count on the SpaceSubscription if the current count exceeds
/// the previously recorded peak.
/// </summary>
public interface IPeakMemberTracker
{
    /// <summary>
    /// Loads the SpaceSubscription for the given space and updates the peak member count
    /// if the current member count exceeds the stored peak. Fails silently if no subscription exists.
    /// </summary>
    Task TrackAsync(Guid spaceId, CancellationToken ct = default);
}
