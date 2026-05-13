using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

public record SetGroupClosedBaseCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId,
    bool IsClosedBase) : IRequest;

public class SetGroupClosedBaseCommandHandler : IRequestHandler<SetGroupClosedBaseCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public SetGroupClosedBaseCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(SetGroupClosedBaseCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.ConstraintsManage, ct);

        var group = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct);

        if (group is null)
            throw new KeyNotFoundException("Group not found.");

        group.SetClosedBase(req.IsClosedBase);
        await _db.SaveChangesAsync(ct);
    }
}
