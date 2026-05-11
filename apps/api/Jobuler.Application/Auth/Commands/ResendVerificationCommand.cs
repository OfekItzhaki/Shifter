using System.Security.Cryptography;
using Jobuler.Application.Common;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Auth.Commands;

public record ResendVerificationCommand(Guid UserId) : IRequest;

public class ResendVerificationCommandHandler : IRequestHandler<ResendVerificationCommand>
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ResendVerificationCommandHandler> _logger;
    private readonly string _frontendBaseUrl;

    public ResendVerificationCommandHandler(
        AppDbContext db,
        IEmailSender emailSender,
        ILogger<ResendVerificationCommandHandler> logger,
        IConfiguration configuration)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
        _frontendBaseUrl = configuration["App:FrontendBaseUrl"]?.TrimEnd('/')
            ?? "https://jobuler.app";
    }

    public async Task Handle(ResendVerificationCommand req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.EmailVerified)
            throw new InvalidOperationException("Email already verified.");

        // Invalidate existing active tokens
        var existing = await _db.EmailVerificationTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);
        foreach (var t in existing)
            t.MarkUsed();

        // Generate new 64-char hex token
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = ComputeSha256(rawToken);

        var verificationToken = EmailVerificationToken.Create(user.Id, tokenHash);
        _db.EmailVerificationTokens.Add(verificationToken);
        await _db.SaveChangesAsync(ct);

        // Send verification email (wrapped in try-catch so it doesn't fail the command)
        try
        {
            var verifyUrl = $"{_frontendBaseUrl}/verify-email?token={rawToken}";
            var subject = GetSubject(user.PreferredLocale);
            var html = BuildVerificationEmailHtml(user.DisplayName, verifyUrl, user.PreferredLocale);
            await _emailSender.SendAsync(user.Email, subject, html, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send verification email to user {UserId}", user.Id);
        }
    }

    private static string ComputeSha256(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetSubject(string locale) => locale switch
    {
        "he" => "אימות כתובת אימייל — Shifter",
        "ru" => "Подтверждение email — Shifter",
        _ => "Verify your email — Shifter"
    };

    private static string BuildVerificationEmailHtml(string displayName, string verifyUrl, string locale)
    {
        var (dir, greeting, body, buttonText, footer) = locale switch
        {
            "he" => ("rtl",
                $"שלום {displayName},",
                "אנא אמת את כתובת האימייל שלך על ידי לחיצה על הכפתור למטה:",
                "אמת אימייל",
                "הקישור תקף ל-24 שעות. אם לא נרשמת ל-Shifter, ניתן להתעלם מהודעה זו."),
            "ru" => ("ltr",
                $"Здравствуйте, {displayName},",
                "Пожалуйста, подтвердите ваш email, нажав на кнопку ниже:",
                "Подтвердить email",
                "Ссылка действительна 24 часа. Если вы не регистрировались в Shifter, проигнорируйте это сообщение."),
            _ => ("ltr",
                $"Hi {displayName},",
                "Please verify your email address by clicking the button below:",
                "Verify Email",
                "This link is valid for 24 hours. If you didn't sign up for Shifter, you can ignore this email.")
        };

        return $"""
            <div dir="{dir}" style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
              <h2>{greeting}</h2>
              <p>{body}</p>
              <a href="{verifyUrl}"
                 style="display:inline-block;background:#3b82f6;color:white;padding:12px 24px;
                        border-radius:8px;text-decoration:none;font-weight:bold;margin:16px 0;">
                {buttonText}
              </a>
              <p style="color:#64748b;font-size:12px;">
                {footer}
              </p>
            </div>
            """;
    }
}
