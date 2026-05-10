using Jobuler.Domain.Common;

namespace Jobuler.Domain.Logs;

/// <summary>
/// Technical/operational event log. Append-only — never updated or deleted.
/// </summary>
public class SystemLog : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public string Severity { get; private set; } = "info";   // info|warning|error|critical
    public string EventType { get; private set; } = default!;
    public string Message { get; private set; } = default!;
    public string? DetailsJson { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public bool IsSensitive { get; private set; }

    private SystemLog() { }

    public static SystemLog Create(
        Guid spaceId, string severity, string eventType, string message,
        string? detailsJson = null, Guid? actorUserId = null,
        Guid? correlationId = null, bool isSensitive = false) =>
        new()
        {
            SpaceId = spaceId,
            Severity = severity,
            EventType = eventType,
            Message = message,
            DetailsJson = detailsJson,
            ActorUserId = actorUserId,
            CorrelationId = correlationId,
            IsSensitive = isSensitive
        };
}
