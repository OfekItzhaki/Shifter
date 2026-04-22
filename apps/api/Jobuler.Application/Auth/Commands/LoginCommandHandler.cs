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
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant().Trim() && u.IsActive, ct)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

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

        return new LoginResult(accessToken, rawRefresh, expiresAt, user.Id, user.DisplayName, user.PreferredLocale);
    }
}
