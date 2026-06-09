using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobuler.Application.Billing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobuler.Infrastructure.Billing;

/// <summary>
/// HTTP client for the LemonSqueezy API. Creates checkout sessions via the
/// LemonSqueezy v1 REST API.
/// </summary>
public class LemonSqueezyClient : ILemonSqueezyClient
{
    private readonly HttpClient _httpClient;
    private readonly LemonSqueezySettings _settings;
    private readonly ILogger<LemonSqueezyClient> _logger;
    private readonly IMemoryCache _cache;

    private const string PlansCacheKey = "lemonsqueezy:plans";
    private static readonly TimeSpan PlansCacheDuration = TimeSpan.FromHours(1);
    private static readonly List<PlanDto> FallbackPlans =
    [
        new("", "Starter", 5000, "month", null, 1, 10),
        new("", "Growth", 9000, "month", null, 2, 20),
        new("", "Team", 15000, "month", null, 3, 30),
        new("", "Organization", 25000, "month", null, 4, 50),
        new("", "Unlimited", 35000, "month", null, 5, null)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LemonSqueezyClient(
        HttpClient httpClient,
        IOptions<LemonSqueezySettings> settings,
        ILogger<LemonSqueezyClient> logger,
        IMemoryCache cache)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _cache = cache;

        // Configure default headers for LemonSqueezy API authentication
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
    }

