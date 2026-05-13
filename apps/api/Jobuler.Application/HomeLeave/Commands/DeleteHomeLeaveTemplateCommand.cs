using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Commands;

public record DeleteHomeLeaveTemplateCommand(
    Guid SpaceId,
    Guid TemplateId,
    Guid RequestingUserId) : IRequest;

public class DeleteHomeLeaveTemplateCommandHandler : IRequestHandler<DeleteHomeLeaveTemplateCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public DeleteHomeLeaveTemplateCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(DeleteHomeLeaveTemplateCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.ConstraintsManage, ct);

        var template = await _db.HomeLeaveTemplates
            .FirstOrDefaultAsync(t => t.Id == req.TemplateId && t.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Template not found.");

        _db.HomeLeaveTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
    }
}
