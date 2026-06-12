// Feature: health-check-alerts
// Integration tests for health endpoints (Task 7.3)
// Validates: Requirements 1.1, 1.2, 1.3, 1.6, 7.1, 7.2, 7.3

using FluentAssertions;
using Jobuler.Api.Controllers;
using Jobuler.Application.Common.HealthChecks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Jobuler.Tests.HealthChecks;

/// <summary>
/// Integration tests for the /health and /health/detailed endpoints.
/// Tests the HealthController directly with mocked dependencies following
/// the project's existing integration test pattern.
/// </summary>
public class HealthEndpointIntegrationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IConnectionMultiplexer CreateHealthyRedis()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        db.PingAsync(Arg.Any<CommandFlags>()).Returns(TimeSpan.FromMilliseconds(1));
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);
        return redis;
    }

    private static IConnectionMultiplexer CreateUnhealthyRedis()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        db.PingAsync(Arg.Any<CommandFlags>()).Returns<TimeSpan>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);
        return redis;
    }

    private static HealthController CreateController(
        IHealthCheckRunner? healthCheckRunner = null,
        AppDbContext? db = null,
        IConnectionMultiplexer? redis = null)
    {
        return new HealthController(
            db ?? CreateDb(),
            redis ?? CreateHealthyRedis(),
            Substitute.For<ILogger<HealthController>>(),
            healthCheckRunner ?? Substitute.For<IHealthCheckRunner>());
    }

    private static HealthCheckReport CreateAllHealthyReport()
    {
        return new HealthCheckReport(
            OverallStatus: "healthy",
            Version: "1.0.0",
            Timestamp: DateTime.UtcNow,
            Checks: new List<ServiceHealthResult>
            {
                new("postgres", "healthy", null, TimeSpan.FromMilliseconds(5)),
                new("redis", "healthy", null, TimeSpan.FromMilliseconds(2)),
                new("lemonsqueezy", "healthy", null, TimeSpan.FromMilliseconds(120)),
                new("resend", "healthy", null, TimeSpan.FromMilliseconds(80)),
                new("solver", "healthy", null, TimeSpan.FromMilliseconds(15))
            });
    }

    private static HealthCheckReport CreateDegradedReport()
    {
        return new HealthCheckReport(
            OverallStatus: "degraded",
            Version: "1.0.0",
            Timestamp: DateTime.UtcNow,
            Checks: new List<ServiceHealthResult>
            {
                new("postgres", "healthy", null, TimeSpan.FromMilliseconds(5)),
                new("redis", "unhealthy", "Connection refused", TimeSpan.FromMilliseconds(10000)),
                new("lemonsqueezy", "unhealthy", "HTTP 401 Unauthorized", TimeSpan.FromMilliseconds(450)),
                new("resend", "skipped"),
                new("solver", "healthy", null, TimeSpan.FromMilliseconds(25))
            });
    }

    // ── /health/detailed endpoint tests ──────────────────────────────────────

    // Validates: Requirement 1.1 — returns JSON with individual service statuses
    [Fact]
    public async Task Detailed_ReturnsCorrectJsonStructure_WithAllServiceEntries()
    {
        // Arrange
        var report = CreateAllHealthyReport();
        var runner = Substitute.For<IHealthCheckRunner>();
        runner.RunAllAsync(Arg.Any<CancellationToken>()).Returns(report);

        var controller = CreateController(healthCheckRunner: runner);

        // Act
        var result = await controller.Detailed(CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        var body = objectResult.Value.Should().BeOfType<HealthCheckReport>().Subject;

        body.OverallStatus.Should().Be("healthy");
        body.Version.Should().NotBeNullOrEmpty();
        body.Timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
        body.Checks.Should().HaveCount(5);
        body.Checks.Select(c => c.ServiceName).Should().Contain(new[]
        {
            "postgres", "redis", "lemonsqueezy", "resend", "solver"
        });
    }

    // Validates: Requirement 1.2 — returns 200 when all healthy
    [Fact]
    public async Task Detailed_PreservesNonSensitiveServiceDetails()
    {
        var report = new HealthCheckReport(
            OverallStatus: "healthy",
            Version: "1.0.0",
            Timestamp: DateTime.UtcNow,
            Checks: new List<ServiceHealthResult>
            {
                new(
                    "ai",
                    "healthy",
                    Details: new Dictionary<string, string>
                    {
                        ["mode"] = "private-compatible",
                        ["endpointKind"] = "private",
                        ["noExportRequired"] = "true"
                    })
            });
        var runner = Substitute.For<IHealthCheckRunner>();
        runner.RunAllAsync(Arg.Any<CancellationToken>()).Returns(report);

        var controller = CreateController(healthCheckRunner: runner);

        var result = await controller.Detailed(CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        var body = objectResult.Value.Should().BeOfType<HealthCheckReport>().Subject;
        var ai = body.Checks.Should().ContainSingle(c => c.ServiceName == "ai").Subject;
        ai.Details.Should().NotBeNull();
        ai.Details.Should().Contain("mode", "private-compatible");
        ai.Details.Should().Contain("endpointKind", "private");
        ai.Details.Should().Contain("noExportRequired", "true");
    }

    // Validates: detailed health preserves non-sensitive provider metadata.
    [Fact]
    public async Task Detailed_Returns200_WhenAllServicesHealthy()
    {
        // Arrange
        var report = CreateAllHealthyReport();
        var runner = Substitute.For<IHealthCheckRunner>();
        runner.RunAllAsync(Arg.Any<CancellationToken>()).Returns(report);

        var controller = CreateController(healthCheckRunner: runner);

        // Act
        var result = await controller.Detailed(CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
    }

    // Validates: Requirement 1.3 — returns 503 when degraded
    [Fact]
    public async Task Detailed_Returns503_WhenAnyServiceDegraded()
    {
        // Arrange
        var report = CreateDegradedReport();
        var runner = Substitute.For<IHealthCheckRunner>();
        runner.RunAllAsync(Arg.Any<CancellationToken>()).Returns(report);

        var controller = CreateController(healthCheckRunner: runner);

        // Act
        var result = await controller.Detailed(CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    // Validates: Requirement 1.6 — /health/detailed requires authentication (exposes infrastructure details)
    [Fact]
    public void Detailed_Endpoint_HasAuthorizeAttribute()
    {
        // The Detailed endpoint exposes infrastructure details and requires authentication.
        var method = typeof(HealthController).GetMethod(nameof(HealthController.Detailed));
        var authorizeAttrs = method!.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);

        authorizeAttrs.Should().NotBeEmpty(
            because: "the /health/detailed endpoint must require authentication to protect infrastructure details (Req 1.6)");
    }

    // Validates: Requirement 1.1 — each check entry contains serviceName and status
    [Fact]
    public async Task Detailed_EachCheckEntry_ContainsServiceNameAndStatus()
    {
        // Arrange
        var report = CreateDegradedReport();
        var runner = Substitute.For<IHealthCheckRunner>();
        runner.RunAllAsync(Arg.Any<CancellationToken>()).Returns(report);

        var controller = CreateController(healthCheckRunner: runner);

        // Act
        var result = await controller.Detailed(CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        var body = objectResult.Value.Should().BeOfType<HealthCheckReport>().Subject;

        foreach (var check in body.Checks)
        {
            check.ServiceName.Should().NotBeNullOrEmpty(
                because: "each check entry must have a service name");
            check.Status.Should().BeOneOf(new[] { "healthy", "unhealthy", "skipped" },
                because: "each check entry must have a valid status");
        }
    }

    // Validates: Requirement 1.1 — report includes version and timestamp
    [Fact]
    public async Task Detailed_Report_IncludesVersionAndTimestamp()
    {
        // Arrange
        var report = CreateAllHealthyReport();
        var runner = Substitute.For<IHealthCheckRunner>();
        runner.RunAllAsync(Arg.Any<CancellationToken>()).Returns(report);

        var controller = CreateController(healthCheckRunner: runner);

        // Act
        var result = await controller.Detailed(CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        var body = objectResult.Value.Should().BeOfType<HealthCheckReport>().Subject;

        body.Version.Should().NotBeNullOrEmpty(
            because: "the report must include the application version (Req 1.4)");
        body.Timestamp.Should().NotBe(default,
            because: "the report must include a UTC timestamp (Req 1.4)");
    }

    // ── /health endpoint backward compatibility tests (regression) ───────────

    // Validates: Requirement 7.1 — /health continues to check PostgreSQL and Redis
    // Note: In-memory EF Core does not support ExecuteSqlRawAsync, so PostgreSQL
    // will always appear "unhealthy" in these tests. We verify the response structure
    // and behavior (503 when any service is unhealthy) which is the correct behavior.
    [Fact]
    public async Task Health_ReturnsExpectedStructure_WithStatusVersionTimestampChecks()
    {
        // Arrange — in-memory EF Core doesn't support raw SQL, so postgres will be "unhealthy"
        var db = CreateDb();
        var redis = CreateHealthyRedis();
        var controller = CreateController(db: db, redis: redis);

        // Act
        var result = await controller.Get(CancellationToken.None);

        // Assert — verify the response structure regardless of health status
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var body = objectResult.Value;
        body.Should().NotBeNull();

        // Verify the response shape matches the existing format
        var statusProp = body!.GetType().GetProperty("status");
        var versionProp = body.GetType().GetProperty("version");
        var timestampProp = body.GetType().GetProperty("timestamp");
        var checksProp = body.GetType().GetProperty("checks");

        statusProp.Should().NotBeNull("response must have 'status' field");
        versionProp.Should().NotBeNull("response must have 'version' field");
        timestampProp.Should().NotBeNull("response must have 'timestamp' field");
        checksProp.Should().NotBeNull("response must have 'checks' field");
    }

    // Validates: Requirement 7.3 — /health returns 503 when any service is degraded
    [Fact]
    public async Task Health_Returns503_WhenRedisIsUnreachable()
    {
        // Arrange
        var db = CreateDb();
        var redis = CreateUnhealthyRedis();
        var controller = CreateController(db: db, redis: redis);

        // Act
        var result = await controller.Get(CancellationToken.None);

        // Assert — both postgres (in-memory doesn't support raw SQL) and redis are unhealthy
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);

        // Verify the response contains degraded status
        var body = objectResult.Value;
        body.Should().NotBeNull();
        var statusProp = body!.GetType().GetProperty("status");
        statusProp!.GetValue(body).Should().Be("degraded");
    }

    // Validates: Requirement 7.1 — /health response format includes checks dictionary
    [Fact]
    public async Task Health_ResponseFormat_ContainsChecksDictionary()
    {
        // Arrange
        var db = CreateDb();
        var redis = CreateHealthyRedis();
        var controller = CreateController(db: db, redis: redis);

        // Act
        var result = await controller.Get(CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var body = objectResult.Value!;

        // The /health endpoint returns checks as a Dictionary<string, string>
        var checksProp = body.GetType().GetProperty("checks");
        checksProp.Should().NotBeNull();
        var checks = checksProp!.GetValue(body) as Dictionary<string, string>;
        checks.Should().NotBeNull();
        checks.Should().ContainKey("postgres");
        checks.Should().ContainKey("redis");
        // Redis is mocked as healthy; postgres fails because in-memory EF doesn't support raw SQL
        checks!["redis"].Should().Be("healthy");
    }

    // Validates: Requirement 7.2 — /health remains accessible without authentication
    [Fact]
    public void Health_Endpoint_HasAllowAnonymousAttribute()
    {
        // The [AllowAnonymous] attribute is on the Get action method
        var method = typeof(HealthController).GetMethod(nameof(HealthController.Get));
        var allowAnonymousAttrs = method!.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true);

        allowAnonymousAttrs.Should().NotBeEmpty(
            because: "the /health endpoint must remain accessible without authentication (Req 7.2)");
    }

    [Fact]
    public async Task Ready_ReturnsExpectedStructure_WithCoreDependencyChecks()
    {
        var db = CreateDb();
        var redis = CreateHealthyRedis();
        var controller = CreateController(db: db, redis: redis);

        var result = await controller.Ready(CancellationToken.None);

        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var body = objectResult.Value!;

        var statusProp = body.GetType().GetProperty("status");
        var timestampProp = body.GetType().GetProperty("timestamp");
        var checksProp = body.GetType().GetProperty("checks");

        statusProp.Should().NotBeNull("readiness response must have 'status'");
        timestampProp.Should().NotBeNull("readiness response must have 'timestamp'");
        checksProp.Should().NotBeNull("readiness response must have 'checks'");

        var checks = checksProp!.GetValue(body) as Dictionary<string, string>;
        checks.Should().NotBeNull();
        checks.Should().ContainKeys("postgres", "redis");
    }

    [Fact]
    public async Task Ready_Returns503_WhenCoreDependencyIsUnreachable()
    {
        var db = CreateDb();
        var redis = CreateUnhealthyRedis();
        var controller = CreateController(db: db, redis: redis);

        var result = await controller.Ready(CancellationToken.None);

        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    [Fact]
    public void Ready_Endpoint_HasTopLevelAndNestedRoutes()
    {
        var method = typeof(HealthController).GetMethod(nameof(HealthController.Ready));
        var httpGetAttrs = method!.GetCustomAttributes(
            typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>();

        httpGetAttrs.Select(attr => attr.Template).Should().Contain(new[] { "ready", "~/ready" });
    }

    // Validates: Requirement 7.1 — /health response includes version and timestamp
    [Fact]
    public async Task Health_ResponseFormat_IncludesVersionAndTimestamp()
    {
        // Arrange
        var db = CreateDb();
        var redis = CreateHealthyRedis();
        var controller = CreateController(db: db, redis: redis);

        // Act
        var result = await controller.Get(CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var body = objectResult.Value!;

        var versionProp = body.GetType().GetProperty("version");
        var timestampProp = body.GetType().GetProperty("timestamp");

        versionProp.Should().NotBeNull("response must have 'version' field");
        timestampProp.Should().NotBeNull("response must have 'timestamp' field");

        var version = versionProp!.GetValue(body) as string;
        version.Should().NotBeNullOrEmpty();

        var timestamp = (DateTime)timestampProp!.GetValue(body)!;
        timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    // Validates: Requirement 7.3 — /health status code behavior unchanged (503 for degraded)
    [Fact]
    public async Task Health_StatusCodeBehavior_503ForDegraded()
    {
        // Test degraded scenario — both postgres (in-memory) and redis fail
        var db = CreateDb();
        var redis = CreateUnhealthyRedis();
        var controller = CreateController(db: db, redis: redis);

        var result = await controller.Get(CancellationToken.None);
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);

        var body = objectResult.Value!;
        var statusProp = body.GetType().GetProperty("status");
        statusProp!.GetValue(body).Should().Be("degraded");

        var checksProp = body.GetType().GetProperty("checks");
        var checks = checksProp!.GetValue(body) as Dictionary<string, string>;
        checks!["redis"].Should().Be("unhealthy");
    }
}
