using Jobuler.Application.Groups.Commands;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Queries;

public record GetGroupRolesQuery(Guid SpaceId, Guid GroupId) : IRequest<List<GroupRoleDto>>;

public class GetGroupRolesQueryHandler : IRequestHandler<GetGroupRolesQuery, List<GroupRoleDto>>
{
    private readonly AppDbContext _db;
    public GetGroupRolesQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<GroupRoleDto>> Handle(GetGroupRolesQuery req, CancellationToken ct) =>
        await _db.SpaceRoles.AsNoTracking()
            .Where(r => r.SpaceId == req.SpaceId && r.GroupId == req.GroupId)
            .OrderBy(r => r.Name)
            .Select(r => new GroupRoleDto(r.Id, r.Name, r.Description, r.IsActive, r.PermissionLevel.ToString()))
            .ToListAsync(ct);
}
