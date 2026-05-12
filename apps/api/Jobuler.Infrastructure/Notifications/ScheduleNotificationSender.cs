using Jobuler.Application.Common;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Notifications;

/// <summary>
/// Sends schedule notifications via the appropriate channel:
/// - Phone number → WhatsApp (Twilio)
/// - Email address → Email (SendGrid)
/// </summary>
public class ScheduleNotificationSender : IScheduleNotificationSender
{
    private readonly TwilioWhatsAppSender _whatsApp;
    private readonly IEmailSender _email;
    private readonly ILogger<ScheduleNotificationSender> _logger;

    public ScheduleNotificationSender(
        TwilioWhatsAppSender whatsApp,
        IEmailSender email,
        ILogger<ScheduleNotificationSender> logger)
    {
        _whatsApp = whatsApp;
        _email = email;
        _logger = logger;
    }

    public async Task SendSchedulePublishedAsync(
        string contact, string personName, string groupName,
        string scheduleUrl, string locale = "he", CancellationToken ct = default)
    {
        if (IsEmail(contact))
        {
            var (subject, greeting, body, buttonText, dir) = locale switch
            {
                "he" => ($"סידור חדש פורסם — {groupName}", $"שלום {personName},", $"סידור חדש פורסם עבור הקבוצה <strong>{groupName}</strong>.", "צפה בסידור", "rtl"),
                "ru" => ($"Новое расписание — {groupName}", $"Здравствуйте, {personName},", $"Новое расписание опубликовано для группы <strong>{groupName}</strong>.", "Посмотреть расписание", "ltr"),
                _ => ($"New schedule published — {groupName}", $"Hi {personName},", $"A new schedule has been published for group <strong>{groupName}</strong>.", "View Schedule", "ltr"),
            };
            var html = $"""
                <div dir="{dir}" style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                  <h2>{greeting}</h2>
                  <p>{body}</p>
                  <a href="{scheduleUrl}"
                     style="display:inline-block;background:#3b82f6;color:white;padding:12px 24px;
                            border-radius:8px;text-decoration:none;font-weight:bold;margin:16px 0;">
                    {buttonText}
                  </a>
                </div>
                """;
            await _email.SendAsync(contact, subject, html, ct);
        }
        else
        {
            var message = locale switch
            {
                "he" => $"שלום {personName}! 📋\n\nסידור חדש פורסם עבור הקבוצה *{groupName}*.\n\nלצפייה בסידור:\n{scheduleUrl}",
                "ru" => $"Здравствуйте, {personName}! 📋\n\nНовое расписание для группы *{groupName}*.\n\nПосмотреть:\n{scheduleUrl}",
                _ => $"Hi {personName}! 📋\n\nA new schedule has been published for *{groupName}*.\n\nView schedule:\n{scheduleUrl}",
            };
            await _whatsApp.SendRawAsync(contact, message, ct);
        }
    }

    public async Task SendAssignmentNotificationAsync(
        string contact, string personName, string taskName,
        string startsAt, string endsAt, string groupName,
        CancellationToken ct = default)
    {
        if (IsEmail(contact))
        {
            var subject = $"שיבוץ חדש — {groupName}";
            var html = $"""
                <div dir="rtl" style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                  <h2>שלום {personName},</h2>
                  <p>שובצת למשימה חדשה בקבוצה <strong>{groupName}</strong>:</p>
                  <table style="border-collapse:collapse;width:100%;margin:16px 0;">
                    <tr>
                      <td style="padding:8px;border:1px solid #e2e8f0;font-weight:bold;">משימה</td>
                      <td style="padding:8px;border:1px solid #e2e8f0;">{taskName}</td>
                    </tr>
                    <tr>
                      <td style="padding:8px;border:1px solid #e2e8f0;font-weight:bold;">התחלה</td>
                      <td style="padding:8px;border:1px solid #e2e8f0;">{startsAt}</td>
                    </tr>
                    <tr>
                      <td style="padding:8px;border:1px solid #e2e8f0;font-weight:bold;">סיום</td>
                      <td style="padding:8px;border:1px solid #e2e8f0;">{endsAt}</td>
                    </tr>
                  </table>
                </div>
                """;
            await _email.SendAsync(contact, subject, html, ct);
        }
        else
        {
            var message = $"שלום {personName}! ✅\n\n" +
                          $"שיבוץ חדש בקבוצה *{groupName}*:\n\n" +
                          $"📌 משימה: {taskName}\n" +
                          $"🕐 התחלה: {startsAt}\n" +
                          $"🕐 סיום: {endsAt}";
            await _whatsApp.SendRawAsync(contact, message, ct);
        }
    }

    private static bool IsEmail(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Contains('@');
}
