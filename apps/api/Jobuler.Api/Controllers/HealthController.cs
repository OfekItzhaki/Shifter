using Jobuler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Reflection;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<HealthController> _logger;

    public HealthController(AppDbContext db, IConnectionMultiplexer redis, ILogger<HealthController> logger)
    {
        _db = db;
        _redis = redis;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        var checks = new Dictionary<string, string>();
        var healthy = true;

        // Check PostgreSQL
        try
        {
            await _db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            checks["postgres"] = "healthy";
        }
        catch (Exception ex)
        {
            checks["postgres"] = "unhealthy";
            healthy = false;
            _logger.LogError(ex, "Health check: PostgreSQL is unreachable");
        }

        // Check Redis
        try
        {
            var db = _redis.GetDatabase();
            await db.PingAsync();
            checks["redis"] = "healthy";
        }
        catch (Exception ex)
        {
            checks["redis"] = "unhealthy";
            healthy = false;
            _logger.LogError(ex, "Health check: Redis is unreachable");
        }

        var result = new
        {
            status = healthy ? "healthy" : "degraded",
            version,
            timestamp = DateTime.UtcNow,
            checks,
        };

        return healthy ? Ok(result) : StatusCode(503, result);
    }

    /// <summary>Lightweight liveness probe — no dependency checks.</summary>
    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "alive" });
    }
}
