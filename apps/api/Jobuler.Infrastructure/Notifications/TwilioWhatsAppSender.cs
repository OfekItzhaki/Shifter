using Jobuler.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Jobuler.Infrastructure.Notifications;

/// <summary>
/// Sends WhatsApp messages via Twilio.
/// Requires configuration:
///   Twilio:AccountSid     — your Twilio Account SID
///   Twilio:AuthToken      — your Twilio Auth Token
///   Twilio:WhatsAppFrom   — your Twilio WhatsApp sender number, e.g. "whatsapp:+14155238886"
///                           (use the Twilio sandbox number for testing)
///
/// If credentials are not configured, falls back to no-op (logs + skips send).
///
/// WhatsApp message format:
/// - Password reset: sends the reset link directly
/// - Invitations: sends the invite link with a friendly Hebrew message
/// - Schedule notifications: sends a short Hebrew summary
/// </summary>
public class TwilioWhatsAppSender : INotificationSender
{
    private readonly ILogger<TwilioWhatsAppSender> _logger;
    private readonly string? _accountSid;
    private readonly string? _authToken;
    private readonly string? _from;

    public TwilioWhatsAppSender(IConfiguration config, ILogger<TwilioWhatsAppSender> logger)
    {
        _logger = logger;
        _accountSid = config["Twilio:AccountSid"];
        _authToken = config["Twilio:AuthToken"];
        _from = config["Twilio:WhatsAppFrom"];
    }

    public async Task SendPasswordResetAsync(string to, string token, CancellationToken ct = default)
    {
        // Determine if `to` is a phone number or email
        // Phone numbers start with + or are all digits
        if (IsPhoneNumber(to))
        {
            var message = $"Hi! Here is your Shifter password reset link:\n{token}\n\nThis link is valid for one hour.";
            await SendWhatsAppAsync(to, message, ct);
        }
        else
        {
            // Email fallback — log only, the caller should use IEmailSender for email addresses
            _logger.LogWarning(
                "TwilioWhatsAppSender received an email address instead of a phone number: {To}. " +
                "Use IEmailSender for email delivery.",
                to);
        }
    }

    /// <summary>
    /// Sends a raw WhatsApp message to a phone number.
    /// The number should be in E.164 format (e.g. +972501234567).
    /// </summary>
    public async Task SendRawAsync(string toPhone, string message, CancellationToken ct = default)
        => await SendWhatsAppAsync(toPhone, message, ct);

    private async Task SendWhatsAppAsync(string toPhone, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_accountSid) ||
            string.IsNullOrWhiteSpace(_authToken) ||
            string.IsNullOrWhiteSpace(_from))
        {
            _logger.LogWarning(
                "Twilio credentials not configured — WhatsApp message not sent to={To}",
                toPhone);
            return;
        }

        try
        {
            TwilioClient.Init(_accountSid, _authToken);

            var toWhatsApp = toPhone.StartsWith("whatsapp:") ? toPhone : $"whatsapp:{toPhone}";

            var msg = await MessageResource.CreateAsync(
                to: new PhoneNumber(toWhatsApp),
                from: new PhoneNumber(_from),
                body: message);

            _logger.LogInformation(
                "WhatsApp message sent via Twilio: sid={Sid} to={To} status={Status}",
                msg.Sid, toPhone, msg.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Twilio WhatsApp delivery failed: to={To}",
                toPhone);
        }
    }

    private static bool IsPhoneNumber(string value)
        => !string.IsNullOrWhiteSpace(value) &&
           (value.StartsWith("+") || value.All(c => char.IsDigit(c) || c == '+' || c == '-' || c == ' '));
}
