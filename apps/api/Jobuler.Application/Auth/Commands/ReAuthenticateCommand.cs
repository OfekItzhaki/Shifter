using System.Text.Json;
using Jobuler.Application.Common;
using Jobuler.Domain.Auth;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Auth.Commands;

/// <summary>
/// Re-authenticates a user before entering an elevated privilege mode
/// (Management Mode or Super Platform Mode).
/// Accepts either password or WebAuthn assertion.
/// </summary>
public record ReAuthenticateCommand(
    Guid UserId,
    string? Password,
    string? WebAuthnChallengeId,
    string? WebAuthnAssertionJson,
    Guid? SpaceId,
    string? IpAddress,
    string? WebAuthnFailureReason = null
) : IRequest<ReAuthenticateResult>;

/// <summary>
/// Result of a re-authentication attempt.
/// IsLockedOut indicates the user has exceeded the failure threshold (5 in 15 min).
/// RetryAfterSeconds tells the client how long to wait before retrying.
/// </summary>
public record ReAuthenticateResult(bool Success, bool IsLockedOut = false, int RetryAfterSeconds = 0);

public class ReAuthenticateCommandHandler : IRequestHandler<ReAuthenticateCommand, ReAuthenticateResult>
{
    private const int MaxFailedAttempts = 5;
    private const int LockoutWindowMinutes = 15;

    private readonly AppDbContext _db;
    private readonly IWebAuthnService _webAuthn;
    private readonly IAuditLogger _audit;
    private readonly ILogger<ReAuthenticateCommandHandler> _logger;

