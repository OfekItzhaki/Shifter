using FluentAssertions;
using Jobuler.Application.Common.HealthChecks;
using Jobuler.Infrastructure.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using Xunit;

namespace Jobuler.Tests.HealthChecks;

public class PushoverNotifierTests
{
    private readonly IOptions<HealthCheckOptions> _validOptions;
    private readonly ILogger<PushoverNotifier> _logger;

    public PushoverNotifierTests()
    {
        _validOptions = Options.Create(new HealthCheckOptions
        {
            PushoverUserKey = "test-user-key",
            PushoverAppToken = "test-app-token"
        });
        _logger = Substitute.For<ILogger<PushoverNotifier>>();
    }

    private static IHttpClientFactory CreateHttpClientFactory(MockHttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Pushover").Returns(client);
        return factory;
    }

    // ── Request body format ───────────────────────────────────────────────────

    [Fact]
    public async Task SendAlertAsync_SendsCorrectFormBody_WithTokenUserMessageAndPriority()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var factory = CreateHttpClientFactory(handler);
        var notifier = new PushoverNotifier(factory, _validOptions, _logger);
        var timestamp = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        await notifier.SendAlertAsync("redis", timestamp, CancellationToken.None);

        // Assert
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest!.RequestUri!.ToString()
            .Should().Be("https://api.pushover.net/1/messages.json");
        handler.CapturedRequest.Method.Should().Be(HttpMethod.Post);

        var body = handler.CapturedBody!;
        body.Should().Contain("token=test-app-token");
        body.Should().Contain("user=test-user-key");
        body.Should().Contain("priority=1");
        body.Should().Contain("title=Shifter+Health+Alert");
    }

    // ── Message content ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendAlertAsync_MessageContainsServiceNameAndUtcTimestamp()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var factory = CreateHttpClientFactory(handler);
        var notifier = new PushoverNotifier(factory, _validOptions, _logger);
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);

        // Act
        await notifier.SendAlertAsync("postgres", timestamp, CancellationToken.None);

        // Assert
        var body = handler.CapturedBody!;
        // URL-encoded form: spaces become +, apostrophes become %27
        body.Should().Contain("postgres");
        body.Should().Contain("2024-01-15+10%3A30%3A45+UTC");
    }

    // ── Graceful degradation when credentials missing ─────────────────────────

    [Fact]
    public async Task SendAlertAsync_WhenUserKeyIsNull_LogsWarningAndDoesNotSend()
    {
        // Arrange
        var options = Options.Create(new HealthCheckOptions
        {
            PushoverUserKey = null,
            PushoverAppToken = "some-token"
        });
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var factory = CreateHttpClientFactory(handler);
        var notifier = new PushoverNotifier(factory, options, _logger);

        // Act
        await notifier.SendAlertAsync("redis", DateTime.UtcNow, CancellationToken.None);

        // Assert
        handler.CapturedRequest.Should().BeNull("no HTTP request should be made");
        _logger.ReceivedWithAnyArgs().LogWarning(default(string)!, default(object[])!);
    }

    [Fact]
    public async Task SendAlertAsync_WhenAppTokenIsEmpty_LogsWarningAndDoesNotSend()
    {
        // Arrange
        var options = Options.Create(new HealthCheckOptions
        {
            PushoverUserKey = "some-user",
            PushoverAppToken = ""
        });
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var factory = CreateHttpClientFactory(handler);
        var notifier = new PushoverNotifier(factory, options, _logger);

        // Act
        await notifier.SendAlertAsync("solver", DateTime.UtcNow, CancellationToken.None);

        // Assert
        handler.CapturedRequest.Should().BeNull("no HTTP request should be made");
        _logger.ReceivedWithAnyArgs().LogWarning(default(string)!, default(object[])!);
    }

    [Fact]
    public async Task SendAlertAsync_WhenBothCredentialsMissing_DoesNotThrow()
    {
        // Arrange
        var options = Options.Create(new HealthCheckOptions
        {
            PushoverUserKey = null,
            PushoverAppToken = null
        });
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var factory = CreateHttpClientFactory(handler);
        var notifier = new PushoverNotifier(factory, options, _logger);

        // Act
        var act = () => notifier.SendAlertAsync("redis", DateTime.UtcNow, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── Error logging on non-success status ──────────────────────────────────

    [Fact]
    public async Task SendAlertAsync_WhenPushoverReturnsNonSuccess_LogsErrorAndDoesNotThrow()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest);
        var factory = CreateHttpClientFactory(handler);
        var notifier = new PushoverNotifier(factory, _validOptions, _logger);

        // Act
        var act = () => notifier.SendAlertAsync("redis", DateTime.UtcNow, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _logger.ReceivedWithAnyArgs().LogError(default(string)!, default(object[])!);
    }

    [Fact]
    public async Task SendAlertAsync_WhenHttpClientThrows_LogsErrorAndDoesNotThrow()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(new HttpRequestException("Connection refused"));
        var factory = CreateHttpClientFactory(handler);
        var notifier = new PushoverNotifier(factory, _validOptions, _logger);

        // Act
        var act = () => notifier.SendAlertAsync("redis", DateTime.UtcNow, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _logger.ReceivedWithAnyArgs().LogError(default(Exception)!, default(string)!, default(object[])!);
    }

    // ── Helper: MockHttpMessageHandler ───────────────────────────────────────

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode? _statusCode;
        private readonly Exception? _exception;

        public HttpRequestMessage? CapturedRequest { get; private set; }
        public string? CapturedBody { get; private set; }

        public MockHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public MockHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            if (request.Content != null)
            {
                CapturedBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (_exception != null)
                throw _exception;

            return new HttpResponseMessage(_statusCode!.Value);
        }
    }
}
