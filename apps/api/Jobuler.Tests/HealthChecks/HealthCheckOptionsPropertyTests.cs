// Feature: health-check-alerts, Property 9: Interval clamping
// For any configured interval value less than 30, the effective polling interval
// is clamped to 30 seconds.
// **Validates: Requirements 6.4**

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Common.HealthChecks;
using Jobuler.Infrastructure.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Jobuler.Tests.HealthChecks;

public class HealthCheckOptionsPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HealthCheckMonitorService CreateServiceWithInterval(int intervalSeconds)
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var notifier = Substitute.For<IPushoverNotifier>();
        var logger = Substitute.For<ILogger<HealthCheckMonitorService>>();

        var options = Options.Create(new HealthCheckOptions
        {
            IntervalSeconds = intervalSeconds
        });

        return new HealthCheckMonitorService(scopeFactory, notifier, options, logger);
    }

    // ── Property 9: Interval clamping ────────────────────────────────────────
    // For any configured interval value less than 30, the effective polling
    // interval is clamped to 30 seconds.

    [Property(MaxTest = 100)]
    public Property IntervalClamping_BelowMinimum_ClampedTo30Seconds(PositiveInt rawValue)
    {
        var intervalSeconds = rawValue.Get % 30; // Generates values 0..29

        var service = CreateServiceWithInterval(intervalSeconds);
        var effective = service.GetEffectiveInterval();

        return (effective == TimeSpan.FromSeconds(30))
            .Label($"IntervalSeconds={intervalSeconds} should clamp to 30s but got {effective.TotalSeconds}s");
    }

    [Property(MaxTest = 100)]
    public Property IntervalClamping_AtOrAboveMinimum_UsesConfiguredValue(PositiveInt rawValue)
    {
        var intervalSeconds = 30 + (rawValue.Get % 10000); // Generates values 30..10029

        var service = CreateServiceWithInterval(intervalSeconds);
        var effective = service.GetEffectiveInterval();

        return (effective == TimeSpan.FromSeconds(intervalSeconds))
            .Label($"IntervalSeconds={intervalSeconds} should use configured value but got {effective.TotalSeconds}s");
    }

    [Property(MaxTest = 100)]
    public Property IntervalClamping_NegativeValues_ClampedTo30Seconds(NegativeInt negValue)
    {
        var intervalSeconds = negValue.Get; // Negative values

        var service = CreateServiceWithInterval(intervalSeconds);
        var effective = service.GetEffectiveInterval();

        return (effective == TimeSpan.FromSeconds(30))
            .Label($"IntervalSeconds={intervalSeconds} (negative) should clamp to 30s but got {effective.TotalSeconds}s");
    }

    [Property(MaxTest = 100)]
    public Property IntervalClamping_AnyValue_NeverBelowMinimum(int intervalSeconds)
    {
        var service = CreateServiceWithInterval(intervalSeconds);
        var effective = service.GetEffectiveInterval();

        var minInterval = TimeSpan.FromSeconds(30);

        return (effective >= minInterval)
            .Label($"IntervalSeconds={intervalSeconds}: effective interval {effective.TotalSeconds}s should never be below 30s");
    }
}
