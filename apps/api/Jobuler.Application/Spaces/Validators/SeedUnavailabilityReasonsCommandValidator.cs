using FluentValidation;
using Jobuler.Application.Spaces.Commands;

namespace Jobuler.Application.Spaces.Validators;

public class SeedUnavailabilityReasonsCommandValidator : AbstractValidator<SeedUnavailabilityReasonsCommand>
{
    public SeedUnavailabilityReasonsCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();

        RuleFor(x => x.ReasonDisplayNames)
            .NotNull().WithMessage("Reason display names list is required.");

        RuleForEach(x => x.ReasonDisplayNames)
            .NotEmpty().WithMessage("Each reason display name must not be empty.")
            .MaximumLength(100).WithMessage("Each reason display name cannot exceed 100 characters.");
    }
}
