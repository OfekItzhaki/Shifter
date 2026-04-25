using Jobuler.Domain.Common;
using System.Security.Cryptography;

namespace Jobuler.Domain.People;

/// <summary>
/// Tracks a pending invitation sent to a person who hasn't yet linked their account.
/// The token is stored hashed; only the raw token is sent to the recipient.
/// </summary>
public class PendingInvitation : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid PersonId { get; private set; }
    public string Contact { get; private set; } = default!;   // email or phone
    public string Channel { get; private set; } = default!;   // "email" | "whatsapp"
    public string TokenHash { get; private set; } = default!; // SHA-256 of raw token
    public bool IsAccepted { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public Guid InvitedByUserId { get; private set; }

    private PendingInvitation() { }

    /// <summary>Creates a new invitation and returns both the entity and the raw token.</summary>
    public static (PendingInvitation invitation, string rawToken) Create(
        Guid spaceId, Guid personId, string contact, string channel, Guid invitedByUserId)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var hash = Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        var invitation = new PendingInvitation
        {
            SpaceId = spaceId,
            PersonId = personId,
            Contact = contact.Trim(),
            Channel = channel.ToLowerInvariant(),
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            InvitedByUserId = invitedByUserId
        };

        return (invitation, rawToken);
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public void Accept() { IsAccepted = true; }

    public static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
