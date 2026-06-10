using Jobuler.Application.Common;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Notifications;

/// <summary>
/// Routes notifications to the correct channel based on the recipient address:
/// - Phone number (starts with + or digits) → WhatsApp via Twilio
/// - Email address (contains @) → Email via configured IEmailSender
///
/// This replaces the NoOpNotificationSender in production.
/// </summary>
public class RoutingNotificationSender : INotificationSender
{
    private readonly TwilioWhatsAppSender _whatsApp;
    private readonly IEmailSender _email;
    private readonly ILogger<RoutingNotificationSender> _logger;
    private readonly string _frontendBaseUrl;

    public RoutingNotificationSender(
        TwilioWhatsAppSender whatsApp,
        IEmailSender email,
        ILogger<RoutingNotificationSender> logger,
        Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _whatsApp = whatsApp;
        _email = email;
        _logger = logger;
        _frontendBaseUrl = config["App:FrontendBaseUrl"]?.TrimEnd('/') ?? "https://shifter.ofeklabs.com";
    }

    public async Task SendPasswordResetAsync(string to, string token, CancellationToken ct = default)
    {
        if (IsEmail(to))
        {
            var resetUrl = $"{_frontendBaseUrl}/reset-password?token={token}";
            var subject = "Password Reset — Shifter";
            var html = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
                  <h2>Password Reset</h2>
                  <p>We received a request to reset your Shifter password.</p>
                  <p>Click the button below to reset your password:</p>
                  <a href="{resetUrl}"
                     style="display:inline-block;background:#3b82f6;color:white;padding:12px 24px;
                            border-radius:8px;text-decoration:none;font-weight:bold;margin:16px 0;">
                    Reset Password
                  </a>
                  <p style="color:#64748b;font-size:12px;">
                    This link is valid for one hour. If you did not request a password reset, you can ignore this message.
                  </p>
                </div>
                """;
            await _email.SendAsync(to, subject, html, ct);
        }
        else
        {
            // Phone number → WhatsApp
            await _whatsApp.SendPasswordResetAsync(to, token, ct);
        }
    }

    private static bool IsEmail(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Contains('@');
}
