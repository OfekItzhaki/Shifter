using FluentAssertions;
using Jobuler.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Jobuler.Tests.Application;

public class AiConfigurationGuardTests
{
    [Theory]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://127.0.0.1:8000/v1")]
    [InlineData("http://10.0.12.4:8000/v1")]
    [InlineData("http://172.16.0.5:8000/v1")]
    [InlineData("http://172.31.255.255:8000/v1")]
    [InlineData("http://192.168.1.20:8000/v1")]
    [InlineData("http://local-ai.customer.internal:8000/v1")]
    [InlineData("https://ai-gateway.customer.local/v1")]
    public void ValidateNoExportPolicy_AllowsPrivateOrLocalBaseUrls(string baseUrl)
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["AI:NoExportRequired"] = "true",
            ["AI:BaseUrl"] = baseUrl,
            ["AI:Model"] = "local-model"
        });

        var act = () => AiConfigurationGuard.ValidateNoExportPolicy(config);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("https://openrouter.ai/api/v1")]
    [InlineData("http://8.8.8.8:8000/v1")]
    [InlineData("http://172.15.0.1:8000/v1")]
    [InlineData("http://172.32.0.1:8000/v1")]
    public void ValidateNoExportPolicy_RejectsPublicBaseUrls(string baseUrl)
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["AI:NoExportRequired"] = "true",
            ["AI:BaseUrl"] = baseUrl,
            ["AI:Model"] = "hosted-model"
        });

        var act = () => AiConfigurationGuard.ValidateNoExportPolicy(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires AI:BaseUrl to use localhost*");
    }

    [Fact]
    public void ValidateNoExportPolicy_RejectsMissingBaseUrlWhenRequired()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["AI:NoExportRequired"] = "true"
        });

        var act = () => AiConfigurationGuard.ValidateNoExportPolicy(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires AI:BaseUrl to point to a private/local*");
    }

    [Fact]
    public void ValidateNoExportPolicy_RejectsInvalidFlagValue()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["AI:NoExportRequired"] = "maybe",
            ["AI:BaseUrl"] = "http://local-ai.internal/v1"
        });

        var act = () => AiConfigurationGuard.ValidateNoExportPolicy(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("AI_NO_EXPORT_REQUIRED must be true, false, or empty.");
    }

    [Fact]
    public void ValidateNoExportPolicy_AcceptsComposeStyleEnvironmentKey()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["AI:NoExportRequired"] = "",
            ["AI_NO_EXPORT_REQUIRED"] = "true",
            ["AI:BaseUrl"] = "http://local-ai.internal/v1"
        });

        var act = () => AiConfigurationGuard.ValidateNoExportPolicy(config);

        act.Should().NotThrow();
    }

    private static IConfiguration CreateConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
