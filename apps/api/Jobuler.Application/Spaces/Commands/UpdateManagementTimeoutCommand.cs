using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record UpdateManagementTimeoutCommand(
    Guid SpaceId,
    int Minutes,
    Guid UserId) : IRequest;

public class UpdateManagementTimeoutCommandHandler : IRequestHandler<UpdateManagementTimeoutCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public UpdateManagementTimeoutCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(UpdateManagementTimeoutCommand request, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(request.UserId, request.SpaceId, Permissions.OwnershipTransfer, ct);

        var space = await _db.Spaces.FirstOrDefaultAsync(s => s.Id == request.SpaceId, ct)
            ?? throw new KeyNotFoundException("Space not found.");

        space.SetManagementTimeout(request.Minutes);
        await _db.SaveChangesAsync(ct);
    }
}
