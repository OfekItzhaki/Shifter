using Jobuler.Domain.Common;

namespace Jobuler.Domain.Logs;

/// <summary>
/// Immutable audit trail entry. Records who did what, when, against which entity.
/// Append-only — never updated or deleted.
/// </summary>
public class AuditLog : Entity
{
    public Guid? SpaceId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = default!;
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public string? IpAddress { get; private set; }
    public Guid? CorrelationId { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        Guid? spaceId, Guid? actorUserId, string action,
        string? entityType = null, Guid? entityId = null,
        string? beforeJson = null, string? afterJson = null,
        string? ipAddress = null, Guid? correlationId = null) =>
        new()
        {
            SpaceId = spaceId,
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            IpAddress = ipAddress,
            CorrelationId = correlationId
        };
}
