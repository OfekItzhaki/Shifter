namespace Jobuler.Application.Billing;

/// <summary>
/// Verifies the authenticity of incoming LemonSqueezy webhook requests
/// by validating the HMAC-SHA256 signature against the configured webhook secret.
/// Implemented in Infrastructure by WebhookSignatureValidator.
/// </summary>
public interface IWebhookSignatureValidator
{
    /// <summary>
    /// Verifies the HMAC-SHA256 signature of a LemonSqueezy webhook payload.
    /// </summary>
    /// <param name="payload">The raw request body string.</param>
    /// <param name="signature">The signature from the X-Signature header.</param>
    /// <returns>True if the signature is valid; otherwise false.</returns>
    bool Verify(string payload, string signature);
}
