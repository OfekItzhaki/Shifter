using Jobuler.Domain.Common;

namespace Jobuler.Domain.Identity;

public class WebAuthnCredential : Entity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = default!;
    public byte[] CredentialId { get; private set; } = default!;
    public byte[] PublicKey { get; private set; } = default!;
    public uint SignCount { get; private set; }
    public string[] Transports { get; private set; } = Array.Empty<string>();
    public string? Nickname { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public bool IsDisabled { get; private set; }

    private WebAuthnCredential() { }

    public static WebAuthnCredential Create(
        Guid userId, byte[] credentialId, byte[] publicKey,
        uint signCount, string[] transports, string? nickname)
    {
        ArgumentNullException.ThrowIfNull(credentialId);
        ArgumentNullException.ThrowIfNull(publicKey);
        if (credentialId.Length == 0) throw new ArgumentException("Credential ID cannot be empty.");
        if (publicKey.Length == 0) throw new ArgumentException("Public key cannot be empty.");

        if (nickname?.Length > 100)
            throw new ArgumentException("Nickname must be 100 characters or fewer.");

        return new WebAuthnCredential
        {
            UserId = userId,
            CredentialId = credentialId,
            PublicKey = publicKey,
            SignCount = signCount,
            Transports = transports,
            Nickname = nickname,
            IsDisabled = false
        };
    }

    public void UpdateSignCount(uint newSignCount)
    {
        if (newSignCount <= SignCount)
        {
            IsDisabled = true;
            throw new InvalidOperationException("Sign count regression detected — credential may be cloned.");
        }

        SignCount = newSignCount;
        LastUsedAt = DateTime.UtcNow;
    }

    public void UpdateNickname(string? nickname)
    {
        if (nickname?.Length > 100)
            throw new ArgumentException("Nickname must be 100 characters or fewer.");
        Nickname = nickname;
    }

    public void Disable() => IsDisabled = true;
}
