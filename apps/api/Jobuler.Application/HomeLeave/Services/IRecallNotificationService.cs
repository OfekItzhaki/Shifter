namespace Jobuler.Application.HomeLeave.Services;

/// <summary>
/// Sends recall notifications (push + email) to a person being recalled from home leave.
/// Implementations handle retry logic for push delivery and graceful failure for email.
/// </summary>
public interface IRecallNotificationService
{
    /// <summary>
    /// Sends a recall notification to the specified person via push and email channels.
    /// </summary>
    /// <param name="spaceId">The space (tenant) in which the recall occurs.</param>
    /// <param name="recalledPersonId">The ID of the person being recalled.</param>
    /// <param name="adminName">The name of the admin who initiated the recall.</param>
    /// <param name="reason">Optional free-text reason for the recall.</param>
    /// <param name="expectedReturnAt">Optional expected return date/time.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the push notification was delivered successfully; false otherwise.</returns>
    Task<bool> SendRecallNotificationAsync(
        Guid spaceId,
        Guid recalledPersonId,
        string adminName,
        string? reason,
        DateTime? expectedReturnAt,
        CancellationToken ct = default);
}
