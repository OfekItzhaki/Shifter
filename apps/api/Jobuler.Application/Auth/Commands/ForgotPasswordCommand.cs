using Jobuler.Application.Auth;
using Jobuler.Application.Common;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Auth.Commands;

public record ForgotPasswordCommand(string Email) : IRequest;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly AppDbContext _db;
    private readonly INotificationSender _notifications;
    private readonly IJwtService _jwt;
    private readonly IContactLookupProtector _contactLookup;

    public ForgotPasswordCommandHandler(
        AppDbContext db, INotificationSender notifications, IJwtService jwt, IContactLookupProtector contactLookup)
    {
        _db = db;
        _notifications = notifications;
        _jwt = jwt;
        _contactLookup = contactLookup;
    }

    public async Task Handle(ForgotPasswordCommand req, CancellationToken ct)
    {
        var emailLookupHash = _contactLookup.HashEmail(req.Email);
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.EmailLookupHash == emailLookupHash && u.IsActive, ct);
        if (user is null)
        {
            var normalizedEmail = _contactLookup.NormalizeEmail(req.Email);
            var legacyUsers = await _db.Users
                .Where(u => u.EmailLookupHash == null && u.IsActive)
                .ToListAsync(ct);
            user = legacyUsers.FirstOrDefault(u => _contactLookup.NormalizeEmail(u.Email) == normalizedEmail);
        }

        // Always return without error — never reveal whether email exists (prevents user enumeration)
        if (user is null) return;

        // Invalidate any existing active tokens for this user
        var existing = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);
        foreach (var t in existing)
            t.MarkUsed();

        // Generate raw token and store only its SHA-256 hash
        var rawToken = _jwt.GenerateRefreshTokenRaw();
        var tokenHash = _jwt.HashToken(rawToken);

        var resetToken = PasswordResetToken.Create(user.Id, tokenHash);
        _db.PasswordResetTokens.Add(resetToken);
        await _db.SaveChangesAsync(ct);

        // Deliver via INotificationSender — send to email always, also try phone if available
        await _notifications.SendPasswordResetAsync(user.Email, rawToken, ct);

        if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            try
            {
                await _notifications.SendPasswordResetAsync(user.PhoneNumber, rawToken, ct);
            }
            catch
            {
                // WhatsApp delivery failure is non-critical — email was already sent
            }
        }
    }
}
