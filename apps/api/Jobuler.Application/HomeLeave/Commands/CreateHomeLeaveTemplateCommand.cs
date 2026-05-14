using Jobuler.Application.Common;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Commands;

public record CreateHomeLeaveTemplateCommand(
    Guid SpaceId,
    string Name,
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours,
    Guid RequestingUserId) : IRequest<Guid>;

public class CreateHomeLeaveTemplateCommandHandler : IRequestHandler<CreateHomeLeaveTemplateCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public CreateHomeLeaveTemplateCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreateHomeLeaveTemplateCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.ConstraintsManage, ct);

        var trimmedName = req.Name.Trim();

        // Check for duplicate name within the same space
        var duplicateExists = await _db.HomeLeaveTemplates.AsNoTracking()
            .AnyAsync(t => t.SpaceId == req.SpaceId && t.Name == trimmedName, ct);

        if (duplicateExists)
            throw new ConflictException("Template name already exists in this space.");

        var template = HomeLeaveTemplate.Create(
            req.SpaceId,
            trimmedName,
            req.MinRestHours,
            req.EligibilityThresholdHours,
            req.LeaveCapacity,
            req.LeaveDurationHours);

        _db.HomeLeaveTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return template.Id;
    }
}
