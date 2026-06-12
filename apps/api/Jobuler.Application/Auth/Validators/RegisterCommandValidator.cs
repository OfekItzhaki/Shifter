using FluentValidation;
using Jobuler.Application.Auth.Commands;

namespace Jobuler.Application.Auth.Validators;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    private static readonly string[] AllowedLocales = ["he", "en", "ru"];
    private static readonly string[] AllowedSetupTemplates =
    [
        "general",
        "restaurant_hospitality",
        "retail_store",
        "security_patrol",
        "military_style",
        "medical_clinic",
        "education_campus",
        "custom"
    ];

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

        When(x => !string.IsNullOrWhiteSpace(x.CountryCode), () =>
        {
            RuleFor(x => x.CountryCode!)
                .Length(2)
                .Matches("^[A-Za-z]{2}$")
                .WithMessage("Country code must be an ISO 3166-1 alpha-2 code.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.StateCode), () =>
        {
            RuleFor(x => x.StateCode!)
                .MaximumLength(12)
                .Matches("^[A-Za-z0-9-]+$")
                .WithMessage("State code must contain only letters, numbers, and dashes.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.SetupTemplate), () =>
        {
            RuleFor(x => x.SetupTemplate!)
                .Must(t => AllowedSetupTemplates.Contains(t.Trim().ToLowerInvariant()))
                .WithMessage("Setup template is not supported.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.OrganizationName), () =>
        {
            RuleFor(x => x.OrganizationName!)
                .MaximumLength(200)
                .WithMessage("Organization name must be 200 characters or fewer.");
        });
    }
}
