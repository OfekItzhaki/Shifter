using System.Net;
using System.Text;
using FluentAssertions;
using Jobuler.Application.AI;
using Jobuler.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jobuler.Tests.Application;

public class AiAssistantSupportTests
{
    [Fact]
    public async Task NoOpChat_WhenAiKeyMissing_OffersHumanSupportEscalation()
    {
        var assistant = new NoOpAiAssistant();

        var result = await assistant.ChatAsync(new AiChatRequestDto(
            "I need support from Ofek",
            "en",
            "Ofek",
            "/settings",
            IsAuthenticated: true,
            IsAdminMode: false,
            RecentMessages: []));

        result.Message.Should().Contain("not connected to an AI model");
        result.SuggestedActions.Should().ContainSingle(a =>
            a.Type == "contact" &&
            a.Label == "Contact support" &&
            a.Payload != null &&
            a.Payload.Contains("I need support from Ofek"));
        result.SuggestedActions.Should().Contain(a => a.Type == "feedback");
    }

    [Theory]
    [InlineData("he", "דבר עם תמיכה", "שלח פידבק", "העוזר החכם")]
    [InlineData("ru", "Связаться с поддержкой", "Отправить отзыв", "AI assistant is not configured")]
    public async Task NoOpChat_LocalizedSupportActions_AreReadable(
        string locale,
        string supportLabel,
        string feedbackLabel,
        string expectedMessageFragment)
    {
        var assistant = new NoOpAiAssistant();

        var result = await assistant.ChatAsync(new AiChatRequestDto(
            "help",
            locale,
            "Ofek",
            "/profile",
            IsAuthenticated: true,
            IsAdminMode: false,
            RecentMessages: []));

        result.Message.Should().Contain(expectedMessageFragment);
        result.SuggestedActions.Should().ContainSingle(a =>
            a.Type == "contact" &&
            a.Label == supportLabel &&
            a.Payload != null);
        result.SuggestedActions.Should().ContainSingle(a =>
            a.Type == "feedback" &&
            a.Label == feedbackLabel);
    }

    [Fact]
    public async Task OpenAiChat_WhenSupportRequestedAndModelOmitsContact_AddsHumanSupportAction()
    {
        var responseJson = """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"message\":\"I can help with that.\",\"suggestedActions\":[]}"
                  }
                }
              ]
            }
            """;
        var http = new HttpClient(new StaticResponseHandler(responseJson));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:ApiKey"] = "test-key",
                ["AI:Model"] = "gpt-test"
            })
            .Build();
        var assistant = new OpenAiAssistant(http, config, NullLogger<OpenAiAssistant>.Instance);

        var result = await assistant.ChatAsync(new AiChatRequestDto(
            "Can I contact a human?",
            "en",
            "Ofek",
            "/profile",
            IsAuthenticated: true,
            IsAdminMode: false,
            RecentMessages: []));

        result.Message.Should().Be("I can help with that.");
        result.SuggestedActions.Should().ContainSingle(a =>
            a.Type == "contact" &&
            a.Label == "Contact support" &&
            a.Payload != null &&
            a.Payload.Contains("Can I contact a human?"));
    }

    [Fact]
    public async Task OpenAiChat_WithBaseUrlAndNoApiKey_UsesPrivateCompatibleEndpoint()
    {
        var responseJson = """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"message\":\"Private endpoint is working.\",\"suggestedActions\":[]}"
                  }
                }
              ]
            }
            """;
        var handler = new StaticResponseHandler(responseJson);
        var http = new HttpClient(handler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:BaseUrl"] = "http://local-ai.internal/v1",
                ["AI:Model"] = "local-model"
            })
            .Build();
        var assistant = new OpenAiAssistant(http, config, NullLogger<OpenAiAssistant>.Instance);

        var result = await assistant.ChatAsync(new AiChatRequestDto(
            "hello",
            "en",
            "Ofek",
            "/home",
            IsAuthenticated: true,
            IsAdminMode: false,
            RecentMessages: []));

        result.Message.Should().Be("Private endpoint is working.");
        handler.Request!.RequestUri!.ToString().Should().Be("http://local-ai.internal/v1/chat/completions");
        handler.Request.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task OpenAiChat_WhenProviderFails_ReturnsLocalizedHebrewSupportActions()
    {
        var http = new HttpClient(new ThrowingHandler());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:BaseUrl"] = "http://local-ai.internal/v1",
                ["AI:Model"] = "local-model"
            })
            .Build();
        var assistant = new OpenAiAssistant(http, config, NullLogger<OpenAiAssistant>.Instance);

        var result = await assistant.ChatAsync(new AiChatRequestDto(
            "אני צריך תמיכה",
            "he",
            "Ofek",
            "/settings",
            IsAuthenticated: true,
            IsAdminMode: true,
            RecentMessages: []));

        result.Message.Should().Contain("לא הצלחתי לענות");
        result.SuggestedActions.Should().ContainSingle(a =>
            a.Type == "contact" &&
            a.Label == "דבר עם תמיכה" &&
            a.Payload != null &&
            a.Payload.Contains("אני צריך תמיכה"));
        result.SuggestedActions.Should().ContainSingle(a =>
            a.Type == "feedback" &&
            a.Label == "שלח פידבק");
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public StaticResponseHandler(string responseBody) => _responseBody = responseBody;

        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("AI endpoint unavailable");
        }
    }
}
