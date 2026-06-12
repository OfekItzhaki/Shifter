using Microsoft.Extensions.Configuration;

namespace Jobuler.Infrastructure.Configuration;

public static class DeploymentEntitlementGuard
{
    public static void Validate(IConfiguration configuration)
    {
        var deploymentMode = FirstConfigured(
            configuration["App:DeploymentMode"],
            configuration["SHIFTER_DEPLOYMENT_MODE"]);

        if (!IsCustomerHosted(deploymentMode))
            return;

        var licensee = FirstConfigured(
            configuration["Entitlement:Licensee"],
            configuration["SHIFTER_LICENSEE"]);
        var licenseKey = FirstConfigured(
            configuration["Entitlement:LicenseKey"],
            configuration["SHIFTER_LICENSE_KEY"]);

        if (string.IsNullOrWhiteSpace(licensee))
            throw new InvalidOperationException("SHIFTER_LICENSEE is required for customer-hosted deployments.");

        if (LooksLikePlaceholder(licensee))
            throw new InvalidOperationException("SHIFTER_LICENSEE still looks like a placeholder.");

        if (string.IsNullOrWhiteSpace(licenseKey))
            throw new InvalidOperationException("SHIFTER_LICENSE_KEY is required for customer-hosted deployments.");

        if (LooksLikePlaceholder(licenseKey))
            throw new InvalidOperationException("SHIFTER_LICENSE_KEY still looks like a placeholder.");

        if (licenseKey.Trim().Length < 24)
            throw new InvalidOperationException("SHIFTER_LICENSE_KEY must be at least 24 characters.");
    }

    private static bool IsCustomerHosted(string? deploymentMode) =>
        deploymentMode?.Trim().Equals("customer-hosted", StringComparison.OrdinalIgnoreCase) == true;

    private static bool LooksLikePlaceholder(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("change_me", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("changeme", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("your-", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("customer.example", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("example.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstConfigured(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
