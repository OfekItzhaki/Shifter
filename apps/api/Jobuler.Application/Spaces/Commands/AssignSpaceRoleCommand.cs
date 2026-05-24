using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record AssignSpaceRoleCommand(
    Guid SpaceId,
    Guid TargetUserId,
    SpacePermissionLevel Level,
    Guid ActorUserId) : IRequest;

public class AssignSpaceRoleCommandHandler : IRequestHandler<AssignSpaceRoleCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogger _audit;

    public AssignSpaceRoleCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IAuditLogger audit)
    {
        _db = db;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task Handle(AssignSpaceRoleCommand request, CancellationToken ct)
    {
        // ── Permission check — caller must have permissions.manage ────────────
        await _permissions.RequirePermissionAsync(
            request.ActorUserId, request.SpaceId, Permissions.PermissionsManage, ct);

        // ── Load target membership ───────────────────────────────────────────
        var membership = await _db.SpaceMemberships
            .FirstOrDefaultAsync(m =>
                m.SpaceId == request.SpaceId &&
                m.UserId == request.TargetUserId &&
                m.IsActive, ct)
            ?? throw new KeyNotFoundException("Space membership not found.");

        // ── Record previous level for audit ──────────────────────────────────
        var previousLevel = membership.PermissionLevel;

        // ── Assign new permission level ──────────────────────────────────────
        membership.SetPermissionLevel(request.Level);

        // ── Persist changes ──────────────────────────────────────────────────
        await _db.SaveChangesAsync(ct);

        // ── Audit log ────────────────────────────────────────────────────────
        await _audit.LogAsync(
            request.SpaceId,
            request.ActorUserId,
            "space.role_assign",
            entityType: "space_membership",
            entityId: membership.Id,
            beforeJson: System.Text.Json.JsonSerializer.Serialize(new { PermissionLevel = previousLevel.ToString() }),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { PermissionLevel = request.Level.ToString() }),
            ct: ct);
    }
}
