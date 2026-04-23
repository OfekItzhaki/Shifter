using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Queries;

public record GroupTypeDto(Guid Id, string Name, string? Description, bool IsActive);
public record GroupDto(Guid Id, Guid? GroupTypeId, string? GroupTypeName, string Name, string? Description, bool IsActive, int MemberCount, int SolverHorizonDays);
public record GroupMemberDto(Guid PersonId, string FullName, string? DisplayName);

public record GetGroupTypesQuery(Guid SpaceId) : IRequest<List<GroupTypeDto>>;

public class GetGroupTypesQueryHandler : IRequestHandler<GetGroupTypesQuery, List<GroupTypeDto>>
{
    private readonly AppDbContext _db;
    public GetGroupTypesQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<GroupTypeDto>> Handle(GetGroupTypesQuery req, CancellationToken ct) =>
        await _db.GroupTypes.AsNoTracking()
            .Where(g => g.SpaceId == req.SpaceId && g.IsActive)
            .OrderBy(g => g.Name)
            .Select(g => new GroupTypeDto(g.Id, g.Name, g.Description, g.IsActive))
            .ToListAsync(ct);
}

public record GetGroupsQuery(Guid SpaceId) : IRequest<List<GroupDto>>;

public class GetGroupsQueryHandler : IRequestHandler<GetGroupsQuery, List<GroupDto>>
{
    private readonly AppDbContext _db;
    public GetGroupsQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<GroupDto>> Handle(GetGroupsQuery req, CancellationToken ct)
    {
        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.SpaceId == req.SpaceId && g.IsActive)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

        // Fetch type names only for groups that have a type (left-join equivalent)
        var typeIds = groups.Where(g => g.GroupTypeId.HasValue)
            .Select(g => g.GroupTypeId!.Value).Distinct().ToList();

        var typeNames = typeIds.Any()
            ? await _db.GroupTypes.AsNoTracking()
                .Where(t => typeIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, ct)
            : new Dictionary<Guid, string>();

        var memberCounts = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.SpaceId == req.SpaceId)
            .GroupBy(m => m.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, ct);

        return groups.Select(g => new GroupDto(
            g.Id,
            g.GroupTypeId,
            g.GroupTypeId.HasValue ? typeNames.GetValueOrDefault(g.GroupTypeId.Value) : null,
            g.Name,
            g.Description,
            g.IsActive,
            memberCounts.GetValueOrDefault(g.Id, 0),
            g.SolverHorizonDays
        )).ToList();
    }
}

public record GetGroupMembersQuery(Guid SpaceId, Guid GroupId) : IRequest<List<GroupMemberDto>>;

public class GetGroupMembersQueryHandler : IRequestHandler<GetGroupMembersQuery, List<GroupMemberDto>>
{
    private readonly AppDbContext _db;
    public GetGroupMembersQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<GroupMemberDto>> Handle(GetGroupMembersQuery req, CancellationToken ct) =>
        await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.GroupId == req.GroupId && m.SpaceId == req.SpaceId)
            .Join(_db.People, m => m.PersonId, p => p.Id,
                (m, p) => new { p.Id, p.FullName, p.DisplayName })
            .OrderBy(p => p.FullName)
            .Select(p => new GroupMemberDto(p.Id, p.FullName, p.DisplayName))
            .ToListAsync(ct);
}
