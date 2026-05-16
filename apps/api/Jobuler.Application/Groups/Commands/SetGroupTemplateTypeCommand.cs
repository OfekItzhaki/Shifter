using Jobuler.Application.Common;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

public record SetGroupTemplateTypeCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId,
    GroupTemplateType TemplateType) : IRequest;

public class SetGroupTemplateTypeCommandHandler : IRequestHandler<SetGroupTemplateTypeCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public SetGroupTemplateTypeCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(SetGroupTemplateTypeCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.PeopleManage, ct);

        var group = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct);

        if (group is null)
            throw new KeyNotFoundException("Group not found.");

        group.SetTemplateType(req.TemplateType);
        await _db.SaveChangesAsync(ct);
    }
}
