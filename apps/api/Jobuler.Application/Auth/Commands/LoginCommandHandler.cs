using Jobuler.Application.Auth;
using Jobuler.Application.Common;
using Jobuler.Application.Conflicts;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Auth.Commands;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly ITimezoneResolver _timezoneResolver;
    private readonly IContactLookupProtector _contactLookup;
    private readonly int _refreshTokenExpiryDays;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        AppDbContext db,
        IJwtService jwt,
        ITimezoneResolver timezoneResolver,
        IContactLookupProtector contactLookup,
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<LoginCommandHandler> logger)
    {
        _db = db;
        _jwt = jwt;
        _timezoneResolver = timezoneResolver;
        _contactLookup = contactLookup;
        _refreshTokenExpiryDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "7");
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken ct)
    {
        var identifier = request.Identifier.Trim();

        // Determine if identifier is email or phone
        var isEmail = identifier.Contains('@');

        User? user;
        if (isEmail)
        {
            var normalizedEmail = _contactLookup.NormalizeEmail(identifier);
            var emailLookupHash = _contactLookup.HashEmail(identifier);
            user = await _db.Users
                .FirstOrDefaultAsync(u => u.EmailLookupHash == emailLookupHash && u.IsActive, ct);
            if (user is null)
            {
                var legacyUsers = await _db.Users
                    .Where(u => u.EmailLookupHash == null && u.IsActive)
                    .ToListAsync(ct);
                user = legacyUsers.FirstOrDefault(u => _contactLookup.NormalizeEmail(u.Email) == normalizedEmail);
            }
        }
        else
        {
            var normalizedPhone = _contactLookup.NormalizePhone(identifier);
            var phoneLookupHash = _contactLookup.HashPhone(identifier);
            user = await _db.Users
                .FirstOrDefaultAsync(u => u.PhoneLookupHash == phoneLookupHash && u.IsActive, ct);
            if (user is null)
            {
                var legacyUsers = await _db.Users
                    .Where(u => u.PhoneLookupHash == null && u.PhoneNumber != null && u.IsActive)
                    .ToListAsync(ct);
                user = legacyUsers.FirstOrDefault(u => u.PhoneNumber is not null && _contactLookup.NormalizePhone(u.PhoneNumber) == normalizedPhone);
            }
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

        // Fire-and-forget: cross-group conflict detection
        var userId = user.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var conflictService = scope.ServiceProvider.GetRequiredService<IConflictDetectionService>();
                await conflictService.DetectOnLoginAsync(userId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<ILogger<LoginCommandHandler>>()
                    .LogError(ex, "Conflict detection failed on login for user {UserId}", userId);
            }
        });

        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, user.DisplayName);
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        // Resolve timezone from user's geographic location
        var timezone = _timezoneResolver.Resolve(user.CountryCode, user.StateCode);

        return new LoginResult(accessToken, rawRefresh, expiresAt, user.Id, user.DisplayName, user.PreferredLocale, user.IsPlatformAdmin,
            timezone.IanaTimezoneId, timezone.OffsetMinutes);
    }
}
