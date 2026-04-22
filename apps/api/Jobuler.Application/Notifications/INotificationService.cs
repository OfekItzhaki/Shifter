namespace Jobuler.Application.Notifications;

public interface INotificationService
{
    /// <summary>
    /// Creates a notification for every space member who has the SpaceView permission.
    /// Called by the solver worker after a run completes or fails.
    /// </summary>
    Task NotifySpaceAdminsAsync(
        Guid spaceId, string eventType, string title, string body,
        string? metadataJson = null, CancellationToken ct = default);
}
