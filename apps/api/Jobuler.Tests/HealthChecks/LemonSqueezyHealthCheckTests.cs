using System.Net;
using FluentAssertions;
using Jobuler.Infrastructure.Billing;
using Jobuler.Infrastructure.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jobuler.Tests.HealthChecks;

public class LemonSqueezyHealthCheckTests
{
    [Fact]
    public async Task CheckAsync_WhenBillingIsNotConfigured_ReturnsSkippedWithoutCallingProvider()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK);
        var check = CreateCheck(new LemonSqueezySettings(), handler);

        var result = await check.CheckAsync(CancellationToken.None);

        result.ServiceName.Should().Be("lemonsqueezy");
        result.Status.Should().Be("skipped");
        handler.Request.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_WhenBillingConfigIsPartial_ReturnsUnhealthyWithoutCallingProvider()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK);
        var check = CreateCheck(new LemonSqueezySettings
        {
            ApiKey = "ls_test",
            StoreId = "store_123",
        }, handler);

        var result = await check.CheckAsync(CancellationToken.None);

        result.Status.Should().Be("unhealthy");
        result.ErrorMessage.Should().Contain("LemonSqueezy__WebhookSecret");
        result.ErrorMessage.Should().Contain("LemonSqueezy__DefaultVariantId");
        result.ErrorMessage.Should().Contain("LemonSqueezy__TestVariantId");
        handler.Request.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_WhenBillingConfigIsComplete_CallsProviderWithAuthorization()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK);
        var check = CreateCheck(CreateCompleteSettings(), handler);

        var result = await check.CheckAsync(CancellationToken.None);

        result.Status.Should().Be("healthy");
        handler.Request.Should().NotBeNull();
        handler.Request!.RequestUri!.ToString().Should().Be("https://api.lemonsqueezy.com/v1/users/me");
        handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Request.Headers.Authorization.Parameter.Should().Be("ls_test");
    }

    [Fact]
    public async Task CheckAsync_WhenProviderRejectsRequest_ReturnsUnhealthy()
    {
        var check = CreateCheck(CreateCompleteSettings(), new CaptureHandler(HttpStatusCode.Unauthorized, "Unauthorized"));

        var result = await check.CheckAsync(CancellationToken.None);

        result.Status.Should().Be("unhealthy");
        result.ErrorMessage.Should().Contain("401");
    }

    private static LemonSqueezyHealthCheck CreateCheck(
        LemonSqueezySettings settings,
        HttpMessageHandler handler)
    {
        return new LemonSqueezyHealthCheck(
            new TestHttpClientFactory(handler),
            Options.Create(settings),
            NullLogger<LemonSqueezyHealthCheck>.Instance);
    }

    private static LemonSqueezySettings CreateCompleteSettings() => new()
    {
        ApiKey = "ls_test",
        WebhookSecret = "whsec_test",
        StoreId = "store_123",
        DefaultVariantId = "variant_default",
        TestVariantId = "variant_test",
    };

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CaptureHandler(
        HttpStatusCode statusCode,
        string reasonPhrase = "OK") : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                ReasonPhrase = reasonPhrase,
                Content = new StringContent("""{"data":{}}""")
            });
        }
    }
}
