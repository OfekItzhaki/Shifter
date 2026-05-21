using Jobuler.Application.Common.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobuler.Infrastructure.HealthChecks;

/// <summary>
/// Sends high-priority push notifications to the platform operator via the Pushover API.
/// Gracefully degrades when credentials are not configured.
/// </summary>
public class PushoverNotifier : IPushoverNotifier
{
    private const string PushoverApiUrl = "https://api.pushover.net/1/messages.json";
    private const string AlertTitle = "Shifter Health Alert";
    private const int HighPriority = 1;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HealthCheckOptions _options;
    private readonly ILogger<PushoverNotifier> _logger;

    public PushoverNotifier(
        IHttpClientFactory httpClientFactory,
        IOptions<HealthCheckOptions> options,
        ILogger<PushoverNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAlertAsync(string serviceName, DateTime detectedAtUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.PushoverUserKey) ||
            string.IsNullOrWhiteSpace(_options.PushoverAppToken))
        {
            _logger.LogWarning(
                "Pushover credentials are not configured. Skipping notification for service {ServiceName}",
                serviceName);
            return;
        }

        var message = $"Service '{serviceName}' is unhealthy. Detected at {detectedAtUtc:yyyy-MM-dd HH:mm:ss} UTC.";

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = _options.PushoverAppToken,
            ["user"] = _options.PushoverUserKey,
            ["message"] = message,
            ["priority"] = HighPriority.ToString(),
            ["title"] = AlertTitle
        });

        try
        {
            var client = _httpClientFactory.CreateClient("Pushover");
            var response = await client.PostAsync(PushoverApiUrl, formContent, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Pushover notification failed for service {ServiceName}. Status code: {StatusCode}",
                    serviceName,
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Pushover notification failed for service {ServiceName}",
                serviceName);
        }
    }
}
