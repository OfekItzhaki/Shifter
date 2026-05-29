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
                "he" => ($"New schedule published — {groupName}", $"Hi {personName},", $"A new schedule has been published for group <strong>{groupName}</strong>.", "View Schedule", "rtl"),
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
                "he" => $"Hi {personName}! 📋\n\nA new schedule has been published for *{groupName}*.\n\nView schedule:\n{scheduleUrl}",
                "ru" => $"Здравствуйте, {personName}! 📋\n\nНовое расписание для группы *{groupName}*.\n\nПосмотреть:\n{scheduleUrl}",
                _ => $"Hi {personName}! 📋\n\nA new schedule has been published for *{groupName}*.\n\nView schedule:\n{scheduleUrl}",
            };
            await _whatsApp.SendRawAsync(contact, message, ct);
        }
    }

    public async Task SendAssignmentNotificationAsync(
        string contact, string personName, string taskName,
        string startsAt, string endsAt, string groupName,
        string locale = "he", CancellationToken ct = default)
    {
        if (IsEmail(contact))
        {
            var (subject, dir, greeting, body, taskLabel, startLabel, endLabel) = locale switch
            {
                "he" => ($"New assignment — {groupName}", "rtl", $"Hi {personName},", $"You've been assigned a new task in group <strong>{groupName}</strong>:", "Task", "Start", "End"),
                "ru" => ($"Новое назначение — {groupName}", "ltr", $"Здравствуйте, {personName},", $"Вам назначена новая задача в группе <strong>{groupName}</strong>:", "Задача", "Начало", "Конец"),
                _ => ($"New assignment — {groupName}", "ltr", $"Hi {personName},", $"You've been assigned a new task in group <strong>{groupName}</strong>:", "Task", "Start", "End"),
            };
            var html = $"""
                <div dir="{dir}" style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                  <h2>{greeting}</h2>
                  <p>{body}</p>
                  <table style="border-collapse:collapse;width:100%;margin:16px 0;">
                    <tr>
                      <td style="padding:8px;border:1px solid #e2e8f0;font-weight:bold;">{taskLabel}</td>
                      <td style="padding:8px;border:1px solid #e2e8f0;">{taskName}</td>
                    </tr>
                    <tr>
                      <td style="padding:8px;border:1px solid #e2e8f0;font-weight:bold;">{startLabel}</td>
                      <td style="padding:8px;border:1px solid #e2e8f0;">{startsAt}</td>
                    </tr>
                    <tr>
                      <td style="padding:8px;border:1px solid #e2e8f0;font-weight:bold;">{endLabel}</td>
                      <td style="padding:8px;border:1px solid #e2e8f0;">{endsAt}</td>
                    </tr>
                  </table>
                </div>
                """;
            await _email.SendAsync(contact, subject, html, ct);
        }
        else
        {
            var message = locale switch
            {
                "he" => $"Hi {personName}! ✅\n\nNew assignment in *{groupName}*:\n\n📌 Task: {taskName}\n🕐 Start: {startsAt}\n🕐 End: {endsAt}",
                "ru" => $"Здравствуйте, {personName}! ✅\n\nНовое назначение в группе *{groupName}*:\n\n📌 Задача: {taskName}\n🕐 Начало: {startsAt}\n🕐 Конец: {endsAt}",
                _ => $"Hi {personName}! ✅\n\nNew assignment in *{groupName}*:\n\n📌 Task: {taskName}\n🕐 Start: {startsAt}\n🕐 End: {endsAt}",
            };
            await _whatsApp.SendRawAsync(contact, message, ct);
        }
    }

    private static bool IsEmail(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Contains('@');
}
