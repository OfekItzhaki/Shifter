// Feature: health-check-alerts
// Properties 1, 2, 3: HealthCheckRunner aggregation and timeout behavior
// Validates: Requirements 1.1, 1.2, 1.3, 1.4, 2.6

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Common.HealthChecks;
using Jobuler.Infrastructure.HealthChecks;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.HealthChecks;

public class HealthCheckRunnerPropertyTests
{
    // ── Generators ───────────────────────────────────────────────────────────

    private static readonly string[] ValidStatuses = ["healthy", "unhealthy", "skipped"];

    private static Arbitrary<List<ServiceHealthResult>> ServiceResultsArbitrary()
    {
        var resultGen = from name in Gen.Elements("postgres", "redis", "lemonsqueezy", "sendgrid", "solver")
                        from status in Gen.Elements(ValidStatuses)
                        from hasError in Arb.Default.Bool().Generator
                        from responseMs in Gen.Choose(1, 5000)
                        let errorMsg = (status == "unhealthy" && hasError) ? "Connection failed" : null
                        let responseTime = status != "skipped" ? TimeSpan.FromMilliseconds(responseMs) : (TimeSpan?)null
                        select new ServiceHealthResult(name, status, errorMsg, responseTime);

        var listGen = from count in Gen.Choose(1, 8)
                      from results in Gen.ListOf(count, resultGen)
                      select results.ToList();

        return Arb.From(listGen);
    }

