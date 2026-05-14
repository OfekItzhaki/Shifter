// Feature: home-leave-slider
// Property 1: Balance value validation — accepts [0,100], rejects outside
// Property 4: Balance value persistence round-trip
// Property 9: Backward compatibility — omitting balance_value preserves stored value
// Validates: Requirements 1.3, 2.3, 2.4, 2.5, 9.3, 9.5, 10.3

using FluentAssertions;
using Jobuler.Application.HomeLeave.Commands;
using Jobuler.Application.HomeLeave.Validators;
using Jobuler.Domain.Groups;
using Xunit;

namespace Jobuler.Tests.HomeLeave;

public class BalanceValuePropertyTests
{
    private readonly UpsertHomeLeaveConfigValidator _validator = new();

    // ── Property 1: Balance value validation ──────────────────────────────────
    // For any integer value, the system accepts it as valid balance_value
    // if and only if it is in [0, 100] inclusive.

    [Fact]
    public void Property1_BalanceValue_AcceptsOnlyValuesIn0To100()
    {
        // Generate 200 random integers spanning valid and invalid ranges
        var rng = new Random(42);
        var validCount = 0;
        var invalidCount = 0;

        for (int i = 0; i < 200; i++)
        {
            var value = rng.Next(-50, 200); // range: -50 to 199
            var cmd = new UpsertHomeLeaveConfigCommand(
                SpaceId: Guid.NewGuid(),
                GroupId: Guid.NewGuid(),
                MinRestHours: 8,
                EligibilityThresholdHours: 24,
                LeaveCapacity: 1,
                LeaveDurationHours: 48,
                RequestingUserId: Guid.NewGuid(),
                BalanceValue: value);

            var result = _validator.Validate(cmd);
            var expectedValid = value >= 0 && value <= 100;

            result.IsValid.Should().Be(expectedValid,
                because: $"balance_value={value} should be {(expectedValid ? "valid" : "invalid")}");

            if (expectedValid) validCount++;
            else invalidCount++;
        }

        validCount.Should().BeGreaterThan(0);
        invalidCount.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(99, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    [InlineData(-100, false)]
    [InlineData(200, false)]
    public void Property1_BalanceValue_BoundaryValues(int value, bool expectedValid)
    {
        var cmd = new UpsertHomeLeaveConfigCommand(
            SpaceId: Guid.NewGuid(),
            GroupId: Guid.NewGuid(),
            MinRestHours: 8,
            EligibilityThresholdHours: 24,
            LeaveCapacity: 1,
            LeaveDurationHours: 48,
            RequestingUserId: Guid.NewGuid(),
            BalanceValue: value);

        var result = _validator.Validate(cmd);
        result.IsValid.Should().Be(expectedValid,
            because: $"balance_value={value} boundary check");
    }

    [Fact]
    public void Property1_BalanceValue_NullIsAlwaysValid()
    {
        // When BalanceValue is null, the validator should not reject it
        // (backward compatibility — omitting means "keep existing")
        var cmd = new UpsertHomeLeaveConfigCommand(
            SpaceId: Guid.NewGuid(),
            GroupId: Guid.NewGuid(),
            MinRestHours: 8,
            EligibilityThresholdHours: 24,
            LeaveCapacity: 1,
            LeaveDurationHours: 48,
            RequestingUserId: Guid.NewGuid(),
            BalanceValue: null);

        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue("null balance_value should be accepted (backward compat)");
    }

    // ── Property 4: Balance value persistence round-trip ──────────────────────
    // Create a HomeLeaveConfig with a random valid balance_value,
    // verify the value is stored correctly.

    [Fact]
    public void Property4_BalanceValue_PersistenceRoundTrip()
    {
        var rng = new Random(123);

        for (int i = 0; i < 100; i++)
        {
            var balanceValue = rng.Next(0, 101); // 0–100 inclusive
            var config = HomeLeaveConfig.Create(
                spaceId: Guid.NewGuid(),
                groupId: Guid.NewGuid(),
                minRestHours: 8,
                eligibilityThresholdHours: 24,
                leaveCapacity: 2,
                leaveDurationHours: 48,
                balanceValue: balanceValue);

            config.BalanceValue.Should().Be(balanceValue,
                because: $"created with balance_value={balanceValue}, should read back the same");
        }
    }

    [Fact]
    public void Property4_BalanceValue_UpdateRoundTrip()
    {
        var rng = new Random(456);

        for (int i = 0; i < 100; i++)
        {
            var initial = rng.Next(0, 101);
            var updated = rng.Next(0, 101);

            var config = HomeLeaveConfig.Create(
                spaceId: Guid.NewGuid(),
                groupId: Guid.NewGuid(),
                minRestHours: 8,
                eligibilityThresholdHours: 24,
                leaveCapacity: 2,
                leaveDurationHours: 48,
                balanceValue: initial);

            config.Update(8, 24, 2, 48, balanceValue: updated);

            config.BalanceValue.Should().Be(updated,
                because: $"updated to balance_value={updated}, should read back the same");
        }
    }

    // ── Property 9: Backward compatibility — omit retains stored value ────────
    // For any existing config with stored balance_value B,
    // updating without providing balance_value should leave it at B.

    [Fact]
    public void Property9_OmittingBalanceValue_RetainsStoredValue()
    {
        var rng = new Random(789);

        for (int i = 0; i < 100; i++)
        {
            var storedValue = rng.Next(0, 101);

            var config = HomeLeaveConfig.Create(
                spaceId: Guid.NewGuid(),
                groupId: Guid.NewGuid(),
                minRestHours: 8,
                eligibilityThresholdHours: 24,
                leaveCapacity: 2,
                leaveDurationHours: 48,
                balanceValue: storedValue);

            // Update WITHOUT providing balanceValue (null)
            config.Update(10, 30, 3, 72, balanceValue: null);

            config.BalanceValue.Should().Be(storedValue,
                because: $"omitting balance_value should retain stored value {storedValue}");

            // Other fields should have changed
            config.MinRestHours.Should().Be(10);
            config.LeaveCapacity.Should().Be(3);
        }
    }

    // ── Domain validation rejects out-of-range values ─────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(-50)]
    [InlineData(200)]
    public void Property1_DomainEntity_RejectsInvalidBalanceValue_OnCreate(int value)
    {
        var act = () => HomeLeaveConfig.Create(
            spaceId: Guid.NewGuid(),
            groupId: Guid.NewGuid(),
            minRestHours: 8,
            eligibilityThresholdHours: 24,
            leaveCapacity: 2,
            leaveDurationHours: 48,
            balanceValue: value);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*0*100*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Property1_DomainEntity_RejectsInvalidBalanceValue_OnUpdate(int value)
    {
        var config = HomeLeaveConfig.Create(
            spaceId: Guid.NewGuid(),
            groupId: Guid.NewGuid(),
            minRestHours: 8,
            eligibilityThresholdHours: 24,
            leaveCapacity: 2,
            leaveDurationHours: 48,
            balanceValue: 50);

        var act = () => config.Update(8, 24, 2, 48, balanceValue: value);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*0*100*");
    }
}
