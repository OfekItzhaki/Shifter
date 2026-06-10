using System.Net.Http.Headers;
using System.Net.Http.Json;
using Jobuler.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Email;

/// <summary>
/// Sends emails through Resend's REST API.
/// Requires configuration:
///   Resend:ApiKey     - Resend API key
///   Resend:FromEmail  - verified sender email address
///   Resend:FromName   - display name for the sender
/// </summary>
public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly ILogger<ResendEmailSender> _logger;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public ResendEmailSender(
        HttpClient http,
        IConfiguration config,
        ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _logger = logger;
        _fromEmail = config["Resend:FromEmail"] ?? "noreply@shifter.app";
        _fromName = config["Resend:FromName"] ?? "Shifter";

        var apiKey = config["Resend:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }

        _http.BaseAddress ??= new Uri("https://api.resend.com/");
    }

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        var payload = new
        {
            from = $"{_fromName} <{_fromEmail}>",
            to = new[] { to },
            subject,
            html = htmlBody
        };

        var response = await _http.PostAsJsonAsync("emails", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Resend delivery failed: status={Status} to={To} subject={Subject} body={Body}",
                (int)response.StatusCode, to, subject, body);
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation(
            "Email sent via Resend: to={To} subject={Subject}",
            to,
            subject);
    }
}
