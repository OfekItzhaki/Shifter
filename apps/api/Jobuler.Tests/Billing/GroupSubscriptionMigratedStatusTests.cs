// Feature: space-billing, Task 2.3
// Verifies that the GroupSubscription EF string conversion handles the Migrated status value
// **Validates: Requirements 8.1**

using FluentAssertions;
using Jobuler.Domain.Billing;
using Xunit;

namespace Jobuler.Tests.Billing;

public class GroupSubscriptionMigratedStatusTests
{
    [Fact]
    public void SubscriptionStatus_Migrated_SerializesToCorrectString()
    {
        // The EF configuration uses .HasConversion<string>() which calls ToString()
        var status = SubscriptionStatus.Migrated;
        status.ToString().Should().Be("Migrated");
    }

    [Fact]
    public void SubscriptionStatus_Migrated_DeserializesFromString()
    {
        // The EF configuration uses .HasConversion<string>() which calls Enum.Parse
        var parsed = Enum.Parse<SubscriptionStatus>("Migrated");
        parsed.Should().Be(SubscriptionStatus.Migrated);
    }

    [Fact]
    public void SubscriptionStatus_Migrated_ExistsInEnum()
    {
        // Verify Migrated is a defined value in the enum
        Enum.IsDefined(typeof(SubscriptionStatus), "Migrated").Should().BeTrue();
    }

    [Fact]
    public void SubscriptionStatus_AllValues_RoundTripThroughStringConversion()
    {
        // Verify all enum values (including Migrated) survive string round-trip
        foreach (var status in Enum.GetValues<SubscriptionStatus>())
        {
            var serialized = status.ToString();
            var deserialized = Enum.Parse<SubscriptionStatus>(serialized);
            deserialized.Should().Be(status, $"status {status} should round-trip through string conversion");
        }
    }

    [Fact]
    public void GroupSubscription_UpdateStatus_AcceptsMigrated()
    {
        // Verify the domain entity can be set to Migrated status
        var sub = GroupSubscription.CreateTrial(Guid.NewGuid(), Guid.NewGuid());
        sub.UpdateStatus(SubscriptionStatus.Migrated);
        sub.Status.Should().Be(SubscriptionStatus.Migrated);
    }
}
