using Jobuler.Application.Common;

namespace Jobuler.Infrastructure.Notifications;

/// <summary>
/// Routes invitation delivery to the correct sender based on channel.
/// </summary>
public class CompositeInvitationSender : IInvitationSender
{
    private readonly EmailInvitationSender _email;
    private readonly WhatsAppInvitationSender _whatsApp;
    private readonly NoOpInvitationSender _noOp;

    public CompositeInvitationSender(
        EmailInvitationSender email,
        WhatsAppInvitationSender whatsApp,
        NoOpInvitationSender noOp)
    {
        _email = email;
        _whatsApp = whatsApp;
        _noOp = noOp;
    }

    public Task SendInvitationAsync(
        string contact, string channel, string inviteUrl, string personName,
        CancellationToken ct = default) =>
        channel.ToLowerInvariant() switch
        {
            "email"    => _email.SendInvitationAsync(contact, channel, inviteUrl, personName, ct),
            "whatsapp" => _whatsApp.SendInvitationAsync(contact, channel, inviteUrl, personName, ct),
            _          => _noOp.SendInvitationAsync(contact, channel, inviteUrl, personName, ct)
        };
}
