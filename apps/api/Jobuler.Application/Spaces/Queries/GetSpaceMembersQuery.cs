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
        var members = await _db.SpaceMemberships
            .AsNoTracking()
            .Where(m => m.SpaceId == request.SpaceId && m.IsActive)
            .Join(_db.Users, m => m.UserId, u => u.Id, (m, u) => new SpaceMemberDto(
                u.Id,
                u.DisplayName,
                u.Email,
                m.JoinedAt))
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(ct);

        return members;
    }
}
