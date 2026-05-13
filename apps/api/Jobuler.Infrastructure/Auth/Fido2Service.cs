using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Jobuler.Application.Auth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Jobuler.Infrastructure.Auth;

/// <summary>
/// Challenge data stored in memory cache during WebAuthn ceremonies.
/// Stores the full options JSON so it can be reconstructed for verification.
/// </summary>
internal record StoredChallenge(
    string OptionsJson,
    Guid? UserId,
    DateTime CreatedAt);

/// <summary>
/// Implements IWebAuthnService using Fido2NetLib for all cryptographic operations.
/// Challenges are stored in IMemoryCache with a 5-minute TTL and are single-use.
/// </summary>
public class Fido2Service : IWebAuthnService
{
    private readonly IFido2 _fido2;
    private readonly IMemoryCache _cache;
    private readonly int _challengeTimeoutMinutes;

    private const string CacheKeyPrefix = "webauthn:challenge:";

    public Fido2Service(IFido2 fido2, IMemoryCache cache, IConfiguration configuration)
    {
        _fido2 = fido2;
        _cache = cache;
        _challengeTimeoutMinutes = int.Parse(
            configuration["WebAuthn:ChallengeTimeoutMinutes"] ?? "5");
    }

    public Task<CredentialCreateOptionsResult> GenerateRegistrationOptionsAsync(
        Guid userId, string userEmail, string userDisplayName,
        IEnumerable<byte[]> existingCredentialIds, CancellationToken ct)
    {
        var user = new Fido2User
        {
            Id = userId.ToByteArray(),
            Name = userEmail,
            DisplayName = userDisplayName
        };

        var excludeCredentials = existingCredentialIds
            .Select(id => new PublicKeyCredentialDescriptor(id))
            .ToList();

        var authenticatorSelection = new AuthenticatorSelection
        {
            AuthenticatorAttachment = AuthenticatorAttachment.Platform,
            UserVerification = UserVerificationRequirement.Required,
            RequireResidentKey = false
        };

        var options = _fido2.RequestNewCredential(
            user,
            excludeCredentials,
            authenticatorSelection,
            AttestationConveyancePreference.None);

        // Store the full options JSON for later verification
        var challengeId = Guid.NewGuid().ToString();
        var optionsJson = options.ToJson();

        var storedChallenge = new StoredChallenge(
            optionsJson,
            userId,
            DateTime.UtcNow);

        _cache.Set(
            CacheKeyPrefix + challengeId,
            storedChallenge,
            TimeSpan.FromMinutes(_challengeTimeoutMinutes));

        return Task.FromResult(new CredentialCreateOptionsResult(optionsJson, challengeId));
    }

    public async Task<RegisteredCredentialResult> CompleteRegistrationAsync(
        string challengeId, string attestationResponseJson, CancellationToken ct)
    {
        // Retrieve and delete challenge (single-use)
        var cacheKey = CacheKeyPrefix + challengeId;
        if (!_cache.TryGetValue(cacheKey, out StoredChallenge? storedChallenge) || storedChallenge is null)
        {
            throw new KeyNotFoundException("Challenge not found or expired.");
        }
        _cache.Remove(cacheKey);

        // Deserialize the attestation response
        var attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationResponseJson)
            ?? throw new InvalidOperationException("Invalid attestation response format.");

        // Reconstruct the original options from stored JSON
        var options = CredentialCreateOptions.FromJson(storedChallenge.OptionsJson);

        // Verify the attestation
        var credentialMakeResult = await _fido2.MakeNewCredentialAsync(
            attestationResponse,
            options,
            IsCredentialIdUniqueToUserCallback,
            cancellationToken: ct);

        if (credentialMakeResult.Result is null)
            throw new InvalidOperationException("Attestation verification failed.");

        var result = credentialMakeResult.Result;

        return new RegisteredCredentialResult(
            result.CredentialId,
            result.PublicKey,
            result.Counter,
            Array.Empty<string>()); // Transports not directly available from v3.0.1 result
    }

    public Task<AssertionOptionsResult> GenerateAuthenticationOptionsAsync(CancellationToken ct)
    {
        // Empty allowed credentials = discoverable credential flow
        var allowedCredentials = new List<PublicKeyCredentialDescriptor>();

        var options = _fido2.GetAssertionOptions(
            allowedCredentials,
            UserVerificationRequirement.Required);

        // Store the full options JSON for later verification
        var challengeId = Guid.NewGuid().ToString();
        var optionsJson = options.ToJson();

        var storedChallenge = new StoredChallenge(
            optionsJson,
            null, // No user ID for discoverable credential flow
            DateTime.UtcNow);

        _cache.Set(
            CacheKeyPrefix + challengeId,
            storedChallenge,
            TimeSpan.FromMinutes(_challengeTimeoutMinutes));

        return Task.FromResult(new AssertionOptionsResult(optionsJson, challengeId));
    }

    public async Task<Application.Auth.AssertionVerificationResult> CompleteAuthenticationAsync(
        string challengeId, string assertionResponseJson,
        byte[] storedPublicKey, uint storedSignCount, CancellationToken ct)
    {
        // Retrieve and delete challenge (single-use)
        var cacheKey = CacheKeyPrefix + challengeId;
        if (!_cache.TryGetValue(cacheKey, out StoredChallenge? storedChallenge) || storedChallenge is null)
        {
            throw new KeyNotFoundException("Challenge not found or expired.");
        }
        _cache.Remove(cacheKey);

        // Deserialize the assertion response
        var assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertionResponseJson)
            ?? throw new InvalidOperationException("Invalid assertion response format.");

        // Reconstruct assertion options from stored JSON
        var options = AssertionOptions.FromJson(storedChallenge.OptionsJson);

        // Verify the assertion
        var result = await _fido2.MakeAssertionAsync(
            assertionResponse,
            options,
            storedPublicKey,
            storedSignCount,
            IsUserHandleOwnerOfCredentialIdCallback,
            cancellationToken: ct);

        return new Application.Auth.AssertionVerificationResult(
            result.Counter,
            assertionResponse.Response.UserHandle ?? Array.Empty<byte>());
    }

    /// <summary>
    /// Callback to check if a credential ID is unique to the user during registration.
    /// Always returns true — uniqueness is enforced by the DB unique index.
    /// </summary>
    private static Task<bool> IsCredentialIdUniqueToUserCallback(
        IsCredentialIdUniqueToUserParams args, CancellationToken ct)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Callback to verify user handle ownership during assertion.
    /// Always returns true — ownership is verified by the command handler after this call.
    /// </summary>
    private static Task<bool> IsUserHandleOwnerOfCredentialIdCallback(
        IsUserHandleOwnerOfCredentialIdParams args, CancellationToken ct)
    {
        return Task.FromResult(true);
    }
}
