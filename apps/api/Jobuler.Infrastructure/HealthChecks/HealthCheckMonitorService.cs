using System.Collections.Concurrent;
using Jobuler.Application.Common.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobuler.Infrastructure.HealthChecks;

/// <summary>
/// Background service that continuously monitors service health and sends
/// Pushover notifications when a service transitions from healthy to unhealthy.
/// Maintains in-memory state per service to detect transitions and enforce cooldowns.
/// </summary>
public class HealthCheckMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPushoverNotifier _notifier;
    private readonly HealthCheckOptions _options;
    private readonly ILogger<HealthCheckMonitorService> _logger;
    private readonly ConcurrentDictionary<string, ServiceState> _states = new();

    public HealthCheckMonitorService(
        IServiceScopeFactory scopeFactory,
        IPushoverNotifier notifier,
        IOptions<HealthCheckOptions> options,
        ILogger<HealthCheckMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _options = options.Value;
        _logger = logger;
    }

    protected virtual TimeSpan MinimumInterval => TimeSpan.FromSeconds(30);

    protected virtual TimeSpan GetInitialDelay() => TimeSpan.FromSeconds(Random.Shared.Next(5, 31));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = GetEffectiveInterval();

        // Initial delay: random 5-30 seconds before first check
        var initialDelay = GetInitialDelay();
        await Task.Delay(initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<IHealthCheckRunner>();
                var report = await runner.RunAllAsync(stoppingToken);
                await ProcessResultsAsync(report, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown — exit the loop
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during health check cycle");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal TimeSpan GetEffectiveInterval()
    {
        var configured = TimeSpan.FromSeconds(_options.IntervalSeconds);

        if (configured < MinimumInterval)
        {
            _logger.LogWarning(
                "Configured health check interval {ConfiguredSeconds}s is below minimum. Clamping to {MinSeconds}s",
                _options.IntervalSeconds,
                (int)MinimumInterval.TotalSeconds);
            return MinimumInterval;
        }

        return configured;
    }

    internal async Task ProcessResultsAsync(HealthCheckReport report, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cooldown = TimeSpan.FromSeconds(_options.AlertCooldownSeconds);

        foreach (var result in report.Checks)
        {
            // Skip services that reported "skipped" status
            if (result.Status == "skipped")
                continue;

            var previousState = _states.GetValueOrDefault(result.ServiceName);
            var previousStatus = previousState?.Status ?? "unknown";

            if (previousStatus == "healthy" && result.Status == "unhealthy")
            {
                // Healthy → Unhealthy transition: send alert (respecting cooldown)
                await HandleHealthyToUnhealthyAsync(result.ServiceName, previousState, now, cooldown, ct);
            }
            else if (previousStatus == "unhealthy" && result.Status == "healthy")
            {
                // Unhealthy → Healthy transition: log recovery, reset cooldown
                _logger.LogInformation(
                    "Service {ServiceName} has recovered and is now healthy",
                    result.ServiceName);

                _states[result.ServiceName] = new ServiceState
                {
                    Status = "healthy",
                    LastCheckedUtc = now,
                    LastAlertSentUtc = null // Reset cooldown on recovery
                };
            }
            else if (previousStatus == "unhealthy" && result.Status == "unhealthy")
            {
                // Still unhealthy: check if cooldown has elapsed
                await HandleStillUnhealthyAsync(result.ServiceName, previousState, now, cooldown, ct);
            }
            else
            {
                // Default state update (e.g., unknown→healthy, unknown→unhealthy, healthy→healthy)
                _states[result.ServiceName] = new ServiceState
                {
                    Status = result.Status,
                    LastCheckedUtc = now,
                    LastAlertSentUtc = previousState?.LastAlertSentUtc
                };
            }
        }
    }

    private async Task HandleHealthyToUnhealthyAsync(
        string serviceName,
        ServiceState? previousState,
        DateTime now,
        TimeSpan cooldown,
        CancellationToken ct)
    {
        // Recovery resets cooldown, so after a healthy→unhealthy transition
        // we always send an alert (LastAlertSentUtc is null after recovery)
        if (ShouldSendAlert(previousState, now, cooldown))
        {
            await _notifier.SendAlertAsync(serviceName, now, ct);
            _states[serviceName] = new ServiceState
            {
                Status = "unhealthy",
                LastCheckedUtc = now,
                LastAlertSentUtc = now
            };
        }
        else
        {
            _states[serviceName] = new ServiceState
            {
                Status = "unhealthy",
                LastCheckedUtc = now,
                LastAlertSentUtc = previousState?.LastAlertSentUtc
            };
        }
    }

    private async Task HandleStillUnhealthyAsync(
        string serviceName,
        ServiceState? previousState,
        DateTime now,
        TimeSpan cooldown,
        CancellationToken ct)
    {
        if (ShouldSendAlert(previousState, now, cooldown))
        {
            await _notifier.SendAlertAsync(serviceName, now, ct);
            _states[serviceName] = new ServiceState
            {
                Status = "unhealthy",
                LastCheckedUtc = now,
                LastAlertSentUtc = now
            };
        }
        else
        {
            _states[serviceName] = new ServiceState
            {
                Status = "unhealthy",
                LastCheckedUtc = now,
                LastAlertSentUtc = previousState?.LastAlertSentUtc
            };
        }
    }

    private static bool ShouldSendAlert(ServiceState? previousState, DateTime now, TimeSpan cooldown)
    {
        // No previous alert sent — always send
        if (previousState?.LastAlertSentUtc is null)
            return true;

        // Cooldown has elapsed — send
        return (now - previousState.LastAlertSentUtc.Value) >= cooldown;
    }
}

/// <summary>
/// Tracks the in-memory state of a monitored service.
/// </summary>
internal record ServiceState
{
    public string Status { get; init; } = "unknown";
    public DateTime LastCheckedUtc { get; init; }
    public DateTime? LastAlertSentUtc { get; init; }
}
