using FluentAssertions;
using Jobuler.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Jobuler.Tests.Application;

public class DeploymentEntitlementGuardTests
{
    [Fact]
    public void Validate_WhenSaasModeHasNoLicense_AllowsConfiguration()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["App:DeploymentMode"] = "saas"
        });

        var act = () => DeploymentEntitlementGuard.Validate(config);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenCustomerHostedHasLicense_AllowsConfiguration()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["App:DeploymentMode"] = "customer-hosted",
            ["Entitlement:Licensee"] = "Acme Scheduling Ltd",
            ["Entitlement:LicenseKey"] = "valid-customer-license-key-2026"
        });

        var act = () => DeploymentEntitlementGuard.Validate(config);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenCustomerHostedUsesComposeStyleKeys_AllowsConfiguration()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["SHIFTER_DEPLOYMENT_MODE"] = "customer-hosted",
            ["SHIFTER_LICENSEE"] = "Acme Scheduling Ltd",
            ["SHIFTER_LICENSE_KEY"] = "valid-customer-license-key-2026"
        });

        var act = () => DeploymentEntitlementGuard.Validate(config);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenCustomerHostedHasSignedLicenseFile_AllowsConfiguration()
    {
        using var rsa = RSA.Create(2048);
        var licenseFile = WriteSignedLicenseFile(
            rsa,
            "Acme Scheduling Ltd",
            "valid-customer-license-key-2026",
            DateTimeOffset.UtcNow.AddYears(1));
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["App:DeploymentMode"] = "customer-hosted",
            ["Entitlement:LicenseFile"] = licenseFile,
            ["Entitlement:LicensePublicKey"] = rsa.ExportSubjectPublicKeyInfoPem()
        });

        var act = () => DeploymentEntitlementGuard.Validate(config);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenSignedLicenseFileSignatureIsInvalid_RejectsConfiguration()
    {
        using var signer = RSA.Create(2048);
        using var verifier = RSA.Create(2048);
        var licenseFile = WriteSignedLicenseFile(
            signer,
            "Acme Scheduling Ltd",
            "valid-customer-license-key-2026",
            DateTimeOffset.UtcNow.AddYears(1));
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["App:DeploymentMode"] = "customer-hosted",
            ["Entitlement:LicenseFile"] = licenseFile,
            ["Entitlement:LicensePublicKey"] = verifier.ExportSubjectPublicKeyInfoPem()
        });

        var act = () => DeploymentEntitlementGuard.Validate(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Signed license signature is invalid.");
    }

    [Fact]
    public void Validate_WhenSignedLicenseFileIsExpired_RejectsConfiguration()
    {
        using var rsa = RSA.Create(2048);
        var licenseFile = WriteSignedLicenseFile(
            rsa,
            "Acme Scheduling Ltd",
            "valid-customer-license-key-2026",
            DateTimeOffset.UtcNow.AddDays(-1));
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["App:DeploymentMode"] = "customer-hosted",
            ["Entitlement:LicenseFile"] = licenseFile,
            ["Entitlement:LicensePublicKey"] = rsa.ExportSubjectPublicKeyInfoPem()
        });

        var act = () => DeploymentEntitlementGuard.Validate(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Signed license has expired.");
    }

    [Fact]
    public void Validate_WhenSignedLicenseFileDoesNotMatchConfiguredLicensee_RejectsConfiguration()
    {
        using var rsa = RSA.Create(2048);
        var licenseFile = WriteSignedLicenseFile(
            rsa,
            "Acme Scheduling Ltd",
            "valid-customer-license-key-2026",
            DateTimeOffset.UtcNow.AddYears(1));
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["App:DeploymentMode"] = "customer-hosted",
            ["Entitlement:Licensee"] = "Other Customer Ltd",
            ["Entitlement:LicenseFile"] = licenseFile,
            ["Entitlement:LicensePublicKey"] = rsa.ExportSubjectPublicKeyInfoPem()
        });

        var act = () => DeploymentEntitlementGuard.Validate(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("SHIFTER_LICENSEE does not match the signed license file.");
    }

    [Fact]
    public void Validate_WhenCustomerHostedMissingLicenseKey_RejectsConfiguration()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["App:DeploymentMode"] = "customer-hosted",
            ["Entitlement:Licensee"] = "Acme Scheduling Ltd"
        });

        var act = () => DeploymentEntitlementGuard.Validate(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("SHIFTER_LICENSE_KEY is required for customer-hosted deployments.");
    }

    [Fact]
    public void Validate_WhenCustomerHostedLicenseKeyIsShort_RejectsConfiguration()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["App:DeploymentMode"] = "customer-hosted",
            ["Entitlement:Licensee"] = "Acme Scheduling Ltd",
            ["Entitlement:LicenseKey"] = "too-short"
        });

        var act = () => DeploymentEntitlementGuard.Validate(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("SHIFTER_LICENSE_KEY must be at least 24 characters.");
    }

    [Theory]
    [InlineData("change_me_customer_legal_name", "valid-customer-license-key-2026", "SHIFTER_LICENSEE still looks like a placeholder.")]
    [InlineData("Acme Scheduling Ltd", "change_me_customer_license_key_min_24_chars", "SHIFTER_LICENSE_KEY still looks like a placeholder.")]
    public void Validate_WhenCustomerHostedLicenseValuesLookLikePlaceholders_RejectsConfiguration(
        string licensee,
        string licenseKey,
        string expectedMessage)
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["App:DeploymentMode"] = "customer-hosted",
            ["Entitlement:Licensee"] = licensee,
            ["Entitlement:LicenseKey"] = licenseKey
        });

        var act = () => DeploymentEntitlementGuard.Validate(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(expectedMessage);
    }

    private static IConfiguration CreateConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static string WriteSignedLicenseFile(
        RSA rsa,
        string licensee,
        string licenseKey,
        DateTimeOffset expiresAt)
    {
        var deploymentMode = "customer-hosted";
        var payload = string.Join('\n', new[]
        {
            $"deploymentMode={deploymentMode}",
            $"licensee={licensee}",
            $"licenseKey={licenseKey}",
            $"expiresAt={expiresAt.UtcDateTime:O}"
        });
        var signature = Convert.ToBase64String(
            rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        var license = new
        {
            deploymentMode,
            licensee,
            licenseKey,
            expiresAt,
            signature
        };
        var path = Path.Combine(Path.GetTempPath(), $"shifter-license-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(license));
        return path;
    }
}
