using Jobuler.Application.Common;

namespace Jobuler.Infrastructure.Notifications;

/// <summary>
/// Sends invitations via email using IEmailSender.
/// </summary>
public class EmailInvitationSender : IInvitationSender
{
    private readonly IEmailSender _emailSender;

    public EmailInvitationSender(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public Task SendInvitationAsync(
        string contact, string channel, string inviteUrl, string personName,
        CancellationToken ct = default)
    {
        if (channel != "email") return Task.CompletedTask;

        var subject = $"You've been invited to join Shifter";
        var html = $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
              <h2>Hi {personName},</h2>
              <p>You've been invited to join a workspace on Shifter.</p>
              <p>Click the button below to accept the invitation:</p>
              <a href="{inviteUrl}"
                 style="display:inline-block;background:#3b82f6;color:white;padding:12px 24px;
                        border-radius:8px;text-decoration:none;font-weight:bold;margin:16px 0;">
                Accept Invitation
              </a>
              <p style="color:#64748b;font-size:12px;">
                This link is valid for 7 days. If you did not request this invitation, you can ignore this message.
              </p>
            </div>
            """;

        return _emailSender.SendAsync(contact, subject, html, ct);
    }
}
