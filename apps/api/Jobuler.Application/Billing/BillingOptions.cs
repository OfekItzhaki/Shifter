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

    /// <summary>
    /// Optional promo coupon code to display in the app (e.g., trial-ended banner).
    /// Created in LemonSqueezy dashboard, shown to users so they can apply it at checkout.
    /// </summary>
    public string? PromoCouponCode { get; set; }

    /// <summary>
    /// Optional discount description for the promo coupon (e.g., "20% הנחה").
    /// </summary>
    public string? PromoCouponLabel { get; set; }
}
