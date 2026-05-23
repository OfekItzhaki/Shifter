using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobuler.Application.Billing;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LemonSqueezyClient(
        HttpClient httpClient,
        IOptions<LemonSqueezySettings> settings,
        ILogger<LemonSqueezyClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

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
            ["checkout_data"] = checkoutDataInner
        };

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
