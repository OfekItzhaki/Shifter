namespace Jobuler.Application.Billing;

/// <summary>
/// Configuration options for the billing feature.
/// Bound from the "LemonSqueezy" section in appsettings.json.
/// </summary>
public class BillingOptions
{
    /// <summary>
    /// Default product variant ID used for real subscription checkouts.
    /// </summary>
    public string DefaultVariantId { get; set; } = "";

    /// <summary>
    /// Product variant ID used for test charge checkouts (~$1).
    /// </summary>
    public string TestVariantId { get; set; } = "";
}
