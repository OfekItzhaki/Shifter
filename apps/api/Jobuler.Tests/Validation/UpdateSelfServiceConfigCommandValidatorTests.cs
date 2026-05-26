// Feature: self-service-scheduling
// Unit tests for UpdateSelfServiceConfigCommandValidator

using FluentAssertions;
using FluentValidation.TestHelper;
using Jobuler.Application.Scheduling.SelfService.Commands;
using Xunit;

namespace Jobuler.Tests.Validation;

public class UpdateSelfServiceConfigCommandValidatorTests
{
    private readonly UpdateSelfServiceConfigCommandValidator _validator = new();

    private static UpdateSelfServiceConfigCommand ValidCommand() => new(
        SpaceId: Guid.NewGuid(),
        GroupId: Guid.NewGuid(),
        MinShiftsPerCycle: 2,
        MaxShiftsPerCycle: 7,
        RequestWindowOpenOffsetHours: 168,
        RequestWindowCloseOffsetHours: 24,
        CancellationCutoffHours: 24,
        WaitlistOfferMinutes: 60,
        CycleDurationDays: 7);

    [Fact]
    public void Valid_Command_PassesValidation()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Min/Max shifts validation ────────────────────────────────────────────

    [Fact]
    public void MinShifts_GreaterThan_MaxShifts_Fails()
    {
        var cmd = ValidCommand() with { MinShiftsPerCycle = 10, MaxShiftsPerCycle = 5 };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.MinShiftsPerCycle);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void MinShifts_OutOfRange_Fails(int min)
    {
        var cmd = ValidCommand() with { MinShiftsPerCycle = min };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.MinShiftsPerCycle);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void MaxShifts_OutOfRange_Fails(int max)
    {
        var cmd = ValidCommand() with { MaxShiftsPerCycle = max };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.MaxShiftsPerCycle);
    }

    [Fact]
    public void MinShifts_EqualTo_MaxShifts_Passes()
    {
        var cmd = ValidCommand() with { MinShiftsPerCycle = 5, MaxShiftsPerCycle = 5 };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.MinShiftsPerCycle);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxShiftsPerCycle);
    }

    [Fact]
    public void MinShifts_Zero_Passes()
    {
        var cmd = ValidCommand() with { MinShiftsPerCycle = 0 };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.MinShiftsPerCycle);
    }

    [Fact]
    public void MaxShifts_One_Passes()
    {
        var cmd = ValidCommand() with { MinShiftsPerCycle = 0, MaxShiftsPerCycle = 1 };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxShiftsPerCycle);
    }

    [Fact]
    public void MaxShifts_Hundred_Passes()
    {
        var cmd = ValidCommand() with { MinShiftsPerCycle = 0, MaxShiftsPerCycle = 100 };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxShiftsPerCycle);
    }

    // ── Request window offset validation ─────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(721)]
    public void RequestWindowOpenOffset_OutOfRange_Fails(int offset)
    {
        var cmd = ValidCommand() with { RequestWindowOpenOffsetHours = offset };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RequestWindowOpenOffsetHours);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(721)]
    public void RequestWindowCloseOffset_OutOfRange_Fails(int offset)
    {
        var cmd = ValidCommand() with { RequestWindowCloseOffsetHours = offset };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RequestWindowCloseOffsetHours);
    }

    [Fact]
    public void RequestWindowOpenOffset_NotGreaterThan_CloseOffset_Fails()
    {
        var cmd = ValidCommand() with { RequestWindowOpenOffsetHours = 24, RequestWindowCloseOffsetHours = 48 };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RequestWindowOpenOffsetHours);
    }

    [Fact]
    public void RequestWindowOpenOffset_EqualTo_CloseOffset_Fails()
    {
        var cmd = ValidCommand() with { RequestWindowOpenOffsetHours = 48, RequestWindowCloseOffsetHours = 48 };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RequestWindowOpenOffsetHours);
    }

    [Fact]
    public void RequestWindowOffsets_BoundaryValues_Pass()
    {
        var cmd = ValidCommand() with { RequestWindowOpenOffsetHours = 720, RequestWindowCloseOffsetHours = 1 };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.RequestWindowOpenOffsetHours);
        result.ShouldNotHaveValidationErrorFor(x => x.RequestWindowCloseOffsetHours);
    }

    // ── Cancellation cutoff validation ───────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(721)]
    public void CancellationCutoffHours_OutOfRange_Fails(int hours)
    {
        var cmd = ValidCommand() with { CancellationCutoffHours = hours };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.CancellationCutoffHours);
    }

    [Fact]
    public void CancellationCutoffHours_BoundaryValues_Pass()
    {
        var cmd1 = ValidCommand() with { CancellationCutoffHours = 1 };
        _validator.TestValidate(cmd1).ShouldNotHaveValidationErrorFor(x => x.CancellationCutoffHours);

        var cmd2 = ValidCommand() with { CancellationCutoffHours = 720 };
        _validator.TestValidate(cmd2).ShouldNotHaveValidationErrorFor(x => x.CancellationCutoffHours);
    }

    // ── Waitlist offer minutes validation ────────────────────────────────────

    [Theory]
    [InlineData(14)]
    [InlineData(1441)]
    public void WaitlistOfferMinutes_OutOfRange_Fails(int minutes)
    {
        var cmd = ValidCommand() with { WaitlistOfferMinutes = minutes };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.WaitlistOfferMinutes);
    }

    [Fact]
    public void WaitlistOfferMinutes_BoundaryValues_Pass()
    {
        var cmd1 = ValidCommand() with { WaitlistOfferMinutes = 15 };
        _validator.TestValidate(cmd1).ShouldNotHaveValidationErrorFor(x => x.WaitlistOfferMinutes);

        var cmd2 = ValidCommand() with { WaitlistOfferMinutes = 1440 };
        _validator.TestValidate(cmd2).ShouldNotHaveValidationErrorFor(x => x.WaitlistOfferMinutes);
    }

    // ── Cycle duration validation ────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void CycleDurationDays_OutOfRange_Fails(int days)
    {
        var cmd = ValidCommand() with { CycleDurationDays = days };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.CycleDurationDays);
    }

    [Fact]
    public void CycleDurationDays_BoundaryValues_Pass()
    {
        var cmd1 = ValidCommand() with { CycleDurationDays = 1 };
        _validator.TestValidate(cmd1).ShouldNotHaveValidationErrorFor(x => x.CycleDurationDays);

        var cmd2 = ValidCommand() with { CycleDurationDays = 30 };
        _validator.TestValidate(cmd2).ShouldNotHaveValidationErrorFor(x => x.CycleDurationDays);
    }

    // ── Required fields ──────────────────────────────────────────────────────

    [Fact]
    public void Empty_SpaceId_Fails()
    {
        var cmd = ValidCommand() with { SpaceId = Guid.Empty };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SpaceId);
    }

    [Fact]
    public void Empty_GroupId_Fails()
    {
        var cmd = ValidCommand() with { GroupId = Guid.Empty };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.GroupId);
    }
}
