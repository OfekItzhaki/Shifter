using Jobuler.Application.Common.HealthChecks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Reflection;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<HealthController> _logger;
    private readonly IHealthCheckRunner _healthCheckRunner;

    public HealthController(
        AppDbContext db,
        IConnectionMultiplexer redis,
        ILogger<HealthController> logger,
        IHealthCheckRunner healthCheckRunner)
    {
        _db = db;
        _redis = redis;
        _logger = logger;
        _healthCheckRunner = healthCheckRunner;
    }

    [HttpGet]
    [AllowAnonymous]
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
    [AllowAnonymous]
    public IActionResult Live()
    {
        return Ok(new { status = "alive" });
    }

    /// <summary>
    /// Detailed health check endpoint reporting per-service status for all monitored services.
    /// Returns 200 when all services are healthy, 503 when any service is degraded.
    /// Requires authentication to prevent exposing infrastructure details publicly.
    /// </summary>
    [HttpGet("detailed")]
    [Authorize]
    public async Task<IActionResult> Detailed(CancellationToken ct)
    {
        var report = await _healthCheckRunner.RunAllAsync(ct);
        var statusCode = report.OverallStatus == "healthy" ? 200 : 503;
        return StatusCode(statusCode, report);
    }

    /// <summary>Debug: Check subscription state for a space directly from DB (temporary diagnostic).</summary>
    [HttpGet("debug/subscription/{spaceId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> DebugSubscription(Guid spaceId, CancellationToken ct)
    {
        var sub = await _db.SpaceSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SpaceId == spaceId, ct);

        if (sub is null)
            return Ok(new { exists = false, spaceId });

        return Ok(new
        {
            exists = true,
            spaceId,
            status = sub.Status.ToString(),
            tierId = sub.TierId,
            lsSubscriptionId = sub.LemonSqueezySubscriptionId,
            lsCustomerId = sub.LemonSqueezyCustomerId,
            trialStartsAt = sub.TrialStartsAt,
            trialEndsAt = sub.TrialEndsAt,
            currentPeriodStart = sub.CurrentPeriodStart,
            currentPeriodEnd = sub.CurrentPeriodEnd,
            isAccessGranted = sub.IsAccessGranted,
            isTrialExpired = sub.IsTrialExpired,
        });
    }

    /// <summary>Debug: Check recent webhook events (temporary diagnostic).</summary>
    [HttpGet("debug/webhooks")]
    [AllowAnonymous]
    public async Task<IActionResult> DebugWebhooks(CancellationToken ct)
    {
        var events = await _db.WebhookEventLogs
            .AsNoTracking()
            .OrderByDescending(e => e.ProcessedAt)
            .Take(20)
            .Select(e => new { e.EventId, e.EventType, e.ProcessedAt, e.ProcessedSuccessfully })
            .ToListAsync(ct);

        return Ok(events);
    }
}
