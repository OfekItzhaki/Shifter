using FluentValidation;
using Jobuler.Application.HomeLeave.Commands;

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
            .InclusiveBetween(0, 336)
            .WithMessage("eligibility_threshold_hours must be between 0 and 336 (14 days) inclusive.");

        RuleFor(x => x.LeaveCapacity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("leave_capacity must be at least 1.");

        RuleFor(x => x.LeaveDurationHours)
            .InclusiveBetween(12, 168)
            .WithMessage("leave_duration_hours must be between 12 and 168 inclusive.");

        RuleFor(x => x.BalanceValue)
            .InclusiveBetween(0, 100)
            .When(x => x.BalanceValue.HasValue)
            .WithMessage("ערך האיזון חייב להיות בין 0 ל-100");
    }
}
