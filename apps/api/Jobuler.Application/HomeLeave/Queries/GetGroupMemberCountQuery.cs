using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Queries;

/// <summary>
/// Returns the number of active members in a group within a space.
/// Used by the controller to compute optimal ratio and feasibility.
/// </summary>
public record GetGroupMemberCountQuery(Guid SpaceId, Guid GroupId) : IRequest<int>;

public class GetGroupMemberCountQueryHandler : IRequestHandler<GetGroupMemberCountQuery, int>
{
    private readonly AppDbContext _db;

    public GetGroupMemberCountQueryHandler(AppDbContext db) => _db = db;

    public async Task<int> Handle(GetGroupMemberCountQuery req, CancellationToken ct)
    {
        return await _db.GroupMemberships.AsNoTracking()
            .CountAsync(m => m.GroupId == req.GroupId && m.SpaceId == req.SpaceId, ct);
    }
}
