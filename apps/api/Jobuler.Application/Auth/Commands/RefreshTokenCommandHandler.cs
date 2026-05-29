using Jobuler.Application.Auth;
using Jobuler.Application.Common;
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
    private readonly ITimezoneResolver _timezoneResolver;
    private readonly int _refreshTokenExpiryDays;

    public RefreshTokenCommandHandler(AppDbContext db, IJwtService jwt, ITimezoneResolver timezoneResolver, IConfiguration config)
    {
        _db = db;
        _jwt = jwt;
        _timezoneResolver = timezoneResolver;
        _refreshTokenExpiryDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "7");
    }

    public async Task<LoginResult> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var tokenHash = _jwt.HashToken(request.RefreshToken);

        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (existing is null || existing.IsExpired)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Grace period: if the token was revoked within the last 30 seconds,
        // the client likely didn't receive the previous refresh response (network
        // failure, server restart mid-request). Find and return the replacement token
        // that was issued when this one was revoked, rather than forcing re-login.
        if (existing.IsRevoked)
        {
            var gracePeriod = TimeSpan.FromSeconds(30);
            var revokedRecently = existing.RevokedAt.HasValue
                && (DateTime.UtcNow - existing.RevokedAt.Value) < gracePeriod;

            if (!revokedRecently)
                throw new UnauthorizedAccessException("Invalid or expired refresh token.");

            // Find the replacement token that was created around the same time
            var replacement = await _db.RefreshTokens
                .Include(t => t.User)
                .Where(t => t.UserId == existing.UserId
                    && t.CreatedAt >= existing.RevokedAt.Value.AddSeconds(-2)
                    && t.CreatedAt <= existing.RevokedAt.Value.AddSeconds(2)
                    && t.TokenHash != existing.TokenHash
                    && t.RevokedAt == null)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (replacement is null || !replacement.IsActive)
                throw new UnauthorizedAccessException("Invalid or expired refresh token.");

            // Re-rotate: revoke the replacement and issue a fresh one
            replacement.Revoke();

            var graceRawRefresh = _jwt.GenerateRefreshTokenRaw();
            var graceNewHash = _jwt.HashToken(graceRawRefresh);
            var graceNewToken = RefreshToken.Create(existing.UserId, graceNewHash, _refreshTokenExpiryDays);

            _db.RefreshTokens.Add(graceNewToken);
            await _db.SaveChangesAsync(ct);

            var graceAccessToken = _jwt.GenerateAccessToken(
                existing.User.Id, existing.User.Email, existing.User.DisplayName);

            var graceTz = _timezoneResolver.Resolve(existing.User.CountryCode, existing.User.StateCode);

            return new LoginResult(
                graceAccessToken, graceRawRefresh,
                DateTime.UtcNow.AddMinutes(15),
                existing.User.Id, existing.User.DisplayName, existing.User.PreferredLocale, existing.User.IsPlatformAdmin,
                graceTz.IanaTimezoneId, graceTz.OffsetMinutes);
        }

        // Normal rotation: revoke old, issue new
        existing.Revoke();

        var rawRefresh = _jwt.GenerateRefreshTokenRaw();
        var newHash = _jwt.HashToken(rawRefresh);
        var newToken = RefreshToken.Create(existing.UserId, newHash, _refreshTokenExpiryDays);

        _db.RefreshTokens.Add(newToken);
        await _db.SaveChangesAsync(ct);

        var accessToken = _jwt.GenerateAccessToken(
            existing.User.Id, existing.User.Email, existing.User.DisplayName);

        // Recalculate timezone on refresh to handle DST changes between sessions
        var timezone = _timezoneResolver.Resolve(existing.User.CountryCode, existing.User.StateCode);

        return new LoginResult(
            accessToken, rawRefresh,
            DateTime.UtcNow.AddMinutes(15),
            existing.User.Id, existing.User.DisplayName, existing.User.PreferredLocale, existing.User.IsPlatformAdmin,
            timezone.IanaTimezoneId, timezone.OffsetMinutes);
    }
}
