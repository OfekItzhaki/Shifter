namespace Jobuler.Application.Common;

/// <summary>
/// Abstraction for sending person invitations via email or WhatsApp.
/// Implementations are registered in Infrastructure.
/// Default: NoOpInvitationSender (logs to console for local dev).
/// </summary>
public interface IInvitationSender
{
    /// <summary>
    /// Sends an invitation to join the space.
    /// </summary>
    /// <param name="contact">Email address or phone number of the recipient.</param>
    /// <param name="channel">"email" or "whatsapp"</param>
    /// <param name="inviteUrl">The full URL the recipient clicks to accept.</param>
    /// <param name="personName">The person's display name for the message body.</param>
    Task SendInvitationAsync(
        string contact,
        string channel,
        string inviteUrl,
        string personName,
        CancellationToken ct = default);
}
