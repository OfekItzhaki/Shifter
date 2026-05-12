using Jobuler.Application.Auth;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Jobuler.Application.Auth.Commands;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly int _refreshTokenExpiryDays;

    public LoginCommandHandler(AppDbContext db, IJwtService jwt, IConfiguration config)
    {
        _db = db;
        _jwt = jwt;
        _refreshTokenExpiryDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "7");
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken ct)
    {
        var identifier = request.Identifier.Trim().ToLowerInvariant();

        // Determine if identifier is email or phone
        var isEmail = identifier.Contains('@');

        User? user;
        if (isEmail)
        {
            user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == identifier && u.IsActive, ct);
        }
        else
        {
            // Normalize phone: strip spaces, dashes, parentheses
            var normalizedPhone = NormalizePhone(request.Identifier.Trim());
            user = await _db.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber != null && u.PhoneNumber == normalizedPhone && u.IsActive, ct);
        }

        if (user == null)
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        user.RecordLogin();

        var rawRefresh = _jwt.GenerateRefreshTokenRaw();
        var tokenHash = _jwt.HashToken(rawRefresh);
        var refreshToken = RefreshToken.Create(user.Id, tokenHash, _refreshTokenExpiryDays);

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, user.DisplayName);
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        return new LoginResult(accessToken, rawRefresh, expiresAt, user.Id, user.DisplayName, user.PreferredLocale, user.IsPlatformAdmin);
    }

    private static string NormalizePhone(string phone)
    {
        // Remove common formatting characters
        return phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
    }
}
