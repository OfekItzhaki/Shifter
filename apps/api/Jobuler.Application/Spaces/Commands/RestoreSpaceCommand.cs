using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record RestoreSpaceCommand(
    Guid SpaceId,
    Guid ActorUserId) : IRequest;

public class RestoreSpaceCommandHandler : IRequestHandler<RestoreSpaceCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogger _audit;

    public RestoreSpaceCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IAuditLogger audit)
    {
        _db = db;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task Handle(RestoreSpaceCommand request, CancellationToken ct)
    {
        // ── Permission check (owner-only action) ─────────────────────────────
        await _permissions.RequirePermissionAsync(
            request.ActorUserId, request.SpaceId, Permissions.OwnershipTransfer, ct);

        // ── Load space (include soft-deleted) ────────────────────────────────
        var space = await _db.Spaces
            .FirstOrDefaultAsync(s => s.Id == request.SpaceId, ct)
            ?? throw new KeyNotFoundException("Space not found.");

        // ── Guard: space must be in a deleted state ──────────────────────────
        if (space.DeletedAt == null)
            throw new InvalidOperationException("Space is not in a deleted state.");

        // ── Capture before-state for audit ───────────────────────────────────
        var deletedAt = space.DeletedAt;

        // ── Restore space ────────────────────────────────────────────────────
        space.Restore();

        // ── Restore groups that were cascade-deleted with the space ───────────
        var groups = await _db.Groups
            .Where(g => g.SpaceId == request.SpaceId)
            .ToListAsync(ct);

        foreach (var group in groups)
        {
            group.RestoreFromSpaceDeletion();
        }

        // ── Persist changes ──────────────────────────────────────────────────
        await _db.SaveChangesAsync(ct);

        // ── Audit log ────────────────────────────────────────────────────────
        await _audit.LogAsync(
            request.SpaceId,
            request.ActorUserId,
            "space.restore",
            entityType: "space",
            entityId: request.SpaceId,
            beforeJson: System.Text.Json.JsonSerializer.Serialize(new { DeletedAt = deletedAt }),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { DeletedAt = (DateTime?)null, RestoredGroups = groups.Count(g => !g.DeletedBySpaceDeletion && g.DeletedAt == null) }),
            ct: ct);
    }
}
