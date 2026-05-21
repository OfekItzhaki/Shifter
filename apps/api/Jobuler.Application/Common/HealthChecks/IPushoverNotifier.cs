namespace Jobuler.Application.Common.HealthChecks;

/// <summary>
/// Sends push notifications to the platform operator via the Pushover API.
/// </summary>
public interface IPushoverNotifier
{
    /// <summary>
    /// Sends a high-priority alert indicating a service has become unhealthy.
    /// </summary>
    /// <param name="serviceName">The name of the unhealthy service.</param>
    /// <param name="detectedAtUtc">The UTC timestamp when the failure was detected.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAlertAsync(string serviceName, DateTime detectedAtUtc, CancellationToken ct);
}
