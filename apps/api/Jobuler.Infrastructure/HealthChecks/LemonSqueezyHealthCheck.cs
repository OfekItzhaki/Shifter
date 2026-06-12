using System.Diagnostics;
using System.Net.Http.Headers;
using Jobuler.Application.Common.HealthChecks;
using Jobuler.Infrastructure.Billing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobuler.Infrastructure.HealthChecks;

/// <summary>
/// Health check for the LemonSqueezy API.
/// Validates the API key by making an authenticated GET request to /v1/users/me.
/// </summary>
public class LemonSqueezyHealthCheck : IServiceHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LemonSqueezySettings _settings;
    private readonly ILogger<LemonSqueezyHealthCheck> _logger;

    public string ServiceName => "lemonsqueezy";

    public LemonSqueezyHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<LemonSqueezySettings> settings,
        ILogger<LemonSqueezyHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ServiceHealthResult> CheckAsync(CancellationToken ct)
    {
        var missingSettings = GetMissingRequiredSettings();
        if (missingSettings.Count == 5)
        {
            return new ServiceHealthResult(ServiceName, "skipped");
        }

        if (missingSettings.Count > 0)
        {
            return new ServiceHealthResult(
                ServiceName,
                "unhealthy",
                $"Missing required LemonSqueezy configuration: {string.Join(", ", missingSettings)}");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var client = _httpClientFactory.CreateClient("LemonSqueezy");
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.lemonsqueezy.com/v1/users/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

            var response = await client.SendAsync(request, ct);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new ServiceHealthResult(ServiceName, "healthy", ResponseTime: stopwatch.Elapsed);
            }

            var errorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            _logger.LogWarning("LemonSqueezy health check failed: {Error}", errorMessage);
            return new ServiceHealthResult(ServiceName, "unhealthy", errorMessage, stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "LemonSqueezy health check threw an exception");
            return new ServiceHealthResult(ServiceName, "unhealthy", ex.Message, stopwatch.Elapsed);
        }
    }

    private List<string> GetMissingRequiredSettings()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            missing.Add("LemonSqueezy__ApiKey");

        if (string.IsNullOrWhiteSpace(_settings.WebhookSecret))
            missing.Add("LemonSqueezy__WebhookSecret");

        if (string.IsNullOrWhiteSpace(_settings.StoreId))
            missing.Add("LemonSqueezy__StoreId");

        if (string.IsNullOrWhiteSpace(_settings.DefaultVariantId))
            missing.Add("LemonSqueezy__DefaultVariantId");

        if (string.IsNullOrWhiteSpace(_settings.TestVariantId))
            missing.Add("LemonSqueezy__TestVariantId");

        return missing;
    }
}
