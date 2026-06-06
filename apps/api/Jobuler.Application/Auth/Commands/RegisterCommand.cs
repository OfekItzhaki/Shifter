using MediatR;

namespace Jobuler.Application.Auth.Commands;

public record RegisterCommand(
    string Email,
    string DisplayName,
    string Password,
    string PreferredLocale = "he",
    string? PhoneNumber = null,
    string? ProfileImageUrl = null,
    DateOnly? Birthday = null,
    string? CountryCode = null,
    string? StateCode = null,
    string? SetupTemplate = null,
    string? OrganizationName = null) : IRequest<Guid>;