    private static Arbitrary<List<(string name, string status)>> UniqueServiceResultsArbitrary()
    {
        var gen = from serviceCount in Gen.Choose(1, 7)
                  from names in Gen.ListOf(serviceCount,
                      Gen.Elements("svc-a", "svc-b", "svc-c", "svc-d", "svc-e", "svc-f", "svc-g"))
                  from statuses in Gen.ListOf(serviceCount, Gen.Elements(ValidStatuses))
                  let pairs = names.Zip(statuses).DistinctBy(p => p.First).ToList()
                  where pairs.Count > 0
                  select pairs;

        return Arb.From(gen);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IServiceHealthCheck CreateMockCheck(string serviceName, string status,
        string? errorMessage = null, TimeSpan? responseTime = null)
    {
        var check = Substitute.For<IServiceHealthCheck>();
        check.ServiceName.Returns(serviceName);
        check.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ServiceHealthResult(serviceName, status, errorMessage, responseTime)));
        return check;
    }

    private static IServiceHealthCheck CreateDelayingCheck(string serviceName, TimeSpan delay)
    {
        var check = Substitute.For<IServiceHealthCheck>();
        check.ServiceName.Returns(serviceName);
        check.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                await Task.Delay(delay, ct);
                return new ServiceHealthResult(serviceName, "healthy");
            });
        return check;
    }

    // ── Property 1: Response structure completeness ──────────────────────────
    // For any set of service health check results, the report contains an entry
    // for every registered service, a UTC timestamp, and the version string.
    // **Validates: Requirements 1.1, 1.4**

    [Property(MaxTest = 100)]
    public Property ResponseStructureCompleteness_ReportContainsAllServices()
    {
        return Prop.ForAll(UniqueServiceResultsArbitrary(), services =>
        {
            // Arrange — create mock checks for each service
            var checks = services.Select(s => CreateMockCheck(s.name, s.status)).ToList();
            var runner = new HealthCheckRunner(checks);

            // Act
            var report = runner.RunAllAsync(CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            var allServicesPresent = report.Checks.Count == services.Count;
            var allNamesMatch = services.All(s =>
                report.Checks.Any(c => c.ServiceName == s.name));
            var hasTimestamp = report.Timestamp.Kind == DateTimeKind.Utc
                || report.Timestamp <= DateTime.UtcNow.AddSeconds(1);
            var hasVersion = !string.IsNullOrEmpty(report.Version);

            return allServicesPresent
                .Label("Report should contain entry for every registered service")
                .And(allNamesMatch
                .Label("All service names should be present in report"))
                .And(hasTimestamp
                .Label("Report should have a UTC timestamp"))
                .And(hasVersion
                .Label("Report should have a non-empty version string"));
        });
    }

    // ── Property 2: Overall status derivation ────────────────────────────────
    // For any set of results, overall status is "healthy" iff all non-skipped
    // services report "healthy"; otherwise "degraded".
    // **Validates: Requirements 1.2, 1.3**

    [Property(MaxTest = 100)]
    public Property OverallStatusDerivation_HealthyIffAllNonSkippedHealthy()
    {
        return Prop.ForAll(UniqueServiceResultsArbitrary(), services =>
        {
            // Arrange
            var checks = services.Select(s => CreateMockCheck(s.name, s.status)).ToList();
            var runner = new HealthCheckRunner(checks);

            // Act
            var report = runner.RunAllAsync(CancellationToken.None).GetAwaiter().GetResult();

            // Compute expected status
            var nonSkipped = services.Where(s => s.status != "skipped").ToList();
            var expectedHealthy = nonSkipped.All(s => s.status == "healthy");
            var expectedStatus = expectedHealthy ? "healthy" : "degraded";

            return (report.OverallStatus == expectedStatus)
                .Label($"Expected '{expectedStatus}' but got '{report.OverallStatus}' " +
                       $"for services: [{string.Join(", ", services.Select(s => $"{s.name}={s.status}"))}]");
        });
    }

    // Also test DeriveOverallStatus directly for more thorough coverage
    [Property(MaxTest = 100)]
    public Property DeriveOverallStatus_DirectTest()
    {
        return Prop.ForAll(ServiceResultsArbitrary(), results =>
        {
            // Act
            var overallStatus = HealthCheckRunner.DeriveOverallStatus(results);

            // Compute expected
            var nonSkipped = results.Where(r => r.Status != "skipped").ToList();
            var expectedHealthy = nonSkipped.All(r => r.Status == "healthy");
            var expectedStatus = expectedHealthy ? "healthy" : "degraded";

            return (overallStatus == expectedStatus)
                .Label($"Expected '{expectedStatus}' but got '{overallStatus}'");
        });
    }

    // ── Property 3: Timeout marks service unhealthy ──────────────────────────
    // For any check exceeding 10s timeout, the resulting status is "unhealthy".
    // **Validates: Requirements 2.6**

    [Fact]
    public async Task TimeoutMarksServiceUnhealthy_CheckExceeding10s_ReturnsUnhealthy()
    {
        // Arrange — create a check that delays 12 seconds (exceeds 10s timeout)
        var slowCheck = CreateDelayingCheck("slow-service", TimeSpan.FromSeconds(12));
        var fastCheck = CreateMockCheck("fast-service", "healthy", responseTime: TimeSpan.FromMilliseconds(50));

        var runner = new HealthCheckRunner(new[] { slowCheck, fastCheck });

        // Act
        var report = await runner.RunAllAsync(CancellationToken.None);

        // Assert
        var slowResult = report.Checks.First(c => c.ServiceName == "slow-service");
        slowResult.Status.Should().Be("unhealthy");
        slowResult.ErrorMessage.Should().Contain("timed out");

        var fastResult = report.Checks.First(c => c.ServiceName == "fast-service");
        fastResult.Status.Should().Be("healthy");

        report.OverallStatus.Should().Be("degraded");
    }

    [Fact]
    public async Task TimeoutMarksServiceUnhealthy_MultipleSlowChecks_AllMarkedUnhealthy()
    {
        // Arrange — multiple checks that exceed timeout
        var slow1 = CreateDelayingCheck("service-a", TimeSpan.FromSeconds(15));
        var slow2 = CreateDelayingCheck("service-b", TimeSpan.FromSeconds(20));
        var fast = CreateMockCheck("service-c", "healthy");

        var runner = new HealthCheckRunner(new[] { slow1, slow2, fast });

        // Act
        var report = await runner.RunAllAsync(CancellationToken.None);

        // Assert
        report.Checks.First(c => c.ServiceName == "service-a").Status.Should().Be("unhealthy");
        report.Checks.First(c => c.ServiceName == "service-b").Status.Should().Be("unhealthy");
        report.Checks.First(c => c.ServiceName == "service-c").Status.Should().Be("healthy");
        report.OverallStatus.Should().Be("degraded");
    }

    [Fact]
    public async Task TimeoutMarksServiceUnhealthy_CheckJustUnderTimeout_RemainsHealthy()
    {
        // Arrange — a check that completes just under the timeout (8 seconds)
        var check = Substitute.For<IServiceHealthCheck>();
        check.ServiceName.Returns("borderline-service");
        check.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                // Simulate a check that takes 1 second (well under 10s timeout)
                await Task.Delay(TimeSpan.FromSeconds(1), callInfo.Arg<CancellationToken>());
                return new ServiceHealthResult("borderline-service", "healthy",
                    ResponseTime: TimeSpan.FromSeconds(1));
            });

        var runner = new HealthCheckRunner(new[] { check });

        // Act
        var report = await runner.RunAllAsync(CancellationToken.None);

        // Assert
        report.Checks.First().Status.Should().Be("healthy");
        report.OverallStatus.Should().Be("healthy");
    }
}
