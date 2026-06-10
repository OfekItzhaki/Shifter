// Feature: health-check-alerts
// Properties 4, 5, 6, 7, 8: HealthCheckMonitorService state machine behavior
// Validates: Requirements 3.4, 3.5, 3.7, 4.1, 4.3, 4.4, 5.3

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Common.HealthChecks;
using Jobuler.Infrastructure.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.HealthChecks;

public class HealthCheckMonitorPropertyTests
{
    // ── Generators ───────────────────────────────────────────────────────────

    private static readonly string[] ServiceNames =
        ["postgres", "redis", "lemonsqueezy", "resend", "solver", "svc-alpha", "svc-beta"];

    private static Arbitrary<string> ServiceNameArbitrary()
    {
        return Arb.From(Gen.Elements(ServiceNames));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a HealthCheckMonitorService with mocked dependencies for direct
    /// ProcessResultsAsync testing (bypasses BackgroundService lifecycle).
    /// </summary>
    private static (HealthCheckMonitorService service, IPushoverNotifier notifier, ILogger<HealthCheckMonitorService> logger)
        CreateService(int cooldownSeconds = 3600)
    {
        var runner = Substitute.For<IHealthCheckRunner>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IHealthCheckRunner)).Returns(runner);
        scope.ServiceProvider.Returns(serviceProvider);
        scopeFactory.CreateScope().Returns(scope);

        var notifier = Substitute.For<IPushoverNotifier>();
        notifier.SendAlertAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = Options.Create(new HealthCheckOptions
        {
            IntervalSeconds = 30,
            AlertCooldownSeconds = cooldownSeconds,
            PushoverUserKey = "test-user-key",
            PushoverAppToken = "test-app-token"
        });

        var logger = Substitute.For<ILogger<HealthCheckMonitorService>>();

        var service = new HealthCheckMonitorService(scopeFactory, notifier, options, logger);

