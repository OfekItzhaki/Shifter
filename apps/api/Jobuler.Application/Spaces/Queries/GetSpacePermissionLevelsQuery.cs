using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Queries;

public record GetSpacePermissionLevelsQuery(Guid SpaceId) : IRequest<List<SpacePermissionLevelDto>>;

public record SpacePermissionLevelDto(Guid UserId, SpacePermissionLevel PermissionLevel);

public class GetSpacePermissionLevelsQueryHandler : IRequestHandler<GetSpacePermissionLevelsQuery, List<SpacePermissionLevelDto>>
{
    private readonly AppDbContext _db;

    public GetSpacePermissionLevelsQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<SpacePermissionLevelDto>> Handle(GetSpacePermissionLevelsQuery request, CancellationToken ct)
    {
        return await _db.SpaceMemberships
            .AsNoTracking()
            .Where(m => m.SpaceId == request.SpaceId && m.IsActive)
            .Select(m => new SpacePermissionLevelDto(m.UserId, m.PermissionLevel))
            .ToListAsync(ct);
    }
}
