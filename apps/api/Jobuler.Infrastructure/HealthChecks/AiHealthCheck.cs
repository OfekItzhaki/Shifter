using System.Diagnostics;
using System.Net.Http.Headers;
using Jobuler.Application.Common.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.HealthChecks;

/// <summary>
/// Checks the configured OpenAI-compatible AI endpoint without sending customer data.
/// Returns skipped when AI is disabled.
/// </summary>
public class AiHealthCheck : IServiceHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AiHealthCheck> _logger;
    private readonly string? _apiKey;
    private readonly string? _configuredBaseUrl;

    public AiHealthCheck(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AiHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKey = configuration["AI:ApiKey"];
        _configuredBaseUrl = configuration["AI:BaseUrl"];
    }

    public string ServiceName => "ai";

    public async Task<ServiceHealthResult> CheckAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) && string.IsNullOrWhiteSpace(_configuredBaseUrl))
        {
            return new ServiceHealthResult(ServiceName, "skipped");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var baseUrl = string.IsNullOrWhiteSpace(_configuredBaseUrl)
                ? "https://api.openai.com/v1"
                : _configuredBaseUrl.TrimEnd('/');

            var client = _httpClientFactory.CreateClient("AI");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models");

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            var response = await client.SendAsync(request, ct);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new ServiceHealthResult(ServiceName, "healthy", ResponseTime: stopwatch.Elapsed);
            }

            var errorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            _logger.LogWarning("AI health check failed: {Error}", errorMessage);
            return new ServiceHealthResult(ServiceName, "unhealthy", errorMessage, stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "AI health check threw an exception");
            return new ServiceHealthResult(ServiceName, "unhealthy", ex.Message, stopwatch.Elapsed);
        }
    }
}
