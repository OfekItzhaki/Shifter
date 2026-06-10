namespace Jobuler.Application.Common;

/// <summary>
/// Abstraction for sending emails. Implementations are registered in Infrastructure.
/// Default: NoOpEmailSender (logs intent, no actual send).
/// Swap for a real provider (Resend, SES, SMTP, etc.) without changing business logic.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
