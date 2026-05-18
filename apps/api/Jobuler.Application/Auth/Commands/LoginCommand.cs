using MediatR;

namespace Jobuler.Application.Auth.Commands;

/// <summary>
/// Login with email or phone number + password.
/// The Identifier field accepts either an email address or a phone number.
/// </summary>
public record LoginCommand(string Identifier, string Password) : IRequest<LoginResult>;

public record LoginResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    Guid UserId,
    string DisplayName,
    string PreferredLocale,
    bool IsPlatformAdmin,
    string TimezoneId,
    int TimezoneOffsetMinutes);
