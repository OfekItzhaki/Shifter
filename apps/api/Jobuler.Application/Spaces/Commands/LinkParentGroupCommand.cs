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
        await _permissions.RequirePermissionAsync(
            request.RequestingUserId, request.SpaceId, Permissions.SpaceAdminMode, ct);

        var childGroup = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == request.ChildGroupId && g.SpaceId == request.SpaceId && g.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Child group not found in this space.");

        var parentGroup = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == request.ParentGroupId && g.SpaceId == request.SpaceId && g.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Parent group not found in this space.");

        if (childGroup.SpaceId != parentGroup.SpaceId)
            throw new InvalidOperationException("Both groups must belong to the same space.");

        if (request.ChildGroupId == request.ParentGroupId)
            throw new InvalidOperationException("A group cannot be its own parent.");

        await EnsureNoCircularHierarchyAsync(request.SpaceId, request.ChildGroupId, parentGroup.ParentGroupId, ct);

        childGroup.SetParentGroup(request.ParentGroupId);
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureNoCircularHierarchyAsync(
        Guid spaceId,
        Guid childGroupId,
        Guid? parentAncestorId,
        CancellationToken ct)
    {
        var visited = new HashSet<Guid> { childGroupId };
        var currentParentId = parentAncestorId;
        var depth = 0;

        while (currentParentId.HasValue)
        {
            if (!visited.Add(currentParentId.Value))
                throw new InvalidOperationException("Parent group link would create a circular hierarchy.");

            currentParentId = await _db.Groups.AsNoTracking()
                .Where(g => g.Id == currentParentId.Value && g.SpaceId == spaceId && g.DeletedAt == null)
                .Select(g => g.ParentGroupId)
                .FirstOrDefaultAsync(ct);

            depth++;
            if (depth > 50)
                throw new InvalidOperationException("Group hierarchy is too deep or circular.");
        }
    }
}
