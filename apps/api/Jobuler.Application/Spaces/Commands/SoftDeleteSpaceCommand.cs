using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record SoftDeleteSpaceCommand(
    Guid SpaceId,
    Guid ActorUserId) : IRequest;

public class SoftDeleteSpaceCommandHandler : IRequestHandler<SoftDeleteSpaceCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogger _audit;

    public SoftDeleteSpaceCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IAuditLogger audit)
    {
        _db = db;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task Handle(SoftDeleteSpaceCommand request, CancellationToken ct)
    {
        // ── Permission check (owner-only action) ─────────────────────────────
        await _permissions.RequirePermissionAsync(
            request.ActorUserId, request.SpaceId, Permissions.OwnershipTransfer, ct);

        // ── Load space ───────────────────────────────────────────────────────
        var space = await _db.Spaces
            .FirstOrDefaultAsync(s => s.Id == request.SpaceId, ct)
            ?? throw new KeyNotFoundException("Space not found.");

        // ── Soft-delete space ────────────────────────────────────────────────
        space.SoftDelete();

        // ── Cascade soft-delete to all groups in the space ───────────────────
        var groups = await _db.Groups
            .Where(g => g.SpaceId == request.SpaceId)
            .ToListAsync(ct);

        foreach (var group in groups)
        {
            group.SoftDeleteBySpace();
        }

        // ── Persist changes ──────────────────────────────────────────────────
        await _db.SaveChangesAsync(ct);

        // ── Audit log ────────────────────────────────────────────────────────
        await _audit.LogAsync(
            request.SpaceId,
            request.ActorUserId,
            "space.soft_delete",
            entityType: "space",
            entityId: request.SpaceId,
            beforeJson: System.Text.Json.JsonSerializer.Serialize(new { DeletedAt = (DateTime?)null }),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { DeletedAt = space.DeletedAt, CascadeDeletedGroups = groups.Count(g => g.DeletedBySpaceDeletion) }),
            ct: ct);
    }
}
