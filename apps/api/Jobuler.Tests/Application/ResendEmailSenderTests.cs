using System.Net;
using System.Text.Json;
using FluentAssertions;
using Jobuler.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jobuler.Tests.Application;

public class ResendEmailSenderTests
{
    [Fact]
    public async Task SendAsync_PostsExpectedPayloadToResend()
    {
        var handler = new CaptureRequestHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.resend.com/")
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resend:ApiKey"] = "re_test",
                ["Resend:FromEmail"] = "support@example.com",
                ["Resend:FromName"] = "Shifter Support"
            })
            .Build();
        var sender = new ResendEmailSender(http, config, NullLogger<ResendEmailSender>.Instance);

        await sender.SendAsync("user@example.com", "Need help", "<p>Hello</p>");

        handler.Request.Should().NotBeNull();
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.ToString().Should().Be("https://api.resend.com/emails");
        handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Request.Headers.Authorization.Parameter.Should().Be("re_test");

        using var doc = JsonDocument.Parse(handler.Body!);
        var root = doc.RootElement;
        root.GetProperty("from").GetString().Should().Be("Shifter Support <support@example.com>");
        root.GetProperty("to")[0].GetString().Should().Be("user@example.com");
        root.GetProperty("subject").GetString().Should().Be("Need help");
        root.GetProperty("html").GetString().Should().Be("<p>Hello</p>");
    }

    private sealed class CaptureRequestHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"email_test"}""")
            };
        }
    }
}
