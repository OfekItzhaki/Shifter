using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record LinkParentGroupCommand(
    Guid SpaceId,
    Guid ChildGroupId,
    Guid ParentGroupId,
    Guid RequestingUserId) : IRequest;

public class LinkParentGroupCommandHandler : IRequestHandler<LinkParentGroupCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public LinkParentGroupCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(LinkParentGroupCommand request, CancellationToken ct)
    {
        // ── Permission check ─────────────────────────────────────────────────
        await _permissions.RequirePermissionAsync(
            request.RequestingUserId, request.SpaceId, Permissions.SpaceAdminMode, ct);

        // ── Load child group ─────────────────────────────────────────────────
        var childGroup = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == request.ChildGroupId && g.SpaceId == request.SpaceId && g.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Child group not found in this space.");

        // ── Load parent group ────────────────────────────────────────────────
        var parentGroup = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == request.ParentGroupId && g.SpaceId == request.SpaceId && g.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Parent group not found in this space.");

        // ── Validate same space ──────────────────────────────────────────────
        if (childGroup.SpaceId != parentGroup.SpaceId)
            throw new InvalidOperationException("Both groups must belong to the same space.");

        // ── Validate no circular reference (child cannot be itself) ──────────
        if (request.ChildGroupId == request.ParentGroupId)
            throw new InvalidOperationException("A group cannot be its own parent.");

        // ── Single-level hierarchy: parent cannot itself have a parent ───────
        if (parentGroup.ParentGroupId is not null)
            throw new InvalidOperationException("Only single-level hierarchy is allowed. The proposed parent group already has a parent.");

        // ── Single-level hierarchy: child cannot already be a parent of other groups ─
        var childIsParent = await _db.Groups
            .AnyAsync(g => g.ParentGroupId == request.ChildGroupId && g.DeletedAt == null, ct);
        if (childIsParent)
            throw new InvalidOperationException("A group that is a parent of other groups cannot itself become a child.");

        // ── Set parent ───────────────────────────────────────────────────────
        childGroup.SetParentGroup(request.ParentGroupId);
        await _db.SaveChangesAsync(ct);
    }
}
