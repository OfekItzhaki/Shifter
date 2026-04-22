using System.Security.Claims;

namespace Jobuler.Application.Auth;

/// <summary>
/// Contract for JWT token generation and validation.
/// Defined in Application so handlers can depend on it without referencing Infrastructure.
/// Implemented by JwtService in Infrastructure.
/// </summary>
public interface IJwtService
{
    string GenerateAccessToken(Guid userId, string email, string displayName);
    string GenerateRefreshTokenRaw();
    string HashToken(string rawToken);
    ClaimsPrincipal? ValidateAccessToken(string token);
}
