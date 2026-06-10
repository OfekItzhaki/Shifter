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

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public StaticResponseHandler(string responseBody) => _responseBody = responseBody;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
