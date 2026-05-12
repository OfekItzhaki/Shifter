using FluentValidation;
using Jobuler.Application.Auth.Commands;

namespace Jobuler.Application.Auth.Validators;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    private static readonly string[] AllowedLocales = ["he", "en", "ru"];

    public RegisterCommandValidator()
    {
        // At least one of email or phone must be provided
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Either email or phone number is required.");

        // Email validation (only when provided)
        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Invalid email format.")
                .MaximumLength(256);
        });

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

        RuleFor(x => x.PreferredLocale)
            .Must(l => AllowedLocales.Contains(l))
            .WithMessage("Locale must be one of: he, en, ru.");
    }
}
