using Jobuler.Application.Common;

namespace Jobuler.Infrastructure.Notifications;

/// <summary>
/// Sends invitations via WhatsApp using INotificationSender.
/// Reuses the existing notification abstraction (same channel as password reset).
/// </summary>
public class WhatsAppInvitationSender : IInvitationSender
{
    private readonly INotificationSender _notificationSender;

    public WhatsAppInvitationSender(INotificationSender notificationSender)
    {
        _notificationSender = notificationSender;
    }

    public Task SendInvitationAsync(
        string contact, string channel, string inviteUrl, string personName,
        CancellationToken ct = default)
    {
        if (channel != "whatsapp") return Task.CompletedTask;

        // Reuse the notification sender with the invite URL as the "token"
        // In production, implement a dedicated WhatsApp template message
        return _notificationSender.SendPasswordResetAsync(contact, inviteUrl, ct);
    }
}