    /// <inheritdoc />
    public async Task<string> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct = default)
    {
        var payload = BuildCheckoutPayload(request);

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            System.Text.Encoding.UTF8,
            "application/vnd.api+json");

        var response = await _httpClient.PostAsync(
            "https://api.lemonsqueezy.com/v1/checkouts",
            jsonContent,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "LemonSqueezy checkout creation failed: Status={Status} Body={Body}",
                (int)response.StatusCode, errorBody);

            var reason = ExtractErrorReason(errorBody);
            throw new InvalidOperationException(
                $"Failed to create LemonSqueezy checkout session: {reason}");
        }

        var result = await response.Content.ReadFromJsonAsync<LemonSqueezyCheckoutResponse>(JsonOptions, ct);
        var checkoutUrl = result?.Data?.Attributes?.Url;

        if (string.IsNullOrWhiteSpace(checkoutUrl))
        {
            _logger.LogError("LemonSqueezy checkout response missing URL. Response body parsed but URL was empty.");
            throw new InvalidOperationException(
                "Failed to create LemonSqueezy checkout session: no checkout URL returned.");
        }

        _logger.LogInformation(
            "LemonSqueezy checkout created: VariantId={VariantId}",
            request.VariantId);

        return checkoutUrl;
    }

    /// <inheritdoc />
    public async Task<List<PlanDto>> GetPlansAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(PlansCacheKey, out List<PlanDto>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var response = await _httpClient.GetAsync(
                "https://api.lemonsqueezy.com/v1/variants", ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "LemonSqueezy variants fetch failed; returning fallback plans. Status={Status} Body={Body}",
                    (int)response.StatusCode, errorBody);

                return CacheFallbackPlans();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var plans = ParseVariantsResponse(json);
            if (plans.Count == 0)
            {
                _logger.LogWarning("LemonSqueezy returned no published subscription plans; returning fallback plans.");
                return CacheFallbackPlans();
            }

            _cache.Set(PlansCacheKey, plans, PlansCacheDuration);

            _logger.LogInformation("Fetched {Count} plans from LemonSqueezy", plans.Count);

            return plans;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LemonSqueezy variants fetch failed; returning fallback plans.");
            return CacheFallbackPlans();
        }
    }

    private List<PlanDto> CacheFallbackPlans()
    {
        var plans = FallbackPlans.ToList();
        _cache.Set(PlansCacheKey, plans, PlansCacheDuration);
        return plans;
    }

    private static List<PlanDto> ParseVariantsResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var plans = new List<PlanDto>();

        if (!doc.RootElement.TryGetProperty("data", out var dataArray))
            return plans;

        foreach (var item in dataArray.EnumerateArray())
        {
            if (!item.TryGetProperty("attributes", out var attrs))
                continue;

            // Only include published subscription variants
            var status = attrs.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() : null;
            if (status != "published")
                continue;

            var isSubscription = attrs.TryGetProperty("is_subscription", out var isSub)
                && isSub.GetBoolean();
            if (!isSubscription)
                continue;

            var variantId = item.TryGetProperty("id", out var idProp)
                ? idProp.GetString() ?? "" : "";

            var name = attrs.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString() ?? "" : "";

            var price = attrs.TryGetProperty("price", out var priceProp)
                ? priceProp.GetInt32() : 0;

            var interval = attrs.TryGetProperty("interval", out var intervalProp)
                ? intervalProp.GetString() ?? "month" : "month";

            var description = attrs.TryGetProperty("description", out var descProp)
                ? descProp.GetString() : null;

            var sort = attrs.TryGetProperty("sort", out var sortProp)
                ? sortProp.GetInt32() : 0;

            plans.Add(new PlanDto(variantId, name, price, interval, description, sort,
                ExtractMemberLimit(name, description)));
        }

        return plans.OrderBy(p => p.SortOrder).ThenBy(p => p.PriceInCents).ToList();
    }

    /// <summary>
    /// Extracts the member limit from the plan name or description.
    /// Looks for patterns like "up to 10 members", "for 20 members", or "unlimited".
    /// Returns null for unlimited plans.
    /// </summary>
    private static int? ExtractMemberLimit(string name, string? description)
    {
        // Check if it's an unlimited plan
        if (name.Contains("unlimited", StringComparison.OrdinalIgnoreCase) ||
            (description?.Contains("unlimited", StringComparison.OrdinalIgnoreCase) ?? false))
            return null;

        // Try to extract number from description (e.g., "up to 10 members", "for up to 30 members")
        var text = description ?? name;
        var match = System.Text.RegularExpressions.Regex.Match(
            text, @"(?:up\s+to|for|עד)\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success && int.TryParse(match.Groups[1].Value, out var limit))
            return limit;

        // Try just finding a number followed by "members" or "חברים"
        match = System.Text.RegularExpressions.Regex.Match(
            text, @"(\d+)\s*(?:members|חברים)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success && int.TryParse(match.Groups[1].Value, out limit))
            return limit;

        return null;
    }

    private object BuildCheckoutPayload(CreateCheckoutRequest request)
    {
        var checkoutDataInner = new Dictionary<string, object>
        {
            ["custom"] = request.Metadata
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            checkoutDataInner["email"] = request.CustomerEmail;
        }

        var attributes = new Dictionary<string, object>
        {
            ["checkout_data"] = checkoutDataInner,
            ["product_options"] = new Dictionary<string, object>
            {
                ["redirect_url"] = request.RedirectUrl ?? ""
            }
        };

        // Remove empty redirect_url to avoid sending it when not set
        if (string.IsNullOrWhiteSpace(request.RedirectUrl))
        {
            ((Dictionary<string, object>)attributes["product_options"]).Remove("redirect_url");
            if (((Dictionary<string, object>)attributes["product_options"]).Count == 0)
                attributes.Remove("product_options");
        }

        return new
        {
            data = new
            {
                type = "checkouts",
                attributes,
                relationships = new
                {
                    store = new
                    {
                        data = new { type = "stores", id = _settings.StoreId }
                    },
                    variant = new
                    {
                        data = new { type = "variants", id = request.VariantId }
                    }
                }
            }
        };
    }

    private static string ExtractErrorReason(string errorBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                errors.GetArrayLength() > 0)
            {
                var firstError = errors[0];
                if (firstError.TryGetProperty("detail", out var detail))
                    return detail.GetString() ?? "Unknown error";
                if (firstError.TryGetProperty("title", out var title))
                    return title.GetString() ?? "Unknown error";
            }

            if (doc.RootElement.TryGetProperty("message", out var message))
                return message.GetString() ?? "Unknown error";
        }
        catch (JsonException)
        {
            // If we can't parse the error body, return a generic message
        }

        return "Checkout creation failed (see server logs for details)";
    }

    // ── Response DTOs (internal) ──────────────────────────────────────────────

    private sealed class LemonSqueezyCheckoutResponse
    {
        [JsonPropertyName("data")]
        public LemonSqueezyCheckoutData? Data { get; set; }
    }

    private sealed class LemonSqueezyCheckoutData
    {
        [JsonPropertyName("attributes")]
        public LemonSqueezyCheckoutAttributes? Attributes { get; set; }
    }

    private sealed class LemonSqueezyCheckoutAttributes
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
