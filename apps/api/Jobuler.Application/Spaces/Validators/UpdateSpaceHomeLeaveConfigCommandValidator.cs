using FluentValidation;
using Jobuler.Application.Spaces.Commands;

namespace Jobuler.Application.Spaces.Validators;

public class UpdateSpaceHomeLeaveConfigCommandValidator : AbstractValidator<UpdateSpaceHomeLeaveConfigCommand>
{
    public UpdateSpaceHomeLeaveConfigCommandValidator()
    {
        RuleFor(x => x.SpaceId)
            .NotEmpty().WithMessage("Space ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Mode)
            .IsInEnum().WithMessage("Mode must be a valid HomeLeaveMode value.");

        RuleFor(x => x.BalanceValue)
            .InclusiveBetween(0, 100)
            .WithMessage("Balance value must be between 0 and 100.");

        RuleFor(x => x.BaseDays)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Base days must be at least 1.");

        RuleFor(x => x.HomeDays)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Home days must be at least 1.");

        RuleFor(x => x.MinPeopleAtBase)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Min people at base must be at least 1.");

        RuleFor(x => x.MinRestHours)
            .InclusiveBetween(0, 16)
            .WithMessage("Min rest hours must be between 0 and 16.");

        RuleFor(x => x.EligibilityThresholdHours)
            .InclusiveBetween(0, 9999)
            .WithMessage("Eligibility threshold hours must be between 0 and 9999.");

        RuleFor(x => x.LeaveCapacity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Leave capacity must be at least 1.");

        RuleFor(x => x.LeaveDurationHours)
            .InclusiveBetween(12, 168)
            .WithMessage("Leave duration hours must be between 12 and 168.");
    }
}
