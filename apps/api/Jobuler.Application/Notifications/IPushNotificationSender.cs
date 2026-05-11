namespace Jobuler.Application.Notifications;

/// <summary>
/// Abstraction for sending web push notifications to subscribed devices.
/// Implemented in Infrastructure using VAPID-authenticated, RFC 8291 encrypted delivery.
/// </summary>
public interface IPushNotificationSender
{
    /// <summary>
    /// Sends a push notification to all subscribed devices for a user in a space.
    /// Silently removes expired/invalid subscriptions (410 Gone responses).
    /// </summary>
    Task SendPushToUserAsync(
        Guid userId, Guid spaceId,
        PushPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Sends push notifications to all subscribed devices for multiple users.
    /// Used when notifying all space members.
    /// </summary>
    Task SendPushToUsersAsync(
        IEnumerable<Guid> userIds, Guid spaceId,
        PushPayload payload, CancellationToken ct = default);
}

/// <summary>
/// Payload for a push notification message.
/// </summary>
public record PushPayload(
    string Title,
    string Body,
    string? Icon = null,
    string? Url = null,
    string? Tag = null);
