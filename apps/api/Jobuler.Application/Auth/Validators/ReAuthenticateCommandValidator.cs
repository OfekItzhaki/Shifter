using FluentValidation;
using Jobuler.Application.Auth.Commands;

namespace Jobuler.Application.Auth.Validators;

public class ReAuthenticateCommandValidator : AbstractValidator<ReAuthenticateCommand>
{
    public ReAuthenticateCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.Password) ||
                       (!string.IsNullOrEmpty(x.WebAuthnChallengeId) &&
                        !string.IsNullOrEmpty(x.WebAuthnAssertionJson)))
            .WithMessage("Either password or WebAuthn assertion must be provided.");
    }
}
