using Jobuler.Application.Auth;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Jobuler.Application.Auth.Commands;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResult>
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly int _refreshTokenExpiryDays;

    public RefreshTokenCommandHandler(AppDbContext db, IJwtService jwt, IConfiguration config)
    {
        _db = db;
        _jwt = jwt;
        _refreshTokenExpiryDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "7");
    }

    public async Task<LoginResult> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var tokenHash = _jwt.HashToken(request.RefreshToken);

        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (existing is null || !existing.IsActive)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Rotate: revoke old, issue new
        existing.Revoke();

        var rawRefresh = _jwt.GenerateRefreshTokenRaw();
        var newHash = _jwt.HashToken(rawRefresh);
        var newToken = RefreshToken.Create(existing.UserId, newHash, _refreshTokenExpiryDays);

        _db.RefreshTokens.Add(newToken);
        await _db.SaveChangesAsync(ct);

        var accessToken = _jwt.GenerateAccessToken(
            existing.User.Id, existing.User.Email, existing.User.DisplayName);

        return new LoginResult(
            accessToken, rawRefresh,
            DateTime.UtcNow.AddMinutes(15),
            existing.User.Id, existing.User.DisplayName, existing.User.PreferredLocale);
    }
}
