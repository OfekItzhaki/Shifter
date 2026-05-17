using FluentValidation;

namespace Jobuler.Application.Scheduling.Models;

/// <summary>
/// Represents settings overrides for the sandbox publish request.
/// All fields are optional — only provided fields are overridden.
/// </summary>
public record SettingsOverrideDto(
    int? MinRestBetweenShiftsHours,
    double? EligibilityThresholdHours,
    double? LeaveDurationHours,
    int? LeaveCapacity,
    int? BalanceValue,
    int? MinPeopleAtBase);

public class SettingsOverrideDtoValidator : AbstractValidator<SettingsOverrideDto>
{
    public SettingsOverrideDtoValidator()
    {
        RuleFor(x => x.MinRestBetweenShiftsHours)
            .InclusiveBetween(0, 24)
            .When(x => x.MinRestBetweenShiftsHours.HasValue)
            .WithMessage("MinRestBetweenShiftsHours must be between 0 and 24 hours.");

        RuleFor(x => x.EligibilityThresholdHours)
            .GreaterThanOrEqualTo(0)
            .When(x => x.EligibilityThresholdHours.HasValue)
            .WithMessage("EligibilityThresholdHours must be non-negative.");

        RuleFor(x => x.LeaveDurationHours)
            .GreaterThan(0)
            .When(x => x.LeaveDurationHours.HasValue)
            .WithMessage("LeaveDurationHours must be positive.");

        RuleFor(x => x.LeaveCapacity)
            .GreaterThanOrEqualTo(1)
            .When(x => x.LeaveCapacity.HasValue)
            .WithMessage("LeaveCapacity must be at least 1.");

        RuleFor(x => x.BalanceValue)
            .InclusiveBetween(0, 100)
            .When(x => x.BalanceValue.HasValue)
            .WithMessage("BalanceValue must be between 0 and 100.");

        RuleFor(x => x.MinPeopleAtBase)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinPeopleAtBase.HasValue)
            .WithMessage("MinPeopleAtBase must be non-negative.");
    }
}
