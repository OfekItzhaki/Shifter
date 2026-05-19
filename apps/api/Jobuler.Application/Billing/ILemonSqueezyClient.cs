namespace Jobuler.Application.Billing;

/// <summary>
/// Contract for communicating with the LemonSqueezy API.
/// Defined in Application, implemented in Infrastructure so HTTP concerns stay out of the Application layer.
/// </summary>
public interface ILemonSqueezyClient
{
    /// <summary>
    /// Creates a checkout session on LemonSqueezy and returns the hosted checkout URL.
    /// </summary>
    Task<string> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct = default);
}

/// <summary>
/// Request model for creating a LemonSqueezy checkout session.
/// </summary>
/// <param name="VariantId">The product variant ID to check out.</param>
/// <param name="Metadata">Custom metadata (e.g. spaceId, groupId) attached to the checkout for webhook correlation.</param>
/// <param name="CustomerEmail">Optional pre-filled customer email for the checkout page.</param>
public record CreateCheckoutRequest(
    string VariantId,
    Dictionary<string, string> Metadata,
    string? CustomerEmail = null);
