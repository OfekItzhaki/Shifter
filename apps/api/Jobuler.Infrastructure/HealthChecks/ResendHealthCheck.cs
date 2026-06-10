using System.Diagnostics;
using System.Net.Http.Headers;
using Jobuler.Application.Common.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.HealthChecks;

/// <summary>
/// Checks Resend API connectivity by calling /domains.
/// Returns "skipped" when the API key is not configured.
/// </summary>
public class ResendHealthCheck : IServiceHealthCheck
{
    public string ServiceName => "resend";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ResendHealthCheck> _logger;
    private readonly string? _apiKey;

    public ResendHealthCheck(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ResendHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKey = configuration["Resend:ApiKey"];
    }

    public async Task<ServiceHealthResult> CheckAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new ServiceHealthResult(ServiceName, "skipped");
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var client = _httpClientFactory.CreateClient("Resend");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await client.GetAsync("https://api.resend.com/domains", ct);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new ServiceHealthResult(ServiceName, "healthy", ResponseTime: sw.Elapsed);
            }

            var errorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            _logger.LogWarning("Resend health check failed: {Error}", errorMessage);
            return new ServiceHealthResult(ServiceName, "unhealthy", errorMessage, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Resend health check threw an exception");
            return new ServiceHealthResult(ServiceName, "unhealthy", ex.Message, sw.Elapsed);
        }
    }
}
