using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record UnlinkParentGroupCommand(
    Guid SpaceId,
    Guid ChildGroupId,
    Guid RequestingUserId) : IRequest;

public class UnlinkParentGroupCommandHandler : IRequestHandler<UnlinkParentGroupCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public UnlinkParentGroupCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(UnlinkParentGroupCommand request, CancellationToken ct)
    {
        // ── Permission check ─────────────────────────────────────────────────
        await _permissions.RequirePermissionAsync(
            request.RequestingUserId, request.SpaceId, Permissions.SpaceAdminMode, ct);

        // ── Load child group ─────────────────────────────────────────────────
        var childGroup = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == request.ChildGroupId && g.SpaceId == request.SpaceId && g.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Group not found in this space.");

        // ── Unlink from parent ───────────────────────────────────────────────
        childGroup.UnlinkFromParent();
        await _db.SaveChangesAsync(ct);
    }
}
