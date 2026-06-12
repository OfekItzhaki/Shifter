using System.Net;
using FluentAssertions;
using Jobuler.Infrastructure.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jobuler.Tests.HealthChecks;

public class ResendHealthCheckTests
{
    [Fact]
    public async Task CheckAsync_WhenApiKeyMissing_ReturnsSkippedWithoutCallingResend()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK);
        var check = CreateCheck(new Dictionary<string, string?>(), handler);

        var result = await check.CheckAsync(CancellationToken.None);

        result.ServiceName.Should().Be("resend");
        result.Status.Should().Be("skipped");
        handler.Request.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_WithApiKey_CallsDomainsEndpointWithAuthorization()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK);
        var check = CreateCheck(new Dictionary<string, string?>
        {
            ["Resend:ApiKey"] = "re_test"
        }, handler);

        var result = await check.CheckAsync(CancellationToken.None);

        result.Status.Should().Be("healthy");
        handler.Request.Should().NotBeNull();
        handler.Request!.RequestUri!.ToString().Should().Be("https://api.resend.com/domains");
        handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Request.Headers.Authorization.Parameter.Should().Be("re_test");
    }

    [Fact]
    public async Task CheckAsync_WhenResendReturnsError_ReturnsUnhealthy()
    {
        var check = CreateCheck(new Dictionary<string, string?>
        {
            ["Resend:ApiKey"] = "re_test"
        }, new CaptureHandler(HttpStatusCode.Unauthorized, "Unauthorized"));

        var result = await check.CheckAsync(CancellationToken.None);

        result.Status.Should().Be("unhealthy");
        result.ErrorMessage.Should().Contain("401");
    }

    [Fact]
    public async Task CheckAsync_WhenRequestThrows_ReturnsUnhealthy()
    {
        var check = CreateCheck(new Dictionary<string, string?>
        {
            ["Resend:ApiKey"] = "re_test"
        }, new ThrowingHandler());

        var result = await check.CheckAsync(CancellationToken.None);

        result.Status.Should().Be("unhealthy");
        result.ErrorMessage.Should().Contain("network down");
    }

    private static ResendHealthCheck CreateCheck(
        Dictionary<string, string?> values,
        HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new ResendHealthCheck(
            new TestHttpClientFactory(handler),
            configuration,
            NullLogger<ResendHealthCheck>.Instance);
    }

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
                Content = new StringContent("""{"data":[]}""")
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("network down");
    }
}
