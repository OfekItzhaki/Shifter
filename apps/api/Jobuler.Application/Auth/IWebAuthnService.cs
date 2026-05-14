namespace Jobuler.Application.Auth;

/// <summary>
/// Result of generating WebAuthn registration options.
/// </summary>
public record CredentialCreateOptionsResult(string OptionsJson, string ChallengeId);

/// <summary>
/// Result of completing WebAuthn registration (verified credential data).
/// </summary>
public record RegisteredCredentialResult(
    byte[] CredentialId,
    byte[] PublicKey,
    uint SignCount,
    string[] Transports);

/// <summary>
/// Result of generating WebAuthn authentication options.
/// </summary>
public record AssertionOptionsResult(string OptionsJson, string ChallengeId);

/// <summary>
/// Result of completing WebAuthn authentication (verified assertion).
/// </summary>
public record AssertionVerificationResult(uint NewSignCount, byte[] UserHandle);

/// <summary>
/// Contract for WebAuthn/FIDO2 ceremony operations.
/// Defined in Application so handlers can depend on it without referencing Infrastructure.
/// Implemented by Fido2Service in Infrastructure.
/// </summary>
public interface IWebAuthnService
{
    /// <summary>
    /// Generate options for navigator.credentials.create().
    /// </summary>
    Task<CredentialCreateOptionsResult> GenerateRegistrationOptionsAsync(
        Guid userId, string userEmail, string userDisplayName,
        IEnumerable<byte[]> existingCredentialIds, CancellationToken ct);

    /// <summary>
    /// Verify the attestation response and return the credential data.
    /// </summary>
    Task<RegisteredCredentialResult> CompleteRegistrationAsync(
        string challengeId, string attestationResponseJson, CancellationToken ct);

    /// <summary>
    /// Generate options for navigator.credentials.get().
    /// </summary>
    Task<AssertionOptionsResult> GenerateAuthenticationOptionsAsync(CancellationToken ct);

    /// <summary>
    /// Verify the assertion response against stored credential.
    /// </summary>
    Task<AssertionVerificationResult> CompleteAuthenticationAsync(
        string challengeId, string assertionResponseJson,
        byte[] storedPublicKey, uint storedSignCount, CancellationToken ct);
}
