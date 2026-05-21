namespace Jobuler.Application.Common.HealthChecks;

/// <summary>
/// Configuration options for the health check monitoring and alerting system.
/// Bound from environment variables via IOptions&lt;HealthCheckOptions&gt;.
/// </summary>
public class HealthCheckOptions
{
    /// <summary>
    /// Pushover user key for receiving notifications.
    /// </summary>
    public string? PushoverUserKey { get; set; }

    /// <summary>
    /// Pushover application API token for sending notifications.
    /// </summary>
    public string? PushoverAppToken { get; set; }

    /// <summary>
    /// Interval in seconds between health check cycles. Defaults to 300 (5 minutes).
    /// Values below 30 are clamped to 30.
    /// </summary>
    public int IntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Cooldown in seconds between duplicate alerts for the same service. Defaults to 3600 (1 hour).
    /// </summary>
    public int AlertCooldownSeconds { get; set; } = 3600;
}
