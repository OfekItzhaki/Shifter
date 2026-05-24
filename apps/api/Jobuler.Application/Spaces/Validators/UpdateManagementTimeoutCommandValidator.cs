using FluentValidation;
using Jobuler.Application.Spaces.Commands;

namespace Jobuler.Application.Spaces.Validators;

public class UpdateManagementTimeoutCommandValidator : AbstractValidator<UpdateManagementTimeoutCommand>
{
    public UpdateManagementTimeoutCommandValidator()
    {
        RuleFor(x => x.SpaceId)
            .NotEmpty().WithMessage("Space ID is required.");

        RuleFor(x => x.Minutes)
            .InclusiveBetween(5, 120)
            .WithMessage("Management timeout must be between 5 and 120 minutes.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
