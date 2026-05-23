using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Queries;

public record GetSpaceMembersQuery(Guid SpaceId) : IRequest<List<SpaceMemberDto>>;

public record SpaceMemberDto(Guid UserId, string? DisplayName, string? Email, DateTime JoinedAt);

public class GetSpaceMembersQueryHandler : IRequestHandler<GetSpaceMembersQuery, List<SpaceMemberDto>>
{
    private readonly AppDbContext _db;

    public GetSpaceMembersQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<SpaceMemberDto>> Handle(GetSpaceMembersQuery request, CancellationToken ct)
    {
        var memberships = await _db.SpaceMemberships
            .AsNoTracking()
            .Where(m => m.SpaceId == request.SpaceId && m.IsActive)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(ct);

        var userIds = memberships.Select(m => m.UserId).ToList();
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        return memberships.Select(m =>
        {
            users.TryGetValue(m.UserId, out var user);
            return new SpaceMemberDto(
                m.UserId,
                user?.DisplayName,
                user?.Email,
                m.JoinedAt);
        }).ToList();
    }
}
