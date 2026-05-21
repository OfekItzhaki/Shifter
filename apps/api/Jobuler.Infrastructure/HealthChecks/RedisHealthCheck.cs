using System.Diagnostics;
using Jobuler.Application.Common.HealthChecks;
using StackExchange.Redis;

namespace Jobuler.Infrastructure.HealthChecks;

/// <summary>
/// Health check for Redis connectivity.
/// Executes a PING command via IConnectionMultiplexer and measures response time.
/// </summary>
public class RedisHealthCheck : IServiceHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public string ServiceName => "redis";

    public async Task<ServiceHealthResult> CheckAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var db = _redis.GetDatabase();
            await db.PingAsync();
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
