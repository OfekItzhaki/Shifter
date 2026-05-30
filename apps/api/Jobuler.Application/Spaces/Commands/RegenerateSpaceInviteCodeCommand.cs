using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record RegenerateSpaceInviteCodeCommand(Guid SpaceId, Guid RequestingUserId) : IRequest<string>;

public class RegenerateSpaceInviteCodeCommandHandler : IRequestHandler<RegenerateSpaceInviteCodeCommand, string>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public RegenerateSpaceInviteCodeCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<string> Handle(RegenerateSpaceInviteCodeCommand request, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(request.RequestingUserId, request.SpaceId, Permissions.OwnershipTransfer, ct);

        var space = await _db.Spaces.FirstOrDefaultAsync(s => s.Id == request.SpaceId, ct)
            ?? throw new KeyNotFoundException("Space not found.");

        var newCode = space.RegenerateInviteCode();
        await _db.SaveChangesAsync(ct);

        return newCode;
    }
}
