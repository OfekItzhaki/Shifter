using Jobuler.Application.Common;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Email;

/// <summary>
/// Default no-op email sender. Logs the intent at Debug level without sending anything.
/// Replace with a real implementation (Resend, SES, SMTP) by registering a different
/// IEmailSender in DI — no business logic changes required.
/// </summary>
public class NoOpEmailSender : IEmailSender
{
    private readonly ILogger<NoOpEmailSender> _logger;

    public NoOpEmailSender(ILogger<NoOpEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "NoOpEmailSender: would send email to={To} subject={Subject} (no provider configured)",
            to, subject);
        return Task.CompletedTask;
    }
}
