using FluentValidation;
using Jobuler.Application.Platform.Commands;

namespace Jobuler.Application.Platform.Validators;

public class UpdatePlatformSettingsCommandValidator : AbstractValidator<UpdatePlatformSettingsCommand>
{
    public UpdatePlatformSettingsCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.PlatformTimeoutMinutes)
            .InclusiveBetween(5, 120)
            .WithMessage("Platform timeout must be between 5 and 120 minutes.");
    }
}
