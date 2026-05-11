namespace Jobuler.Infrastructure.Notifications;

/// <summary>
/// VAPID (Voluntary Application Server Identification) configuration
/// for Web Push notifications. Loaded from environment variables.
/// </summary>
public class VapidSettings
{
    /// <summary>
    /// The VAPID public key (Base64URL-encoded).
    /// Used by both frontend (applicationServerKey) and backend (JWT signing).
    /// Environment variable: VAPID_PUBLIC_KEY
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// The VAPID private key (Base64URL-encoded).
    /// Used to sign VAPID JWTs per RFC 8292. Backend only.
    /// Environment variable: VAPID_PRIVATE_KEY
    /// </summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Contact URI for the application server (mailto: or https://).
    /// Included in the VAPID JWT so push services can contact the operator.
    /// Environment variable: VAPID_SUBJECT
    /// </summary>
    public string Subject { get; set; } = string.Empty;
}
