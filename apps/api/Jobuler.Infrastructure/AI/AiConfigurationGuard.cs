using Microsoft.Extensions.Configuration;

namespace Jobuler.Infrastructure.AI;

public static class AiConfigurationGuard
{
    public static void Validate(IConfiguration configuration)
    {
        ValidateEndpointConfiguration(configuration);
        ValidateNoExportPolicy(configuration);
    }

    public static void ValidateEndpointConfiguration(IConfiguration configuration)
    {
        var baseUrl = configuration["AI:BaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            return;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException($"AI:BaseUrl must be an absolute OpenAI-compatible endpoint URL: {baseUrl}");

        var model = configuration["AI:Model"]?.Trim();
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("AI:Model is required when AI:BaseUrl is configured.");
    }

    public static void ValidateNoExportPolicy(IConfiguration configuration)
    {
        var noExportRequired = FirstConfigured(
            configuration["AI:NoExportRequired"],
            configuration["AI_NO_EXPORT_REQUIRED"]);

        if (string.IsNullOrWhiteSpace(noExportRequired) ||
            noExportRequired.Equals("false", StringComparison.OrdinalIgnoreCase))
            return;

        if (!noExportRequired.Equals("true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AI_NO_EXPORT_REQUIRED must be true, false, or empty.");

        var baseUrl = configuration["AI:BaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("AI_NO_EXPORT_REQUIRED=true requires AI:BaseUrl to point to a private/local OpenAI-compatible endpoint.");

        if (!IsPrivateOrLocalEndpoint(baseUrl))
        {
            throw new InvalidOperationException(
                $"AI_NO_EXPORT_REQUIRED=true requires AI:BaseUrl to use localhost, a private IP, .internal, or .local endpoint: {baseUrl}");
        }
    }

    public static bool IsPrivateOrLocalEndpoint(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!System.Net.IPAddress.TryParse(host, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        return bytes.Length == 4 &&
            (bytes[0] == 10 ||
             bytes[0] == 127 ||
             (bytes[0] == 192 && bytes[1] == 168) ||
             (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31));
    }

    private static string? FirstConfigured(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
