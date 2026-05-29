using Jobuler.Application.Common;

namespace Jobuler.Infrastructure.Notifications;

/// <summary>
/// Sends group invitations via WhatsApp using TwilioWhatsAppSender.
/// Uses a friendly Hebrew message with the invite link.
/// </summary>
public class WhatsAppInvitationSender : IInvitationSender
{
    private readonly TwilioWhatsAppSender _twilio;

    public WhatsAppInvitationSender(TwilioWhatsAppSender twilio)
    {
        _twilio = twilio;
    }

    public Task SendInvitationAsync(
        string contact, string channel, string inviteUrl, string personName,
        CancellationToken ct = default)
    {
        if (channel != "whatsapp") return Task.CompletedTask;

        var message = $"Hi {personName}! 👋\n\n" +
                      $"You've been invited to join a group on Shifter.\n\n" +
                      $"Click the link to accept the invitation:\n{inviteUrl}\n\n" +
                      $"This link is valid for 7 days.";

        return _twilio.SendRawAsync(contact, message, ct);
    }
}
