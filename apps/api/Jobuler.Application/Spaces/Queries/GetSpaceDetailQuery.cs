using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Queries;

public record GetSpaceDetailQuery(Guid SpaceId, Guid RequestingUserId) : IRequest<SpaceDetailDto?>;

public record SpaceDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string Locale,
    bool IsActive,
    string? InviteCode,
    int MemberCount,
    int GroupCount,
    bool IsOwner,
    DateTime CreatedAt,
    int ManagementTimeoutMinutes);

public class GetSpaceDetailQueryHandler : IRequestHandler<GetSpaceDetailQuery, SpaceDetailDto?>
{
    private readonly AppDbContext _db;

    public GetSpaceDetailQueryHandler(AppDbContext db) => _db = db;

    public async Task<SpaceDetailDto?> Handle(GetSpaceDetailQuery request, CancellationToken ct)
    {
        var space = await _db.Spaces
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SpaceId && s.IsActive && s.DeletedAt == null, ct);

        if (space is null) return null;

        var isOwner = space.OwnerUserId == request.RequestingUserId;

        var memberCount = await _db.SpaceMemberships
            .CountAsync(m => m.SpaceId == request.SpaceId && m.IsActive, ct);

        var groupCount = await _db.Groups
            .CountAsync(g => g.SpaceId == request.SpaceId && g.DeletedAt == null, ct);

        return new SpaceDetailDto(
            space.Id,
            space.Name,
            space.Description,
            space.Locale,
            space.IsActive,
            isOwner ? space.InviteCode : null, // Only show invite code to owner
            memberCount,
            groupCount,
            isOwner,
            space.CreatedAt,
            space.ManagementTimeoutMinutes);
    }
}