    public ReAuthenticateCommandHandler(
        AppDbContext db,
        IWebAuthnService webAuthn,
        IAuditLogger audit,
        ILogger<ReAuthenticateCommandHandler> logger)
    {
        _db = db;
        _webAuthn = webAuthn;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ReAuthenticateResult> Handle(ReAuthenticateCommand request, CancellationToken ct)
    {
        // Determine authentication method for audit logging
        var method = !string.IsNullOrEmpty(request.Password) ? "password" : "webauthn";

        // --- Lockout check: query failures in last 15 minutes ---
        var windowStart = DateTime.UtcNow.AddMinutes(-LockoutWindowMinutes);
        var recentFailureCount = await _db.ReAuthAttempts
            .CountAsync(a => a.UserId == request.UserId
                          && !a.Success
                          && a.AttemptedAt >= windowStart, ct);

        if (recentFailureCount >= MaxFailedAttempts)
        {
            // User is locked out — log lockout event and return 429
            _logger.LogWarning("Re-auth lockout triggered for user {UserId}. {Count} failures in last {Window} minutes.",
                request.UserId, recentFailureCount, LockoutWindowMinutes);

            await LogLockoutEvent(request, method, ct);
            return new ReAuthenticateResult(Success: false, IsLockedOut: true, RetryAfterSeconds: LockoutWindowMinutes * 60);
        }

        // --- Reject passwords > 128 chars without hashing (DoS prevention) ---
        if (!string.IsNullOrEmpty(request.Password) && request.Password.Length > 128)
        {
            await RecordAttempt(request.UserId, success: false, method, ct);
            await LogAttempt(request, method, success: false, ct);
            return new ReAuthenticateResult(false);
        }

        // Load user from DB
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, ct);

        if (user == null)
        {
            await RecordAttempt(request.UserId, success: false, method, ct);
            await LogAttempt(request, method, success: false, ct);
            return new ReAuthenticateResult(false);
        }

        bool verified;

        if (!string.IsNullOrEmpty(request.Password))
        {
            // Password-based re-authentication
            verified = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        else if (!string.IsNullOrEmpty(request.WebAuthnChallengeId) &&
                 !string.IsNullOrEmpty(request.WebAuthnAssertionJson))
        {
            // WebAuthn-based re-authentication
            verified = await VerifyWebAuthnAsync(request, ct);
        }
        else
        {
            // No valid credential provided
            await RecordAttempt(request.UserId, success: false, method, ct);
            await LogAttempt(request, method, success: false, ct);
            return new ReAuthenticateResult(false);
        }

        // Record the attempt (success or failure)
        await RecordAttempt(request.UserId, verified, method, ct);
        await LogAttempt(request, method, verified, ct);
        return new ReAuthenticateResult(verified);
    }

    private async Task<bool> VerifyWebAuthnAsync(ReAuthenticateCommand request, CancellationToken ct)
    {
        try
        {
            // Extract credential ID from the assertion to look up the stored credential
            var credentialId = ExtractCredentialIdFromAssertion(request.WebAuthnAssertionJson!);

            var credential = await _db.WebAuthnCredentials
                .FirstOrDefaultAsync(c => c.UserId == request.UserId &&
                                          c.CredentialId == credentialId &&
                                          !c.IsDisabled, ct);

            if (credential == null)
                return false;

            var result = await _webAuthn.CompleteAuthenticationAsync(
                request.WebAuthnChallengeId!,
                request.WebAuthnAssertionJson!,
                credential.PublicKey,
                credential.SignCount,
                ct);

            // Update sign count on successful verification
            credential.UpdateSignCount(result.NewSignCount);
            await _db.SaveChangesAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebAuthn re-authentication failed for user {UserId}", request.UserId);
            return false;
        }
    }

    /// <summary>
    /// Records a re-authentication attempt in the reauth_attempts table for lockout tracking.
    /// </summary>
    private async Task RecordAttempt(Guid userId, bool success, string method, CancellationToken ct)
    {
        var attempt = ReAuthAttempt.Create(userId, success, method);
        _db.ReAuthAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Logs the re-authentication attempt to the audit log with method, outcome,
    /// and optional WebAuthn failure reason.
    /// </summary>
    private async Task LogAttempt(ReAuthenticateCommand request, string method, bool success, CancellationToken ct)
    {
        object afterSnapshot;

        if (method == "webauthn" && !success && !string.IsNullOrEmpty(request.WebAuthnFailureReason))
        {
            afterSnapshot = new
            {
                method,
                success,
                failureReason = request.WebAuthnFailureReason
            };
        }
        else
        {
            afterSnapshot = new
            {
                method,
                success
            };
        }

        var afterJson = JsonSerializer.Serialize(afterSnapshot);

        await _audit.LogAsync(
            spaceId: request.SpaceId,
            actorUserId: request.UserId,
            action: "re_authenticate",
            entityType: "user",
            entityId: request.UserId,
            afterJson: afterJson,
            ipAddress: request.IpAddress,
            ct: ct);
    }

    /// <summary>
    /// Logs a lockout event to the audit log when the user exceeds the failure threshold.
    /// </summary>
    private async Task LogLockoutEvent(ReAuthenticateCommand request, string method, CancellationToken ct)
    {
        var afterJson = JsonSerializer.Serialize(new
        {
            method,
            success = false,
            lockout = true,
            reason = "exceeded_max_attempts",
            maxAttempts = MaxFailedAttempts,
            windowMinutes = LockoutWindowMinutes
        });

        await _audit.LogAsync(
            spaceId: request.SpaceId,
            actorUserId: request.UserId,
            action: "re_authenticate_lockout",
            entityType: "user",
            entityId: request.UserId,
            afterJson: afterJson,
            ipAddress: request.IpAddress,
            ct: ct);
    }

    private static byte[] ExtractCredentialIdFromAssertion(string assertionResponseJson)
    {
        using var doc = JsonDocument.Parse(assertionResponseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("id", out var idElement))
        {
            var idBase64Url = idElement.GetString()
                ?? throw new InvalidOperationException("Assertion response missing credential ID.");
            return Base64UrlDecode(idBase64Url);
        }

        if (root.TryGetProperty("rawId", out var rawIdElement))
        {
            var rawIdBase64Url = rawIdElement.GetString()
                ?? throw new InvalidOperationException("Assertion response missing credential ID.");
            return Base64UrlDecode(rawIdBase64Url);
        }

        throw new InvalidOperationException("Assertion response does not contain a credential ID.");
    }

    private static byte[] Base64UrlDecode(string base64Url)
    {
        var base64 = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }
}
