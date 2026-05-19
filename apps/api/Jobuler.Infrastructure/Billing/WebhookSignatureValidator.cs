using System.Security.Cryptography;
using System.Text;
using Jobuler.Application.Billing;
using Microsoft.Extensions.Options;

namespace Jobuler.Infrastructure.Billing;

/// <summary>
/// Verifies LemonSqueezy webhook signatures using HMAC-SHA256.
/// Compares the computed hash of the raw payload against the signature
/// provided in the webhook request header using a timing-safe comparison.
/// </summary>
public class WebhookSignatureValidator : IWebhookSignatureValidator
{
    private readonly byte[] _secretBytes;

    public WebhookSignatureValidator(IOptions<LemonSqueezySettings> settings)
    {
        _secretBytes = Encoding.UTF8.GetBytes(settings.Value.WebhookSecret);
    }

    /// <inheritdoc />
    public bool Verify(string payload, string signature)
    {
        if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(signature))
            return false;

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var computedHash = HMACSHA256.HashData(_secretBytes, payloadBytes);
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(signature));
    }
}
