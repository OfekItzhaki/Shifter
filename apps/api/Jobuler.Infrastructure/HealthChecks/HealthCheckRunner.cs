using System.Reflection;
using Jobuler.Application.Common.HealthChecks;

namespace Jobuler.Infrastructure.HealthChecks;

/// <summary>
/// Aggregates all registered service health checks and produces a unified report.
/// Each check is executed with a 10-second timeout. Timed-out or failed checks
/// are marked as "unhealthy". Overall status is "healthy" only if all non-skipped
/// checks pass; otherwise "degraded".
/// </summary>
public class HealthCheckRunner : IHealthCheckRunner
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(10);

    private readonly IEnumerable<IServiceHealthCheck> _checks;

    public HealthCheckRunner(IEnumerable<IServiceHealthCheck> checks)
    {
        _checks = checks;
    }

    public async Task<HealthCheckReport> RunAllAsync(CancellationToken ct)
    {
        var results = new List<ServiceHealthResult>();

        foreach (var check in _checks)
        {
            var result = await RunCheckWithTimeoutAsync(check, ct);
            results.Add(result);
        }

        var overallStatus = DeriveOverallStatus(results);
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        var timestamp = DateTime.UtcNow;

        return new HealthCheckReport(overallStatus, version, timestamp, results);
    }

    private static async Task<ServiceHealthResult> RunCheckWithTimeoutAsync(
        IServiceHealthCheck check, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(CheckTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            return await check.CheckAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return new ServiceHealthResult(
                check.ServiceName,
                "unhealthy",
                ErrorMessage: "Health check timed out after 10 seconds");
        }
        catch (Exception ex)
        {
            return new ServiceHealthResult(
                check.ServiceName,
                "unhealthy",
                ErrorMessage: ex.Message);
        }
    }

    internal static string DeriveOverallStatus(IReadOnlyList<ServiceHealthResult> results)
    {
        var nonSkipped = results.Where(r => r.Status != "skipped");
        return nonSkipped.All(r => r.Status == "healthy") ? "healthy" : "degraded";
    }
}
