using FluentValidation;

namespace Jobuler.Application.Notifications.Validators;

public class CreatePushSubscriptionCommandValidator : AbstractValidator<CreatePushSubscriptionCommand>
{
    // Base64URL alphabet: A-Z, a-z, 0-9, -, _ (no padding required)
    private static readonly System.Text.RegularExpressions.Regex Base64UrlRegex =
        new(@"^[A-Za-z0-9\-_]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public CreatePushSubscriptionCommandValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("Endpoint is required.")
            .Must(BeAValidHttpsUrl).WithMessage("Endpoint must be a valid HTTPS URL.");

        RuleFor(x => x.P256dh)
            .NotEmpty().WithMessage("P256dh key is required.")
            .Must(BeValidBase64Url).WithMessage("P256dh must be a valid Base64URL string.");

        RuleFor(x => x.Auth)
            .NotEmpty().WithMessage("Auth secret is required.")
            .Must(BeValidBase64Url).WithMessage("Auth must be a valid Base64URL string.");
    }

    private static bool BeAValidHttpsUrl(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return false;

        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps;
    }

    private static bool BeValidBase64Url(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return Base64UrlRegex.IsMatch(value);
    }
}
