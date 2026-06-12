using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

        var licenseFile = FirstConfigured(
            configuration["Entitlement:LicenseFile"],
            configuration["SHIFTER_LICENSE_FILE"]);
        if (!string.IsNullOrWhiteSpace(licenseFile))
        {
            ValidateSignedLicenseFile(configuration, licenseFile);
            return;
        }

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

    private static void ValidateSignedLicenseFile(IConfiguration configuration, string licenseFile)
    {
        var publicKey = FirstConfigured(
            configuration["Entitlement:LicensePublicKey"],
            configuration["SHIFTER_LICENSE_PUBLIC_KEY"]);

        if (string.IsNullOrWhiteSpace(publicKey))
            throw new InvalidOperationException("SHIFTER_LICENSE_PUBLIC_KEY is required when SHIFTER_LICENSE_FILE is set.");

        if (!File.Exists(licenseFile))
            throw new InvalidOperationException($"SHIFTER_LICENSE_FILE was not found: {licenseFile}");

        SignedLicense license;
        try
        {
            var json = File.ReadAllText(licenseFile);
            license = JsonSerializer.Deserialize<SignedLicense>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("License file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("SHIFTER_LICENSE_FILE is not valid JSON.", ex);
        }

        ValidateSignedLicenseFields(license);

        var configuredLicensee = FirstConfigured(
            configuration["Entitlement:Licensee"],
            configuration["SHIFTER_LICENSEE"]);
        if (!string.IsNullOrWhiteSpace(configuredLicensee) &&
            !configuredLicensee.Trim().Equals(license.Licensee.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("SHIFTER_LICENSEE does not match the signed license file.");
        }

        VerifySignature(license, publicKey);
    }

    private static void ValidateSignedLicenseFields(SignedLicense license)
    {
        if (!IsCustomerHosted(license.DeploymentMode))
            throw new InvalidOperationException("Signed license deploymentMode must be customer-hosted.");

        if (string.IsNullOrWhiteSpace(license.Licensee))
            throw new InvalidOperationException("Signed license licensee is required.");

        if (LooksLikePlaceholder(license.Licensee))
            throw new InvalidOperationException("Signed license licensee still looks like a placeholder.");

        if (string.IsNullOrWhiteSpace(license.LicenseKey))
            throw new InvalidOperationException("Signed license licenseKey is required.");

        if (LooksLikePlaceholder(license.LicenseKey))
            throw new InvalidOperationException("Signed license licenseKey still looks like a placeholder.");

        if (license.LicenseKey.Trim().Length < 24)
            throw new InvalidOperationException("Signed license licenseKey must be at least 24 characters.");

        if (license.ExpiresAt is not null && license.ExpiresAt.Value.UtcDateTime < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Signed license has expired.");

        if (string.IsNullOrWhiteSpace(license.Signature))
            throw new InvalidOperationException("Signed license signature is required.");
    }

    private static void VerifySignature(SignedLicense license, string publicKey)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(NormalizePem(publicKey));
            var payload = Encoding.UTF8.GetBytes(SignaturePayload(license));
            var signature = Convert.FromBase64String(license.Signature);
            var isValid = rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!isValid)
                throw new InvalidOperationException("Signed license signature is invalid.");
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Signed license signature must be base64.", ex);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("SHIFTER_LICENSE_PUBLIC_KEY is not a valid PEM RSA public key.", ex);
        }
    }

    private static string SignaturePayload(SignedLicense license) =>
        string.Join('\n', new[]
        {
            $"deploymentMode={license.DeploymentMode.Trim()}",
            $"licensee={license.Licensee.Trim()}",
            $"licenseKey={license.LicenseKey.Trim()}",
            $"expiresAt={license.ExpiresAt?.UtcDateTime.ToString("O") ?? string.Empty}"
        });

    private static char[] NormalizePem(string publicKey) =>
        publicKey.Replace("\\n", "\n", StringComparison.Ordinal).ToCharArray();

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

    private sealed record SignedLicense(
        string DeploymentMode,
        string Licensee,
        string LicenseKey,
        DateTimeOffset? ExpiresAt,
        string Signature);
}
