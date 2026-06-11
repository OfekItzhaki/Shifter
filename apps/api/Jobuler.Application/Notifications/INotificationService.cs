namespace Jobuler.Application.Notifications;

public interface INotificationService
{
    /// <summary>
    /// Creates a notification for space owner and group owners/admins.
    /// When groupId is provided, only notifies owners of that specific group + space owner.
    /// When groupId is null, notifies space owner + all group owners in the space.
    /// </summary>
    Task NotifySpaceAdminsAsync(
        Guid spaceId, string eventType, string title, string body,
        string? metadataJson = null, Guid? groupId = null, CancellationToken ct = default);

    /// <summary>
    /// Creates the admin notification only when no notification with the same
    /// event type and deduplication hash already exists in the space.
    /// </summary>
    Task NotifySpaceAdminsOnceAsync(
        Guid spaceId, string eventType, string title, string body,
        string? metadataJson, string deduplicationHash, Guid? groupId = null, CancellationToken ct = default);
}
