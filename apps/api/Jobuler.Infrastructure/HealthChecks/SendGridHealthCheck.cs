using System.Diagnostics;
using System.Net.Http.Headers;
using Jobuler.Application.Common.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.HealthChecks;

/// <summary>
/// Checks SendGrid API connectivity by making an authenticated GET request
/// to /v3/user/profile. Returns "skipped" when the API key is not configured.
/// </summary>
public class SendGridHealthCheck : IServiceHealthCheck
{
    public string ServiceName => "sendgrid";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SendGridHealthCheck> _logger;
    private readonly string? _apiKey;

    public SendGridHealthCheck(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SendGridHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKey = configuration["SendGrid:ApiKey"];
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
            var client = _httpClientFactory.CreateClient("SendGrid");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await client.GetAsync("https://api.sendgrid.com/v3/user/profile", ct);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new ServiceHealthResult(ServiceName, "healthy", ResponseTime: sw.Elapsed);
            }

            var errorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            _logger.LogWarning("SendGrid health check failed: {Error}", errorMessage);
            return new ServiceHealthResult(ServiceName, "unhealthy", errorMessage, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "SendGrid health check threw an exception");
            return new ServiceHealthResult(ServiceName, "unhealthy", ex.Message, sw.Elapsed);
        }
    }
}
