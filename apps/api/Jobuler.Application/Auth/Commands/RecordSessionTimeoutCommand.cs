using Jobuler.Application.Common;
using MediatR;

namespace Jobuler.Application.Auth.Commands;

/// <summary>
/// Records a session timeout event in the audit log when an elevated mode
/// (management or platform) is terminated due to inactivity.
/// </summary>
public record RecordSessionTimeoutCommand(
    Guid UserId,
    Guid? SpaceId,
    string Mode // "management" | "platform"
) : IRequest;

public class RecordSessionTimeoutCommandHandler : IRequestHandler<RecordSessionTimeoutCommand>
{
    private readonly IAuditLogger _audit;

    public RecordSessionTimeoutCommandHandler(IAuditLogger audit)
    {
        _audit = audit;
    }

    public async Task Handle(RecordSessionTimeoutCommand request, CancellationToken ct)
    {
        await _audit.LogAsync(
            spaceId: request.SpaceId,
            actorUserId: request.UserId,
            action: "session_timeout",
            entityType: "session",
            entityId: null,
            beforeJson: null,
            afterJson: $"{{\"mode\":\"{request.Mode}\"}}",
            ipAddress: null,
            ct: ct);
    }
}
