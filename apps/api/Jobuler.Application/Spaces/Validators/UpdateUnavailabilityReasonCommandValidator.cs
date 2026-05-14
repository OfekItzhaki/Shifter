using FluentValidation;
using Jobuler.Application.Spaces.Commands;

namespace Jobuler.Application.Spaces.Validators;

public class UpdateUnavailabilityReasonCommandValidator : AbstractValidator<UpdateUnavailabilityReasonCommand>
{
    public UpdateUnavailabilityReasonCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.ReasonId).NotEmpty();

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(100).WithMessage("Display name cannot exceed 100 characters.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sort order must be non-negative.");
    }
}
