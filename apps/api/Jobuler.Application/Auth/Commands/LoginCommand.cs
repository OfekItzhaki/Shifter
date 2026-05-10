using MediatR;

namespace Jobuler.Application.Auth.Commands;

public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public record LoginResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    Guid UserId,
    string DisplayName,
    string PreferredLocale,
    bool IsPlatformAdmin);
