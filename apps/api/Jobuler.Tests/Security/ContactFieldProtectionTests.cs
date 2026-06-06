using FluentAssertions;
using Jobuler.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Jobuler.Tests.Security;

public class ContactFieldProtectionTests
{
    [Fact]
    public void FieldEncryption_RoundTripsWithoutStoringPlaintext()
    {
        FieldEncryption.Configure("test-field-encryption-key");

        var encrypted = FieldEncryption.Encrypt("owner@example.com");

        encrypted.Should().NotBe("owner@example.com");
        encrypted.Should().StartWith("enc:v1:");
        FieldEncryption.Decrypt(encrypted).Should().Be("owner@example.com");
    }

    [Fact]
    public void ContactLookupProtector_ProducesStableNormalizedHashes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:FieldHashKey"] = "test-contact-hash-key"
            })
            .Build();
        var protector = new ContactLookupProtector(configuration);

        protector.HashEmail(" Owner@Example.COM ")
            .Should().Be(protector.HashEmail("owner@example.com"));
        protector.HashPhone("+972 50-123-4567")
            .Should().Be(protector.HashPhone("+972501234567"));
    }
}
