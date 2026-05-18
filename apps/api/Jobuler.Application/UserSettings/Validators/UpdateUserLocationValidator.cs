using FluentValidation;
using Jobuler.Application.UserSettings.Commands;

namespace Jobuler.Application.UserSettings.Validators;

/// <summary>
/// Validates UpdateUserLocationCommand:
/// - CountryCode must be a valid ISO 3166-1 alpha-2 code supported by the system
/// - StateCode (if provided) must be a valid ISO 3166-2 subdivision for the given country
/// </summary>
public class UpdateUserLocationValidator : AbstractValidator<UpdateUserLocationCommand>
{
    public UpdateUserLocationValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.CountryCode)
            .NotEmpty().WithMessage("Country code is required.")
            .Length(2).WithMessage("Country code must be exactly 2 characters (ISO 3166-1 alpha-2).")
            .Must(code => ValidLocationCodes.IsValidCountryCode(code))
            .WithMessage("Invalid ISO 3166-1 alpha-2 country code.");

        RuleFor(x => x.StateCode)
            .Must((cmd, stateCode) =>
            {
                // If no state code provided, that's always valid (state is optional)
                if (string.IsNullOrWhiteSpace(stateCode))
                    return true;

                // If the country doesn't have state subdivisions, state code is not applicable
                if (!ValidLocationCodes.HasStateSubdivisions(cmd.CountryCode))
                    return false;

                // Validate state belongs to the country
                return ValidLocationCodes.IsValidStateCode(cmd.CountryCode, stateCode);
            })
            .WithMessage("Subdivision code does not belong to the selected country.");
    }
}
