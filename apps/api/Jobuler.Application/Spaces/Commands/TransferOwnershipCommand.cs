using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record TransferOwnershipCommand(
    Guid SpaceId,
    Guid NewOwnerUserId,
    Guid RequestingUserId,
    string? Reason) : IRequest;

public class TransferOwnershipCommandHandler : IRequestHandler<TransferOwnershipCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogger _audit;

    public TransferOwnershipCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IAuditLogger audit)
    {
        _db = db;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task Handle(TransferOwnershipCommand request, CancellationToken ct)
    {
        // ── Permission check — only Space Owner can transfer ─────────────────
        await _permissions.RequirePermissionAsync(
            request.RequestingUserId, request.SpaceId, Permissions.OwnershipTransfer, ct);

        // ── Load space ───────────────────────────────────────────────────────
        var space = await _db.Spaces.FirstOrDefaultAsync(s => s.Id == request.SpaceId, ct)
            ?? throw new KeyNotFoundException("Space not found.");

        // ── Validate: cannot transfer to yourself ────────────────────────────
        if (request.NewOwnerUserId == space.OwnerUserId)
            throw new InvalidOperationException("Cannot transfer ownership to yourself.");

        // ── Validate: target must be an active member of the space ───────────
        var targetMembership = await _db.SpaceMemberships
            .FirstOrDefaultAsync(m =>
                m.SpaceId == request.SpaceId &&
                m.UserId == request.NewOwnerUserId &&
                m.IsActive, ct);

        if (targetMembership == null)
            throw new InvalidOperationException("Target user is not an active member of this space.");

        // ── Record previous owner for audit ──────────────────────────────────
        var previousOwnerId = space.OwnerUserId;

        // ── Transfer ownership ───────────────────────────────────────────────
        space.TransferOwnership(request.NewOwnerUserId);

        // ── Grant all permission keys to new owner ───────────────────────────
        var allPermissionKeys = new[]
        {
            Permissions.SpaceView,
            Permissions.SpaceAdminMode,
            Permissions.PeopleManage,
            Permissions.ConstraintsManage,
            Permissions.RestrictionsManageSensitive,
            Permissions.TasksManage,
            Permissions.ScheduleRecalculate,
            Permissions.SchedulePublish,
            Permissions.ScheduleRollback,
            Permissions.PermissionsManage,
            Permissions.OwnershipTransfer,
            Permissions.LogsViewSensitive,
            Permissions.BillingManage
        };

        var existingGrants = await _db.SpacePermissionGrants
            .Where(g =>
                g.SpaceId == request.SpaceId &&
                g.UserId == request.NewOwnerUserId &&
                g.RevokedAt == null)
            .Select(g => g.PermissionKey)
            .ToListAsync(ct);

        foreach (var key in allPermissionKeys)
        {
            if (!existingGrants.Contains(key))
            {
                var grant = SpacePermissionGrant.Grant(
                    request.SpaceId,
                    request.NewOwnerUserId,
                    key,
                    request.RequestingUserId);
                _db.SpacePermissionGrants.Add(grant);
            }
        }

        // ── Create ownership transfer history record ─────────────────────────
        var history = OwnershipTransferHistory.Record(
            request.SpaceId,
            previousOwnerId,
            request.NewOwnerUserId,
            request.RequestingUserId,
            request.Reason);
        _db.OwnershipTransferHistory.Add(history);

        // ── Save changes ─────────────────────────────────────────────────────
        await _db.SaveChangesAsync(ct);

        // ── Audit log ────────────────────────────────────────────────────────
        await _audit.LogAsync(
            request.SpaceId,
            request.RequestingUserId,
            "space.ownership_transfer",
            entityType: "space",
            entityId: request.SpaceId,
            beforeJson: System.Text.Json.JsonSerializer.Serialize(new { OwnerUserId = previousOwnerId }),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { OwnerUserId = request.NewOwnerUserId }),
            ct: ct);
    }
}
