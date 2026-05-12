using System.Security.Cryptography;
using Jobuler.Application.Common;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Auth.Commands;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<RegisterCommandHandler> _logger;
    private readonly string _frontendBaseUrl;

    public RegisterCommandHandler(
        AppDbContext db,
        IEmailSender emailSender,
        ILogger<RegisterCommandHandler> logger,
        IConfiguration configuration)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
        _frontendBaseUrl = configuration["App:FrontendBaseUrl"]?.TrimEnd('/')
            ?? "https://shifter.ofeklabs.com";
    }

    public async Task<Guid> Handle(RegisterCommand request, CancellationToken ct)
    {
        var exists = await _db.Users.AnyAsync(u => u.Email == request.Email.ToLowerInvariant().Trim(), ct);
        if (exists)
            throw new InvalidOperationException("Email already registered.");

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
        var user = User.Create(request.Email, request.DisplayName, hash, request.PreferredLocale, request.PhoneNumber, request.ProfileImageUrl, request.Birthday);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // Auto-create a personal space for the new user
        var displayName = request.DisplayName ?? request.Email.Split('@')[0];
        var spaceName = (request.PreferredLocale ?? "he") switch
        {
            "he" => $"המרחב של {displayName}",
            "ru" => $"Пространство {displayName}",
            _ => $"{displayName}'s Space",
        };
        var space = Jobuler.Domain.Spaces.Space.Create(spaceName, user.Id);
        _db.Spaces.Add(space);

        // Add user as space member
        var membership = Jobuler.Domain.Spaces.SpaceMembership.Create(space.Id, user.Id);
        _db.SpaceMemberships.Add(membership);

        // Generate email verification token
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = ComputeSha256(rawToken);
        var verificationToken = EmailVerificationToken.Create(user.Id, tokenHash);
        _db.EmailVerificationTokens.Add(verificationToken);

        await _db.SaveChangesAsync(ct);

        // Send verification email — wrapped in try-catch so registration succeeds even if email fails
        try
        {
            var verifyUrl = $"{_frontendBaseUrl}/verify-email?token={rawToken}";
            var locale = request.PreferredLocale ?? "he";
            var subject = GetSubject(locale);
            var html = BuildVerificationEmailHtml(user.DisplayName, verifyUrl, locale);
            await _emailSender.SendAsync(user.Email, subject, html, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send verification email during registration for user {UserId}", user.Id);
        }

        return user.Id;
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
