namespace Jobuler.Application.Common.HealthChecks;

/// <summary>
/// Aggregates all registered service health checks and produces a unified report.
/// </summary>
public interface IHealthCheckRunner
{
    /// <summary>
    /// Runs all registered health checks and returns a consolidated report.
    /// </summary>
    Task<HealthCheckReport> RunAllAsync(CancellationToken ct);
}
