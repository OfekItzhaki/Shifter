using System.Net.Http.Json;
using System.Text.Json;
using Jobuler.Application.Billing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobuler.Infrastructure.Billing;

/// <summary>
/// In-memory cache for the trial duration configured in the LemonSqueezy product variant.
/// Syncs from the LemonSqueezy API every 6 hours. Falls back to 14 days when the cache
/// is unavailable or has never been populated.
/// Registered as a singleton so the cached value persists across requests.
/// </summary>
public class TrialDurationCache : ITrialDurationCache
{
    private const int DefaultTrialDays = 14;
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(6);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LemonSqueezySettings _settings;
    private readonly ILogger<TrialDurationCache> _logger;

    private int? _cachedDays;
    private DateTime _lastSync = DateTime.MinValue;

    public TrialDurationCache(
        IHttpClientFactory httpClientFactory,
        IOptions<LemonSqueezySettings> settings,
        ILogger<TrialDurationCache> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> GetTrialDaysAsync(CancellationToken ct = default)
    {
        if (_cachedDays.HasValue && DateTime.UtcNow - _lastSync < SyncInterval)
            return Task.FromResult(_cachedDays.Value);

        // Cache is stale or never populated — return last known value or default
        return Task.FromResult(_cachedDays ?? DefaultTrialDays);
    }

    /// <inheritdoc />
    public async Task SyncFromLemonSqueezyAsync(CancellationToken ct = default)
    {
        try
        {
            var variantId = _settings.DefaultVariantId;
            if (string.IsNullOrWhiteSpace(variantId))
            {
                _logger.LogWarning("TrialDurationCache sync skipped: DefaultVariantId is not configured");
                return;
            }

            using var client = _httpClientFactory.CreateClient("TrialDurationCache");

            var response = await client.GetAsync(
                $"v1/variants/{variantId}",
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "TrialDurationCache sync failed: LemonSqueezy returned {StatusCode}",
                    (int)response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var trialDays = ExtractTrialDurationDays(json);

            if (trialDays.HasValue)
            {
                _cachedDays = trialDays.Value;
                _lastSync = DateTime.UtcNow;
                _logger.LogInformation(
                    "TrialDurationCache synced: trial_duration_days={TrialDays}",
                    trialDays.Value);
            }
            else
            {
                _logger.LogWarning(
                    "TrialDurationCache sync: trial_duration_days not found in variant attributes");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TrialDurationCache sync failed — keeping existing cached value");
        }
    }

    /// <summary>
    /// Extracts the trial_duration_days value from the LemonSqueezy variant JSON:API response.
    /// Expected path: data.attributes.trial_duration_days
    /// </summary>
    private static int? ExtractTrialDurationDays(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("attributes", out var attributes) &&
                attributes.TryGetProperty("trial_duration_days", out var trialDuration))
            {
                if (trialDuration.ValueKind == JsonValueKind.Number)
                    return trialDuration.GetInt32();

                // Handle string representation
                if (trialDuration.ValueKind == JsonValueKind.String &&
                    int.TryParse(trialDuration.GetString(), out var parsed))
                    return parsed;
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — caller will log the warning
        }

        return null;
    }
}
