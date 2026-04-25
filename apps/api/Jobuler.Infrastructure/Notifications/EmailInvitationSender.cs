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

        var subject = $"הוזמנת להצטרף ל-Jobuler";
        var html = $"""
            <div dir="rtl" style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
              <h2>שלום {personName},</h2>
              <p>הוזמנת להצטרף למרחב עבודה ב-Jobuler.</p>
              <p>לחץ על הכפתור למטה כדי לאשר את ההזמנה:</p>
              <a href="{inviteUrl}"
                 style="display:inline-block;background:#3b82f6;color:white;padding:12px 24px;
                        border-radius:8px;text-decoration:none;font-weight:bold;margin:16px 0;">
                אשר הזמנה
              </a>
              <p style="color:#64748b;font-size:12px;">
                הקישור תקף ל-7 ימים. אם לא ביקשת הזמנה זו, ניתן להתעלם מהודעה זו.
              </p>
            </div>
            """;

        return _emailSender.SendAsync(contact, subject, html, ct);
    }
}
