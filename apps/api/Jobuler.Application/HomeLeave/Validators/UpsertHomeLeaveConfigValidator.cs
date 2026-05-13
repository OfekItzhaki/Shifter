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
            .InclusiveBetween(4, 16)
            .WithMessage("min_rest_hours must be between 4 and 16 inclusive.");

        RuleFor(x => x.EligibilityThresholdHours)
            .InclusiveBetween(4, 48)
            .WithMessage("eligibility_threshold_hours must be between min_rest_hours and 48 inclusive.")
            .Must((cmd, threshold) => threshold >= cmd.MinRestHours)
            .WithMessage("eligibility_threshold_hours must be greater than or equal to min_rest_hours.");

        RuleFor(x => x.LeaveCapacity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("leave_capacity must be at least 1.");

        RuleFor(x => x.LeaveDurationHours)
            .InclusiveBetween(12, 168)
            .WithMessage("leave_duration_hours must be between 12 and 168 inclusive.");
    }
}
