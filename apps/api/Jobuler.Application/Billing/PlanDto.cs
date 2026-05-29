namespace Jobuler.Application.Billing;

/// <summary>
/// Represents a product variant (plan) from LemonSqueezy, exposed to the pricing page.
/// </summary>
/// <param name="VariantId">LemonSqueezy variant ID used to create checkouts.</param>
/// <param name="Name">Display name of the plan (e.g. "Monthly", "Yearly").</param>
/// <param name="PriceInCents">Price in the store's currency (cents). Divide by 100 for display.</param>
/// <param name="Interval">Billing interval: "month" or "year".</param>
/// <param name="Description">Optional plan description from LemonSqueezy.</param>
/// <param name="SortOrder">Sort position for display ordering.</param>
/// <param name="MemberLimit">Maximum number of members allowed on this plan. Null = unlimited.</param>
public record PlanDto(
    string VariantId,
    string Name,
    int PriceInCents,
    string Interval,
    string? Description,
    int SortOrder,
    int? MemberLimit = null);
