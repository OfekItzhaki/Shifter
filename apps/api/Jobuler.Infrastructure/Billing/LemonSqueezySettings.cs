namespace Jobuler.Infrastructure.Billing;

/// <summary>
/// Configuration settings for the LemonSqueezy billing integration.
/// Loaded from the "LemonSqueezy" configuration section.
/// All values come from environment variables or secrets manager — never hardcoded.
/// </summary>
public class LemonSqueezySettings
{
    /// <summary>
    /// LemonSqueezy API key for authenticating API requests.
    /// Environment variable: LemonSqueezy__ApiKey
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// HMAC secret used to verify webhook request signatures.
    /// Environment variable: LemonSqueezy__WebhookSecret
    /// </summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>
    /// LemonSqueezy store identifier.
    /// Environment variable: LemonSqueezy__StoreId
    /// </summary>
    public string StoreId { get; set; } = "";

    /// <summary>
    /// Default product variant ID used for real subscription checkouts.
    /// Environment variable: LemonSqueezy__DefaultVariantId
    /// </summary>
    public string DefaultVariantId { get; set; } = "";

    /// <summary>
    /// Product variant ID used for test charge checkouts (~$1).
    /// Environment variable: LemonSqueezy__TestVariantId
    /// </summary>
    public string TestVariantId { get; set; } = "";

    /// <summary>
    /// Validates that all required configuration values are present and non-whitespace.
    /// Throws <see cref="InvalidOperationException"/> identifying the specific missing key(s).
    /// </summary>
    public void Validate()
    {
        var missingKeys = new List<string>();

        if (string.IsNullOrWhiteSpace(ApiKey))
            missingKeys.Add(nameof(ApiKey));

        if (string.IsNullOrWhiteSpace(WebhookSecret))
            missingKeys.Add(nameof(WebhookSecret));

        if (string.IsNullOrWhiteSpace(StoreId))
            missingKeys.Add(nameof(StoreId));

        if (string.IsNullOrWhiteSpace(DefaultVariantId))
            missingKeys.Add(nameof(DefaultVariantId));

        if (string.IsNullOrWhiteSpace(TestVariantId))
            missingKeys.Add(nameof(TestVariantId));

        if (missingKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"LemonSqueezy configuration is incomplete. Missing or empty values: {string.Join(", ", missingKeys)}");
        }
    }
}
