using FluentAssertions;
using Jobuler.Infrastructure.HealthChecks;
using Jobuler.Infrastructure.Notifications;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jobuler.Tests.HealthChecks;

public class PushHealthCheckTests
{
    [Fact]
    public async Task CheckAsync_WhenVapidIsNotConfigured_ReturnsSkipped()
    {
        var check = CreateCheck(new VapidSettings());

        var result = await check.CheckAsync(CancellationToken.None);

        result.ServiceName.Should().Be("push");
        result.Status.Should().Be("skipped");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_WhenVapidIsComplete_ReturnsHealthy()
    {
        var check = CreateCheck(new VapidSettings
        {
            PublicKey = "public",
            PrivateKey = "private",
            Subject = "mailto:ops@example.com"
        });

        var result = await check.CheckAsync(CancellationToken.None);

        result.Status.Should().Be("healthy");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_WhenVapidIsPartial_ReturnsUnhealthyWithMissingFields()
    {
        var check = CreateCheck(new VapidSettings
        {
            PublicKey = "public"
        });

        var result = await check.CheckAsync(CancellationToken.None);

        result.Status.Should().Be("unhealthy");
        result.ErrorMessage.Should().Contain("VAPID_PRIVATE_KEY");
        result.ErrorMessage.Should().Contain("VAPID_SUBJECT");
        result.ErrorMessage.Should().NotContain("VAPID_PUBLIC_KEY");
    }

    private static PushHealthCheck CreateCheck(VapidSettings settings)
    {
        return new PushHealthCheck(Options.Create(settings));
    }
}
