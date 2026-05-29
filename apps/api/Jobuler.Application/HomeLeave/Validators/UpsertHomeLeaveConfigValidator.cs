using FluentValidation;
using Jobuler.Application.HomeLeave.Commands;
using Jobuler.Domain.Groups;

namespace Jobuler.Application.HomeLeave.Validators;

public class UpsertHomeLeaveConfigValidator : AbstractValidator<UpsertHomeLeaveConfigCommand>
{
    public UpsertHomeLeaveConfigValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();

        RuleFor(x => x.MinRestHours)
            .InclusiveBetween(0, 16)
            .WithMessage("min_rest_hours must be between 0 and 16 inclusive.");

        RuleFor(x => x.EligibilityThresholdHours)
            .InclusiveBetween(0, 9999)
            .WithMessage("eligibility_threshold_hours must be between 0 and 9999 inclusive.");

        RuleFor(x => x.LeaveCapacity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("leave_capacity must be at least 1.");

        RuleFor(x => x.LeaveDurationHours)
            .InclusiveBetween(12, 168)
            .WithMessage("leave_duration_hours must be between 12 and 168 inclusive.");

        RuleFor(x => x.BalanceValue)
            .InclusiveBetween(0, 100)
            .When(x => x.BalanceValue.HasValue)
            .WithMessage("Balance value must be between 0 and 100");

        // Mode validation
        RuleFor(x => x.Mode)
            .IsInEnum()
            .When(x => x.Mode.HasValue)
            .WithMessage("Mode must be 'automatic' or 'manual'");

        // BaseDays validation — required for Manual mode
        RuleFor(x => x.BaseDays)
            .GreaterThanOrEqualTo(1)
            .When(x => x.BaseDays.HasValue)
            .WithMessage("Base days must be at least 1");

        RuleFor(x => x.BaseDays)
            .NotNull()
            .When(x => x.Mode == HomeLeaveMode.Manual)
            .WithMessage("BaseDays is required for Manual mode.");

        // HomeDays validation — required for Manual mode
        RuleFor(x => x.HomeDays)
            .GreaterThanOrEqualTo(1)
            .When(x => x.HomeDays.HasValue)
            .WithMessage("Home days must be at least 1");

        RuleFor(x => x.HomeDays)
            .NotNull()
            .When(x => x.Mode == HomeLeaveMode.Manual)
            .WithMessage("HomeDays is required for Manual mode.");

        // SliderValue validation — required for Automatic mode when provided
        RuleFor(x => x.SliderValue)
            .InclusiveBetween(0, 100)
            .When(x => x.SliderValue.HasValue)
            .WithMessage("SliderValue must be between 0 and 100.");
    }
}
