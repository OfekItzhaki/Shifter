using System.Net;
using FluentAssertions;
using Jobuler.Infrastructure.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jobuler.Tests.HealthChecks;

public class AiHealthCheckTests
{
    [Fact]
    public async Task CheckAsync_WhenAiDisabled_ReturnsSkipped()
    {
        var check = CreateCheck(new Dictionary<string, string?>());

        var result = await check.CheckAsync(CancellationToken.None);

        result.ServiceName.Should().Be("ai");
        result.Status.Should().Be("skipped");
    }

    [Fact]
    public async Task CheckAsync_WithPrivateBaseUrlAndNoApiKey_UsesModelsEndpointWithoutAuthorization()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK);
        var check = CreateCheck(new Dictionary<string, string?>
        {
            ["AI:BaseUrl"] = "http://local-ai.internal/v1",
            ["AI:Model"] = "local-model"
        }, handler);

        var result = await check.CheckAsync(CancellationToken.None);

        result.Status.Should().Be("healthy");
        handler.Request.Should().NotBeNull();
        handler.Request!.RequestUri!.ToString().Should().Be("http://local-ai.internal/v1/models");
        handler.Request.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_WithApiKeyAndNoBaseUrl_UsesOpenAiDefaultEndpointWithAuthorization()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK);
        var check = CreateCheck(new Dictionary<string, string?>
        {
            ["AI:ApiKey"] = "sk-test",
            ["AI:Model"] = "gpt-test"
        }, handler);

        var result = await check.CheckAsync(CancellationToken.None);

        result.Status.Should().Be("healthy");
        handler.Request.Should().NotBeNull();
        handler.Request!.RequestUri!.ToString().Should().Be("https://api.openai.com/v1/models");
        handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Request.Headers.Authorization.Parameter.Should().Be("sk-test");
    }

    [Fact]
    public async Task CheckAsync_WhenEndpointFails_ReturnsUnhealthy()
    {
        var check = CreateCheck(new Dictionary<string, string?>
        {
            ["AI:BaseUrl"] = "http://local-ai.internal/v1",
            ["AI:Model"] = "local-model"
        }, new CaptureHandler(HttpStatusCode.ServiceUnavailable));

        var result = await check.CheckAsync(CancellationToken.None);

        result.Status.Should().Be("unhealthy");
        result.ErrorMessage.Should().Contain("503");
    }

    private static AiHealthCheck CreateCheck(
        Dictionary<string, string?> values,
        HttpMessageHandler? handler = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var factory = new TestHttpClientFactory(handler ?? new CaptureHandler(HttpStatusCode.OK));
        return new AiHealthCheck(factory, configuration, NullLogger<AiHealthCheck>.Instance);
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CaptureHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("""{"data":[]}""")
            });
        }
    }
}
