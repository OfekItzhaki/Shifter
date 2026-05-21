namespace Jobuler.Application.Common.HealthChecks;

/// <summary>
/// Contract for an individual service health check.
/// Each monitored service (PostgreSQL, Redis, etc.) implements this interface.
/// </summary>
public interface IServiceHealthCheck
{
    /// <summary>
    /// The display name of the service being checked (e.g., "postgres", "redis").
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Executes the health check for this service.
    /// </summary>
    Task<ServiceHealthResult> CheckAsync(CancellationToken ct);
}
