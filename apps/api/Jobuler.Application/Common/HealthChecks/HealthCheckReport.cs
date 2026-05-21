namespace Jobuler.Application.Common.HealthChecks;

/// <summary>
/// The consolidated health check report returned by the detailed health endpoint.
/// </summary>
/// <param name="OverallStatus">Overall system status: "healthy" or "degraded".</param>
/// <param name="Version">The application version string.</param>
/// <param name="Timestamp">UTC timestamp of when the report was generated.</param>
/// <param name="Checks">Individual service health check results.</param>
public record HealthCheckReport(
    string OverallStatus,
    string Version,
    DateTime Timestamp,
    IReadOnlyList<ServiceHealthResult> Checks);
