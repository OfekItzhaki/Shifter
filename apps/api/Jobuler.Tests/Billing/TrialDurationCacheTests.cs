// Feature: space-billing
// Unit tests: TrialDurationCache — fallback, sync, and caching behavior
// **Validates: Requirements 1.2, 1.5, 1.7**

using FluentAssertions;
using Jobuler.Application.Billing;
using Jobuler.Infrastructure.Billing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Jobuler.Tests.Billing;

public class TrialDurationCacheTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<LemonSqueezySettings> _settings;
    private readonly ILogger<TrialDurationCache> _logger;

    public TrialDurationCacheTests()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _settings = Options.Create(new LemonSqueezySettings
        {
            ApiKey = "test_key",
            WebhookSecret = "test_secret",
            StoreId = "test_store",
            DefaultVariantId = "variant_123",
            TestVariantId = "test_variant"
        });
        _logger = Substitute.For<ILogger<TrialDurationCache>>();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fallback to 14 days when cache is unavailable
    // **Validates: Requirements 1.5**
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTrialDaysAsync_WhenNeverSynced_ReturnsFourteenDays()
    {
        var cache = new TrialDurationCache(_httpClientFactory, _settings, _logger);

        var result = await cache.GetTrialDaysAsync();

        result.Should().Be(14);
    }

    [Fact]
    public async Task GetTrialDaysAsync_WhenCachePopulated_ReturnsCachedValue()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildVariantResponse(30));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.lemonsqueezy.com/") };
        _httpClientFactory.CreateClient("TrialDurationCache").Returns(client);

        var cache = new TrialDurationCache(_httpClientFactory, _settings, _logger);
        await cache.SyncFromLemonSqueezyAsync();

        var result = await cache.GetTrialDaysAsync();

        result.Should().Be(30);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Sync from LemonSqueezy variant attributes
    // **Validates: Requirements 1.2, 1.7**
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SyncFromLemonSqueezyAsync_ExtractsTrialDurationDays()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildVariantResponse(21));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.lemonsqueezy.com/") };
        _httpClientFactory.CreateClient("TrialDurationCache").Returns(client);

        var cache = new TrialDurationCache(_httpClientFactory, _settings, _logger);
        await cache.SyncFromLemonSqueezyAsync();

        var result = await cache.GetTrialDaysAsync();
        result.Should().Be(21);
    }

    [Fact]
    public async Task SyncFromLemonSqueezyAsync_OnHttpFailure_KeepsExistingValue()
    {
        // First sync succeeds with 21 days
        var successHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildVariantResponse(21));
        var successClient = new HttpClient(successHandler) { BaseAddress = new Uri("https://api.lemonsqueezy.com/") };
        _httpClientFactory.CreateClient("TrialDurationCache").Returns(successClient);

        var cache = new TrialDurationCache(_httpClientFactory, _settings, _logger);
        await cache.SyncFromLemonSqueezyAsync();

        // Second sync fails
        var failHandler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "");
        var failClient = new HttpClient(failHandler) { BaseAddress = new Uri("https://api.lemonsqueezy.com/") };
        _httpClientFactory.CreateClient("TrialDurationCache").Returns(failClient);

        await cache.SyncFromLemonSqueezyAsync();

        var result = await cache.GetTrialDaysAsync();
        result.Should().Be(21, "existing cached value should be preserved on failure");
    }

    [Fact]
    public async Task SyncFromLemonSqueezyAsync_OnException_KeepsExistingValue()
    {
        // First sync succeeds
        var successHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildVariantResponse(7));
        var successClient = new HttpClient(successHandler) { BaseAddress = new Uri("https://api.lemonsqueezy.com/") };
        _httpClientFactory.CreateClient("TrialDurationCache").Returns(successClient);

        var cache = new TrialDurationCache(_httpClientFactory, _settings, _logger);
        await cache.SyncFromLemonSqueezyAsync();

        // Second sync throws
        _httpClientFactory.CreateClient("TrialDurationCache")
            .Returns(_ => throw new HttpRequestException("Network error"));

        await cache.SyncFromLemonSqueezyAsync();

        var result = await cache.GetTrialDaysAsync();
        result.Should().Be(7, "existing cached value should be preserved on exception");
    }

    [Fact]
    public async Task SyncFromLemonSqueezyAsync_WhenNeverSyncedAndFails_FallsBackToDefault()
    {
        var failHandler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "");
        var failClient = new HttpClient(failHandler) { BaseAddress = new Uri("https://api.lemonsqueezy.com/") };
        _httpClientFactory.CreateClient("TrialDurationCache").Returns(failClient);

        var cache = new TrialDurationCache(_httpClientFactory, _settings, _logger);
        await cache.SyncFromLemonSqueezyAsync();

        var result = await cache.GetTrialDaysAsync();
        result.Should().Be(14, "should fall back to 14 days when never synced and sync fails");
    }

    [Fact]
    public async Task SyncFromLemonSqueezyAsync_WhenVariantIdNotConfigured_SkipsSync()
    {
        var emptySettings = Options.Create(new LemonSqueezySettings
        {
            ApiKey = "test_key",
            WebhookSecret = "test_secret",
            StoreId = "test_store",
            DefaultVariantId = "",
            TestVariantId = "test_variant"
        });

        var cache = new TrialDurationCache(_httpClientFactory, emptySettings, _logger);
        await cache.SyncFromLemonSqueezyAsync();

        var result = await cache.GetTrialDaysAsync();
        result.Should().Be(14, "should fall back to default when variant ID is not configured");
    }

    [Fact]
    public async Task SyncFromLemonSqueezyAsync_WhenResponseMissingTrialDuration_KeepsExistingValue()
    {
        // Response without trial_duration_days attribute
        var json = JsonSerializer.Serialize(new
        {
            data = new
            {
                type = "variants",
                id = "variant_123",
                attributes = new
                {
                    name = "Pro Plan",
                    price = 999
                }
            }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.lemonsqueezy.com/") };
        _httpClientFactory.CreateClient("TrialDurationCache").Returns(client);

        var cache = new TrialDurationCache(_httpClientFactory, _settings, _logger);
        await cache.SyncFromLemonSqueezyAsync();

        var result = await cache.GetTrialDaysAsync();
        result.Should().Be(14, "should fall back to default when attribute is missing");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private static string BuildVariantResponse(int trialDurationDays)
    {
        return JsonSerializer.Serialize(new
        {
            data = new
            {
                type = "variants",
                id = "variant_123",
                attributes = new
                {
                    name = "Pro Plan",
                    price = 999,
                    trial_duration_days = trialDurationDays
                }
            }
        });
    }

    /// <summary>
    /// Fake HTTP message handler for testing HTTP client behavior without real network calls.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody)
            };
            return Task.FromResult(response);
        }
    }
}
