using Jobuler.Domain.Common;

namespace Jobuler.Domain.Notifications;

public class PushSubscription : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Push service endpoint URL (unique per device+browser)</summary>
    public string Endpoint { get; private set; } = default!;

    /// <summary>Client public key for payload encryption (Base64URL)</summary>
    public string P256dh { get; private set; } = default!;

    /// <summary>Authentication secret for payload encryption (Base64URL)</summary>
    public string Auth { get; private set; } = default!;

    private PushSubscription() { }

    public static PushSubscription Create(
        Guid spaceId, Guid userId,
        string endpoint, string p256dh, string auth)
    {
        if (spaceId == Guid.Empty)
            throw new ArgumentException("SpaceId must not be empty.", nameof(spaceId));

        if (userId == Guid.Empty)
            throw new ArgumentException("UserId must not be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint must not be empty.", nameof(endpoint));

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Endpoint must be a valid HTTPS URL.", nameof(endpoint));

        if (string.IsNullOrWhiteSpace(p256dh))
            throw new ArgumentException("P256dh must not be empty.", nameof(p256dh));

        if (string.IsNullOrWhiteSpace(auth))
            throw new ArgumentException("Auth must not be empty.", nameof(auth));

        return new PushSubscription
        {
            SpaceId = spaceId,
            UserId = userId,
            Endpoint = endpoint,
            P256dh = p256dh,
            Auth = auth
        };
    }
}
