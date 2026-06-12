using FluentAssertions;
using Jobuler.Application.Scheduling.SelfService;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class SelfServiceDefaultPolicyOptionsTests
{
    [Fact]
    public void Validate_Defaults_ReturnsNoErrors()
    {
        var options = new SelfServiceDefaultPolicyOptions();

        options.Validate().Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithValidBoundaryValues_ReturnsNoErrors()
    {
        var options = new SelfServiceDefaultPolicyOptions
        {
            MinShiftsPerCycle = 0,
            MaxShiftsPerCycle = 100,
            RequestWindowOpenOffsetHours = 720,
            RequestWindowCloseOffsetHours = 1,
            CancellationCutoffHours = 720,
            MaxAbsencesPerCycle = 0,
            MaxLateCancellationsPerCycle = 100,
            LateCancellationWindowHours = 1,
            WaitlistOfferMinutes = 1440,
            CycleDurationDays = 30
        };

        options.Validate().Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenMinShiftsExceedsMaxShifts_ReturnsError()
    {
        var options = new SelfServiceDefaultPolicyOptions
        {
            MinShiftsPerCycle = 8,
            MaxShiftsPerCycle = 3
        };

        options.Validate().Should().Contain(error =>
            error.Contains(nameof(SelfServiceDefaultPolicyOptions.MinShiftsPerCycle), StringComparison.Ordinal)
            && error.Contains(nameof(SelfServiceDefaultPolicyOptions.MaxShiftsPerCycle), StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenRequestWindowOpenIsNotAfterClose_ReturnsError()
    {
        var options = new SelfServiceDefaultPolicyOptions
        {
            RequestWindowOpenOffsetHours = 12,
            RequestWindowCloseOffsetHours = 24
        };

        options.Validate().Should().Contain(error =>
            error.Contains(nameof(SelfServiceDefaultPolicyOptions.RequestWindowOpenOffsetHours), StringComparison.Ordinal)
            && error.Contains(nameof(SelfServiceDefaultPolicyOptions.RequestWindowCloseOffsetHours), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-1, 7, 168, 24, 24, 3, 2, 24, 60, 7, nameof(SelfServiceDefaultPolicyOptions.MinShiftsPerCycle))]
    [InlineData(0, 0, 168, 24, 24, 3, 2, 24, 60, 7, nameof(SelfServiceDefaultPolicyOptions.MaxShiftsPerCycle))]
    [InlineData(0, 7, 0, 24, 24, 3, 2, 24, 60, 7, nameof(SelfServiceDefaultPolicyOptions.RequestWindowOpenOffsetHours))]
    [InlineData(0, 7, 168, 0, 24, 3, 2, 24, 60, 7, nameof(SelfServiceDefaultPolicyOptions.RequestWindowCloseOffsetHours))]
    [InlineData(0, 7, 168, 24, 0, 3, 2, 24, 60, 7, nameof(SelfServiceDefaultPolicyOptions.CancellationCutoffHours))]
    [InlineData(0, 7, 168, 24, 24, -1, 2, 24, 60, 7, nameof(SelfServiceDefaultPolicyOptions.MaxAbsencesPerCycle))]
    [InlineData(0, 7, 168, 24, 24, 3, -1, 24, 60, 7, nameof(SelfServiceDefaultPolicyOptions.MaxLateCancellationsPerCycle))]
    [InlineData(0, 7, 168, 24, 24, 3, 2, 0, 60, 7, nameof(SelfServiceDefaultPolicyOptions.LateCancellationWindowHours))]
    [InlineData(0, 7, 168, 24, 24, 3, 2, 24, 14, 7, nameof(SelfServiceDefaultPolicyOptions.WaitlistOfferMinutes))]
    [InlineData(0, 7, 168, 24, 24, 3, 2, 24, 60, 0, nameof(SelfServiceDefaultPolicyOptions.CycleDurationDays))]
    public void Validate_WhenIntegerValueIsOutOfRange_ReturnsFieldError(
        int minShifts,
        int maxShifts,
        int openOffset,
        int closeOffset,
        int cancellationCutoff,
        int maxAbsences,
        int maxLateCancellations,
        int lateCancellationWindow,
        int waitlistOfferMinutes,
        int cycleDurationDays,
        string fieldName)
    {
        var options = new SelfServiceDefaultPolicyOptions
        {
            MinShiftsPerCycle = minShifts,
            MaxShiftsPerCycle = maxShifts,
            RequestWindowOpenOffsetHours = openOffset,
            RequestWindowCloseOffsetHours = closeOffset,
            CancellationCutoffHours = cancellationCutoff,
            MaxAbsencesPerCycle = maxAbsences,
            MaxLateCancellationsPerCycle = maxLateCancellations,
            LateCancellationWindowHours = lateCancellationWindow,
            WaitlistOfferMinutes = waitlistOfferMinutes,
            CycleDurationDays = cycleDurationDays
        };

        options.Validate().Should().Contain(error => error.Contains(fieldName, StringComparison.Ordinal));
    }

    [Fact]
    public void ToConfig_WhenDefaultsAreInvalid_ThrowsDescriptiveError()
    {
        var options = new SelfServiceDefaultPolicyOptions
        {
            WaitlistOfferMinutes = 5
        };

        var act = () => options.ToConfig(Guid.NewGuid(), Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SelfServiceDefaults*WaitlistOfferMinutes*");
    }
}
