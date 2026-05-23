namespace Jobuler.Application.Billing;

/// <summary>
/// Manages statistics period boundaries in response to subscription lifecycle events.
/// Each lifecycle event closes active periods and opens new ones for all groups in the space.
/// Defined in Application, implemented in Infrastructure.
/// </summary>
public interface IStatisticsPeriodService
{
    /// <summary>
    /// Opens a new statistics period for each group in the space when a trial begins.
    /// </summary>
    /// <param name="spaceId">The space whose trial has started.</param>
    /// <param name="startBoundary">The trial start date used as the new period start.</param>
    /// <param name="ct">Cancellation token.</param>
    Task OnTrialStartedAsync(Guid spaceId, DateTime startBoundary, CancellationToken ct);

    /// <summary>
    /// Closes active statistics periods for each group in the space when the trial expires without activation.
    /// </summary>
    /// <param name="spaceId">The space whose trial has expired.</param>
    /// <param name="endBoundary">The trial end date used as the period end boundary.</param>
    /// <param name="ct">Cancellation token.</param>
    Task OnTrialExpiredAsync(Guid spaceId, DateTime endBoundary, CancellationToken ct);

    /// <summary>
    /// Closes existing active periods and opens new ones when a subscription is activated.
    /// </summary>
    /// <param name="spaceId">The space whose subscription has been activated.</param>
    /// <param name="startBoundary">The subscription activation date used as the new period start.</param>
    /// <param name="ct">Cancellation token.</param>
    Task OnSubscriptionActivatedAsync(Guid spaceId, DateTime startBoundary, CancellationToken ct);

    /// <summary>
    /// Closes active statistics periods for each group in the space when the subscription expires without renewal.
    /// </summary>
    /// <param name="spaceId">The space whose subscription has expired.</param>
    /// <param name="endBoundary">The subscription end date used as the period end boundary.</param>
    /// <param name="ct">Cancellation token.</param>
    Task OnSubscriptionExpiredAsync(Guid spaceId, DateTime endBoundary, CancellationToken ct);

    /// <summary>
    /// Closes active statistics periods and opens new ones when a billing period renews.
    /// </summary>
    /// <param name="spaceId">The space whose billing period has renewed.</param>
    /// <param name="newPeriodStart">The start date of the new billing period.</param>
    /// <param name="ct">Cancellation token.</param>
    Task OnPeriodRenewedAsync(Guid spaceId, DateTime newPeriodStart, CancellationToken ct);
}
