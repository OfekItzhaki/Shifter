using Jobuler.Application.Common.HealthChecks;
using Jobuler.Infrastructure.Notifications;
using Microsoft.Extensions.Options;

namespace Jobuler.Infrastructure.HealthChecks;

/// <summary>
/// Validates Web Push VAPID configuration without contacting push providers.
/// </summary>
public class PushHealthCheck : IServiceHealthCheck
{
    private readonly VapidSettings _settings;

    public PushHealthCheck(IOptions<VapidSettings> settings)
    {
        _settings = settings.Value;
    }

    public string ServiceName => "push";

    public Task<ServiceHealthResult> CheckAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var hasPublicKey = !string.IsNullOrWhiteSpace(_settings.PublicKey);
        var hasPrivateKey = !string.IsNullOrWhiteSpace(_settings.PrivateKey);
        var hasSubject = !string.IsNullOrWhiteSpace(_settings.Subject);

        if (!hasPublicKey && !hasPrivateKey && !hasSubject)
        {
            return Task.FromResult(new ServiceHealthResult(ServiceName, "skipped"));
        }

        if (hasPublicKey && hasPrivateKey && hasSubject)
        {
            return Task.FromResult(new ServiceHealthResult(ServiceName, "healthy"));
        }

        var missing = new List<string>();
        if (!hasPublicKey) missing.Add("VAPID_PUBLIC_KEY");
        if (!hasPrivateKey) missing.Add("VAPID_PRIVATE_KEY");
        if (!hasSubject) missing.Add("VAPID_SUBJECT");

        return Task.FromResult(new ServiceHealthResult(
            ServiceName,
            "unhealthy",
            $"Missing required VAPID configuration: {string.Join(", ", missing)}"));
    }
}
