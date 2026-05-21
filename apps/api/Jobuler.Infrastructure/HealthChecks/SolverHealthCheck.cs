using System.Diagnostics;
using Jobuler.Application.Common.HealthChecks;

namespace Jobuler.Infrastructure.HealthChecks;

/// <summary>
/// Health check for the Solver service reachability.
/// Makes an HTTP GET request to the Solver base URL to verify the service is up.
/// </summary>
public class SolverHealthCheck : IServiceHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SolverHealthCheck(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string ServiceName => "solver";

    public async Task<ServiceHealthResult> CheckAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient("Solver");
            var response = await client.GetAsync("/", ct);
            response.EnsureSuccessStatusCode();
            stopwatch.Stop();

            return new ServiceHealthResult(
                ServiceName,
                "healthy",
                ResponseTime: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new ServiceHealthResult(
                ServiceName,
                "unhealthy",
                ErrorMessage: ex.Message,
                ResponseTime: stopwatch.Elapsed);
        }
    }
}
