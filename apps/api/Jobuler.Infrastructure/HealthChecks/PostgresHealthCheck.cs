using System.Diagnostics;
using Jobuler.Application.Common.HealthChecks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Infrastructure.HealthChecks;

/// <summary>
/// Health check for PostgreSQL connectivity.
/// Executes a lightweight SELECT 1 query to verify the database is reachable.
/// </summary>
public class PostgresHealthCheck : IServiceHealthCheck
{
    private readonly AppDbContext _db;

    public PostgresHealthCheck(AppDbContext db)
    {
        _db = db;
    }

    public string ServiceName => "postgres";

    public async Task<ServiceHealthResult> CheckAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
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