        return (service, notifier, logger);
    }

    private static HealthCheckReport MakeReport(params (string name, string status)[] services)
    {
        var checks = services.Select(s => new ServiceHealthResult(s.name, s.status)).ToList();
        var overall = checks.All(c => c.Status == "healthy" || c.Status == "skipped") ? "healthy" : "degraded";
        return new HealthCheckReport(overall, "1.0.0", DateTime.UtcNow, checks);
    }

    // ── Property 4: State transition triggers alert with correct content ─────
    // For any service transitioning healthy→unhealthy, a Pushover notification
    // is sent containing the service name and UTC timestamp.
    // **Validates: Requirements 3.4, 5.3**

    [Property(MaxTest = 100)]
    public Property StateTransitionTriggersAlert_HealthyToUnhealthy_SendsNotification()
    {
        return Prop.ForAll(ServiceNameArbitrary(), serviceName =>
        {
            // Arrange
            var (service, notifier, logger) = CreateService();

            // Act: first cycle establishes healthy baseline
            var healthyReport = MakeReport((serviceName, "healthy"));
            service.ProcessResultsAsync(healthyReport, CancellationToken.None).GetAwaiter().GetResult();

            // Second cycle: transition to unhealthy
            var unhealthyReport = MakeReport((serviceName, "unhealthy"));
            service.ProcessResultsAsync(unhealthyReport, CancellationToken.None).GetAwaiter().GetResult();

            // Assert: notifier was called with the service name and a UTC timestamp
            notifier.Received(1).SendAlertAsync(
                serviceName,
                Arg.Is<DateTime>(dt => dt.Kind == DateTimeKind.Utc || (DateTime.UtcNow - dt).TotalMinutes < 5),
                Arg.Any<CancellationToken>());

            return true.ToProperty();
        });
    }

    // ── Property 5: Recovery logs at Information level ────────────────────────
    // For any service transitioning unhealthy→healthy, an Information-level log
    // entry is produced containing the service name.
    // **Validates: Requirements 3.5**

    [Property(MaxTest = 100)]
    public Property RecoveryLogsAtInformationLevel_UnhealthyToHealthy_LogsRecovery()
    {
        return Prop.ForAll(ServiceNameArbitrary(), serviceName =>
        {
            // Arrange
            var (service, notifier, logger) = CreateService();

            // Act: first cycle establishes unhealthy state (from unknown → unhealthy)
            var unhealthyReport = MakeReport((serviceName, "unhealthy"));
            service.ProcessResultsAsync(unhealthyReport, CancellationToken.None).GetAwaiter().GetResult();

            // Clear any previous log calls from the first transition
            logger.ClearReceivedCalls();

            // Second cycle: transition to healthy (unhealthy → healthy = recovery)
            var healthyReport = MakeReport((serviceName, "healthy"));
            service.ProcessResultsAsync(healthyReport, CancellationToken.None).GetAwaiter().GetResult();

            // Assert: logger received an Information-level log containing the service name
            logger.Received().Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains(serviceName)),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());

            return true.ToProperty();
        });
    }

    // ── Property 7: Cooldown suppresses duplicate alerts ─────────────────────
    // For any service remaining continuously unhealthy, at most one alert is
    // sent per cooldown period.
    // **Validates: Requirements 4.1, 4.3**

    [Property(MaxTest = 100)]
    public Property CooldownSuppressesDuplicateAlerts_ContinuouslyUnhealthy_OneAlertPerCooldown()
    {
        return Prop.ForAll(ServiceNameArbitrary(), serviceName =>
        {
            // Arrange: use a large cooldown so subsequent unhealthy checks are suppressed
            var (service, notifier, logger) = CreateService(cooldownSeconds: 3600);

            // Act: establish healthy baseline
            service.ProcessResultsAsync(MakeReport((serviceName, "healthy")), CancellationToken.None)
                .GetAwaiter().GetResult();

            // Transition to unhealthy (triggers alert)
            service.ProcessResultsAsync(MakeReport((serviceName, "unhealthy")), CancellationToken.None)
                .GetAwaiter().GetResult();

            // Remain unhealthy for several more cycles (should be suppressed)
            service.ProcessResultsAsync(MakeReport((serviceName, "unhealthy")), CancellationToken.None)
                .GetAwaiter().GetResult();
            service.ProcessResultsAsync(MakeReport((serviceName, "unhealthy")), CancellationToken.None)
                .GetAwaiter().GetResult();
            service.ProcessResultsAsync(MakeReport((serviceName, "unhealthy")), CancellationToken.None)
                .GetAwaiter().GetResult();

            // Assert: exactly 1 alert sent (the healthy→unhealthy transition)
            var callCount = notifier.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == nameof(IPushoverNotifier.SendAlertAsync));

            return (callCount == 1)
                .Label($"Expected exactly 1 alert but got {callCount} for service '{serviceName}'");
        });
    }

    // ── Property 8: Recovery resets cooldown ─────────────────────────────────
    // For any service transitioning unhealthy→healthy→unhealthy, the second
    // unhealthy transition triggers a new alert immediately.
    // **Validates: Requirements 4.4**

    [Property(MaxTest = 100)]
    public Property RecoveryResetsCooldown_UnhealthyHealthyUnhealthy_SecondAlertSent()
    {
        return Prop.ForAll(ServiceNameArbitrary(), serviceName =>
        {
            // Arrange: large cooldown — but recovery should reset it
            var (service, notifier, logger) = CreateService(cooldownSeconds: 3600);

            // Act: establish healthy baseline
            service.ProcessResultsAsync(MakeReport((serviceName, "healthy")), CancellationToken.None)
                .GetAwaiter().GetResult();

            // First unhealthy transition (triggers first alert)
            service.ProcessResultsAsync(MakeReport((serviceName, "unhealthy")), CancellationToken.None)
                .GetAwaiter().GetResult();

            // Recovery (resets cooldown)
            service.ProcessResultsAsync(MakeReport((serviceName, "healthy")), CancellationToken.None)
                .GetAwaiter().GetResult();

            // Second unhealthy transition (should trigger second alert immediately)
            service.ProcessResultsAsync(MakeReport((serviceName, "unhealthy")), CancellationToken.None)
                .GetAwaiter().GetResult();

            // Assert: exactly 2 alerts sent
            var callCount = notifier.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == nameof(IPushoverNotifier.SendAlertAsync));

            return (callCount == 2)
                .Label($"Expected exactly 2 alerts but got {callCount} for service '{serviceName}'");
        });
    }

    // ── Property 6: Exception resilience ─────────────────────────────────────
    // For any exception thrown during a health check cycle, the monitor catches
    // it, logs at Error level, and continues executing subsequent cycles.
    // **Validates: Requirements 3.7**

    private static readonly Exception[] GeneratedExceptions =
    [
        new InvalidOperationException("Random failure"),
        new TimeoutException("Connection timed out"),
        new HttpRequestException("Network error"),
        new ArgumentException("Bad argument"),
        new NullReferenceException("Object reference not set"),
        new IOException("I/O error occurred"),
        new ApplicationException("Application error"),
        new NotSupportedException("Operation not supported"),
        new FormatException("Input string was not in a correct format"),
        new InvalidCastException("Specified cast is not valid")
    ];

    private static Arbitrary<Exception> ExceptionArbitrary()
    {
        return Arb.From(Gen.Elements(GeneratedExceptions));
    }

    [Property(MaxTest = 10)]
    public Property ExceptionResilience_AnyException_LoggedAtErrorAndServiceContinues()
    {
        return Prop.ForAll(ExceptionArbitrary(), exception =>
        {
            // Arrange: runner throws on first call, succeeds on subsequent calls.
            // Use a TaskCompletionSource to signal when the second call completes,
            // avoiding reliance on arbitrary sleep durations.
            var callCount = 0;
            var secondCallCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var runner = Substitute.For<IHealthCheckRunner>();
            runner.RunAllAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var count = Interlocked.Increment(ref callCount);
                    if (count == 1)
                        throw exception;

                    // Signal that the service continued past the exception
                    secondCallCompleted.TrySetResult(true);

                    return Task.FromResult(new HealthCheckReport(
                        "healthy", "1.0.0", DateTime.UtcNow,
                        new List<ServiceHealthResult>
                        {
                            new("postgres", "healthy", ResponseTime: TimeSpan.FromMilliseconds(5))
                        }));
                });

            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var scope = Substitute.For<IServiceScope>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IHealthCheckRunner)).Returns(runner);
            scope.ServiceProvider.Returns(serviceProvider);
            scopeFactory.CreateScope().Returns(scope);

            var notifier = Substitute.For<IPushoverNotifier>();
            var fakeLogger = new FakeLogger<HealthCheckMonitorService>();

            var options = Options.Create(new HealthCheckOptions
            {
                IntervalSeconds = 1
            });

            var service = new FastHealthCheckMonitorService(scopeFactory, notifier, options, fakeLogger);

            // Act: start the service and wait for the second call to complete
            using var cts = new CancellationTokenSource();
            service.StartAsync(cts.Token).GetAwaiter().GetResult();

            var completed = secondCallCompleted.Task.Wait(TimeSpan.FromSeconds(2));

            cts.Cancel();
            try { service.StopAsync(CancellationToken.None).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }

            // Assert 1: exception was logged at Error level
            var errorLogs = fakeLogger.LogEntries
                .Where(e => e.LogLevel == LogLevel.Error)
                .ToList();

            var hasErrorLog = errorLogs.Any(e =>
                e.Exception?.GetType() == exception.GetType());

            // Assert 2: service continued running (second call was made)
            var continuedRunning = completed && Volatile.Read(ref callCount) >= 2;

            return hasErrorLog
                .Label($"Exception of type {exception.GetType().Name} should be logged at Error level (got {errorLogs.Count} error logs)")
                .And(continuedRunning
                .Label($"Service should continue after exception (got {Volatile.Read(ref callCount)} calls, expected >= 2, completed={completed})"));
        });
    }
}

internal sealed class FastHealthCheckMonitorService : HealthCheckMonitorService
{
    public FastHealthCheckMonitorService(
        IServiceScopeFactory scopeFactory,
        IPushoverNotifier notifier,
        IOptions<HealthCheckOptions> options,
        ILogger<HealthCheckMonitorService> logger)
        : base(scopeFactory, notifier, options, logger)
    {
    }

    protected override TimeSpan MinimumInterval => TimeSpan.FromMilliseconds(10);

    protected override TimeSpan GetInitialDelay() => TimeSpan.Zero;
}

/// <summary>
/// A simple fake logger that captures log entries for assertion in property tests.
/// </summary>
internal sealed class FakeLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = new();
    private readonly object _lock = new();

    public IReadOnlyList<LogEntry> LogEntries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_lock)
        {
            _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }
}

internal record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);
