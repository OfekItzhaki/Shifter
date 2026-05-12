namespace Jobuler.Application.Common;

/// <summary>
/// Sends schedule-related notifications to group members.
/// Used when a schedule is published or when a member's assignment changes.
/// Implementations route to WhatsApp or email based on the member's contact info.
/// </summary>
public interface IScheduleNotificationSender
{
    /// <summary>
    /// Notifies a member that a new schedule has been published.
    /// </summary>
    Task SendSchedulePublishedAsync(
        string contact,
        string personName,
        string groupName,
        string scheduleUrl,
        string locale = "he",
        CancellationToken ct = default);

    /// <summary>
    /// Notifies a member of their specific assignment in the new schedule.
    /// </summary>
    Task SendAssignmentNotificationAsync(
        string contact,
        string personName,
        string taskName,
        string startsAt,
        string endsAt,
        string groupName,
        string locale = "he",
        CancellationToken ct = default);
}
