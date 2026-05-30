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
        // Get the space owner ID to ensure they always show as SpaceOwner
        var space = await _db.Spaces
            .AsNoTracking()
            .Where(s => s.Id == request.SpaceId)
            .Select(s => new { s.OwnerUserId })
            .FirstOrDefaultAsync(ct);

        var members = await _db.SpaceMemberships
            .AsNoTracking()
            .Where(m => m.SpaceId == request.SpaceId && m.IsActive)
            .Select(m => new SpacePermissionLevelDto(m.UserId, m.PermissionLevel))
            .ToListAsync(ct);

        // Override: space owner always has SpaceOwner level regardless of what's in the membership table
        if (space != null)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].UserId == space.OwnerUserId && members[i].PermissionLevel != SpacePermissionLevel.SpaceOwner)
                {
                    members[i] = new SpacePermissionLevelDto(members[i].UserId, SpacePermissionLevel.SpaceOwner);
                }
            }
        }

        return members;
    }
}
