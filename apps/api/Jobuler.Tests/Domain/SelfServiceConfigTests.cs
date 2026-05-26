// Feature: self-service-scheduling
// Unit tests for SelfServiceConfig domain entity

using FluentAssertions;
using Jobuler.Domain.Groups;
using Xunit;

namespace Jobuler.Tests.Domain;

public class SelfServiceConfigTests
{
    // ── Create with defaults ─────────────────────────────────────────────────

    [Fact]
    public void Create_WithDefaults_SetsCorrectValues()
    {
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var config = SelfServiceConfig.Create(spaceId, groupId);

        config.SpaceId.Should().Be(spaceId);
        config.GroupId.Should().Be(groupId);
        config.MinShiftsPerCycle.Should().Be(0);
        config.MaxShiftsPerCycle.Should().Be(7);
        config.RequestWindowOpenOffsetHours.Should().Be(168);
        config.RequestWindowCloseOffsetHours.Should().Be(24);
        config.CancellationCutoffHours.Should().Be(24);
        config.WaitlistOfferMinutes.Should().Be(60);
        config.CycleDurationDays.Should().Be(7);
        config.Id.Should().NotBeEmpty();
        config.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithCustomValues_SetsAllFields()
    {
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var config = SelfServiceConfig.Create(
            spaceId, groupId,
            minShiftsPerCycle: 2,
            maxShiftsPerCycle: 10,
            requestWindowOpenOffsetHours: 336,
            requestWindowCloseOffsetHours: 48,
            cancellationCutoffHours: 12,
            waitlistOfferMinutes: 30,
            cycleDurationDays: 14);

        config.MinShiftsPerCycle.Should().Be(2);
        config.MaxShiftsPerCycle.Should().Be(10);
        config.RequestWindowOpenOffsetHours.Should().Be(336);
        config.RequestWindowCloseOffsetHours.Should().Be(48);
        config.CancellationCutoffHours.Should().Be(12);
        config.WaitlistOfferMinutes.Should().Be(30);
        config.CycleDurationDays.Should().Be(14);
    }

    // ── Min/Max shifts validation ────────────────────────────────────────────

    [Fact]
    public void SetMinMaxShifts_WithMinGreaterThanMax_Throws()
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        var act = () => config.SetMinMaxShifts(5, 3);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*less than or equal to max*");
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(101, 5)]
    public void SetMinMaxShifts_WithMinOutOfRange_Throws(int min, int max)
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        var act = () => config.SetMinMaxShifts(min, max);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Min shifts*");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public void SetMinMaxShifts_WithMaxOutOfRange_Throws(int min, int max)
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        var act = () => config.SetMinMaxShifts(min, max);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Max shifts*");
    }

    [Fact]
    public void SetMinMaxShifts_WithValidValues_Updates()
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        config.SetMinMaxShifts(3, 10);

        config.MinShiftsPerCycle.Should().Be(3);
        config.MaxShiftsPerCycle.Should().Be(10);
    }

    // ── Request window offset validation ─────────────────────────────────────

    [Fact]
    public void SetRequestWindowOffsets_WithOpenNotGreaterThanClose_Throws()
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        var act = () => config.SetRequestWindowOffsets(24, 48);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*open offset must be greater than close offset*");
    }

    [Fact]
    public void SetRequestWindowOffsets_WithEqualValues_Throws()
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        var act = () => config.SetRequestWindowOffsets(48, 48);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*open offset must be greater than close offset*");
    }

    [Theory]
    [InlineData(0, 24)]
    [InlineData(721, 24)]
    public void SetRequestWindowOffsets_WithOpenOutOfRange_Throws(int open, int close)
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        var act = () => config.SetRequestWindowOffsets(open, close);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*open offset*");
    }

    [Theory]
    [InlineData(168, 0)]
    [InlineData(168, 721)]
    public void SetRequestWindowOffsets_WithCloseOutOfRange_Throws(int open, int close)
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        var act = () => config.SetRequestWindowOffsets(open, close);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*close offset*");
    }

    [Fact]
    public void SetRequestWindowOffsets_WithValidValues_Updates()
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        config.SetRequestWindowOffsets(336, 48);

        config.RequestWindowOpenOffsetHours.Should().Be(336);
        config.RequestWindowCloseOffsetHours.Should().Be(48);
    }

    // ── Cancellation cutoff validation ───────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(721)]
    public void SetCancellationCutoffHours_OutOfRange_Throws(int hours)
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        var act = () => config.SetCancellationCutoffHours(hours);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cancellation cutoff*");
    }

    [Fact]
    public void SetCancellationCutoffHours_WithValidValue_Updates()
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        config.SetCancellationCutoffHours(48);

        config.CancellationCutoffHours.Should().Be(48);
    }

    // ── Waitlist offer minutes validation ────────────────────────────────────

    [Theory]
    [InlineData(14)]
    [InlineData(1441)]
    public void SetWaitlistOfferMinutes_OutOfRange_Throws(int minutes)
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        var act = () => config.SetWaitlistOfferMinutes(minutes);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Waitlist offer*");
    }

    [Fact]
    public void SetWaitlistOfferMinutes_WithValidValue_Updates()
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        config.SetWaitlistOfferMinutes(120);

        config.WaitlistOfferMinutes.Should().Be(120);
    }

    // ── Cycle duration validation ────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void SetCycleDurationDays_OutOfRange_Throws(int days)
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        var act = () => config.SetCycleDurationDays(days);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cycle duration*");
    }

    [Fact]
    public void SetCycleDurationDays_WithValidValue_Updates()
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        config.SetCycleDurationDays(14);

        config.CycleDurationDays.Should().Be(14);
    }

    // ── Update method ────────────────────────────────────────────────────────

    [Fact]
    public void Update_WithValidValues_UpdatesAllFields()
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        config.Update(
            minShiftsPerCycle: 1,
            maxShiftsPerCycle: 5,
            requestWindowOpenOffsetHours: 240,
            requestWindowCloseOffsetHours: 12,
            cancellationCutoffHours: 48,
            waitlistOfferMinutes: 90,
            cycleDurationDays: 14);

        config.MinShiftsPerCycle.Should().Be(1);
        config.MaxShiftsPerCycle.Should().Be(5);
        config.RequestWindowOpenOffsetHours.Should().Be(240);
        config.RequestWindowCloseOffsetHours.Should().Be(12);
        config.CancellationCutoffHours.Should().Be(48);
        config.WaitlistOfferMinutes.Should().Be(90);
        config.CycleDurationDays.Should().Be(14);
    }

    // ── ITenantScoped ────────────────────────────────────────────────────────

    [Fact]
    public void Implements_ITenantScoped()
    {
        var config = SelfServiceConfig.Create(Guid.NewGuid(), Guid.NewGuid());

        config.Should().BeAssignableTo<Jobuler.Domain.Common.ITenantScoped>();
    }
}
