namespace Jobuler.Application.Common.HealthChecks;

/// <summary>
/// The result of an individual service health check.
/// </summary>
/// <param name="ServiceName">The name of the service that was checked.</param>
/// <param name="Status">The health status: "healthy", "unhealthy", or "skipped".</param>
/// <param name="ErrorMessage">Optional error message when the service is unhealthy.</param>
/// <param name="ResponseTime">Optional response time measurement for the check.</param>
/// <param name="Details">Optional non-sensitive service metadata for authenticated detailed health checks.</param>
public record ServiceHealthResult(
    string ServiceName,
    string Status,
    string? ErrorMessage = null,
    TimeSpan? ResponseTime = null,
    IReadOnlyDictionary<string, string>? Details = null);
