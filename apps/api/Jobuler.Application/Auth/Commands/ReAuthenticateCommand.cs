using System.Text.Json;
using Jobuler.Application.Common;
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
    string? IpAddress
) : IRequest<ReAuthenticateResult>;

/// <summary>
/// Result of a re-authentication attempt. Contains only a success boolean
/// to avoid leaking information about failure causes.
/// </summary>
public record ReAuthenticateResult(bool Success);

public class ReAuthenticateCommandHandler : IRequestHandler<ReAuthenticateCommand, ReAuthenticateResult>
{
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

        // Reject passwords exceeding 128 characters without performing hash verification
        if (!string.IsNullOrEmpty(request.Password) && request.Password.Length > 128)
        {
            await LogAttempt(request, method, success: false, ct);
            return new ReAuthenticateResult(false);
        }

        // Load user from DB
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, ct);

        if (user == null)
        {
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
            await LogAttempt(request, method, success: false, ct);
            return new ReAuthenticateResult(false);
        }

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

    private async Task LogAttempt(ReAuthenticateCommand request, string method, bool success, CancellationToken ct)
    {
        var afterJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            method,
            success
        });

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
