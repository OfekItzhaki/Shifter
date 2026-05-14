using System.Text.Json;
using FluentValidation;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Jobuler.Application.Auth.Commands;

/// <summary>
/// Completes WebAuthn authentication by verifying the assertion response,
/// updating the credential sign count, and issuing JWT tokens.
/// Returns the same token format as the email+password login flow.
/// </summary>
public record WebAuthnLoginCompleteCommand(
    string ChallengeId,
    string AssertionResponseJson) : IRequest<LoginResult>;

public class WebAuthnLoginCompleteCommandValidator : AbstractValidator<WebAuthnLoginCompleteCommand>
{
    public WebAuthnLoginCompleteCommandValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.AssertionResponseJson).NotEmpty();
    }
}

public class WebAuthnLoginCompleteCommandHandler
    : IRequestHandler<WebAuthnLoginCompleteCommand, LoginResult>
{
    private readonly AppDbContext _db;
    private readonly IWebAuthnService _webAuthn;
    private readonly IJwtService _jwt;
    private readonly int _refreshTokenExpiryDays;

    public WebAuthnLoginCompleteCommandHandler(
        AppDbContext db,
        IWebAuthnService webAuthn,
        IJwtService jwt,
        IConfiguration config)
    {
        _db = db;
        _webAuthn = webAuthn;
        _jwt = jwt;
        _refreshTokenExpiryDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "7");
    }

    public async Task<LoginResult> Handle(WebAuthnLoginCompleteCommand request, CancellationToken ct)
    {
        // Extract the credential ID from the assertion response to look up the credential
        var credentialId = ExtractCredentialIdFromAssertion(request.AssertionResponseJson);

        // Look up the credential in the database
        var credential = await _db.WebAuthnCredentials
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId, ct)
            ?? throw new KeyNotFoundException("Credential not found.");

        if (credential.IsDisabled)
            throw new InvalidOperationException("Credential has been disabled.");

        if (!credential.User.IsActive)
            throw new UnauthorizedAccessException("User account is inactive.");

        // Verify the assertion against the stored public key and sign count
        var verificationResult = await _webAuthn.CompleteAuthenticationAsync(
            request.ChallengeId,
            request.AssertionResponseJson,
            credential.PublicKey,
            credential.SignCount,
            ct);

        // Update sign count (throws if regression detected)
        credential.UpdateSignCount(verificationResult.NewSignCount);

        // Record login on the user entity
        credential.User.RecordLogin();

        // Issue tokens (same as email+password login)
        var rawRefresh = _jwt.GenerateRefreshTokenRaw();
        var tokenHash = _jwt.HashToken(rawRefresh);
        var refreshToken = RefreshToken.Create(credential.UserId, tokenHash, _refreshTokenExpiryDays);

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        var accessToken = _jwt.GenerateAccessToken(
            credential.User.Id,
            credential.User.Email,
            credential.User.DisplayName);
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        return new LoginResult(
            accessToken,
            rawRefresh,
            expiresAt,
            credential.User.Id,
            credential.User.DisplayName,
            credential.User.PreferredLocale,
            credential.User.IsPlatformAdmin);
    }

    /// <summary>
    /// Extracts the raw credential ID bytes from the assertion response JSON.
    /// The assertion response contains an "id" field which is the base64url-encoded credential ID.
    /// </summary>
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
