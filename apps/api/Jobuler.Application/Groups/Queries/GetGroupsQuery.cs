using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Queries;

public record GroupTypeDto(Guid Id, string Name, string? Description, bool IsActive);
public record GroupDto(Guid Id, Guid? GroupTypeId, string? GroupTypeName, string Name, string? Description, bool IsActive, int MemberCount, int SolverHorizonDays, Guid? OwnerPersonId, DateTime? SolverStartDateTime = null, bool AutoPublish = false, bool IsClosedBase = false);
public record GroupMemberDto(Guid PersonId, string FullName, string? DisplayName, bool IsOwner, string? PhoneNumber, string? InvitationStatus, string? ProfileImageUrl, string? Birthday, Guid? LinkedUserId = null, Guid? RoleId = null, string? RoleName = null, decimal HomeLeavePriority = 1.0m);

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
            .Where(g => g.SpaceId == req.SpaceId && g.IsActive && g.DeletedAt == null)
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

        var groupIds = groups.Select(g => g.Id).ToList();
        var ownerPersonIds = await _db.GroupMemberships.AsNoTracking()
            .Where(m => groupIds.Contains(m.GroupId) && m.IsOwner)
            .ToDictionaryAsync(m => m.GroupId, m => m.PersonId, ct);

        return groups.Select(g => new GroupDto(
            g.Id,
            g.GroupTypeId,
            g.GroupTypeId.HasValue ? typeNames.GetValueOrDefault(g.GroupTypeId.Value) : null,
            g.Name,
            g.Description,
            g.IsActive,
            memberCounts.GetValueOrDefault(g.Id, 0),
            g.SolverHorizonDays,
            ownerPersonIds.GetValueOrDefault(g.Id),
            g.SolverStartDateTime,
            g.AutoPublish,
            g.IsClosedBase
        )).ToList();
    }
}

public record GetGroupMembersQuery(Guid SpaceId, Guid GroupId) : IRequest<List<GroupMemberDto>>;

public class GetGroupMembersQueryHandler : IRequestHandler<GetGroupMembersQuery, List<GroupMemberDto>>
{
    private readonly AppDbContext _db;
    public GetGroupMembersQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<GroupMemberDto>> Handle(GetGroupMembersQuery req, CancellationToken ct)
    {
        var memberships = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.GroupId == req.GroupId && m.SpaceId == req.SpaceId)
            .ToListAsync(ct);

        var personIds = memberships.Select(m => m.PersonId).ToList();
        var people = await _db.People.AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // Load group-scoped role assignments for this group
        var roleAssignments = await _db.PersonRoleAssignments.AsNoTracking()
            .Where(a => a.GroupId == req.GroupId && personIds.Contains(a.PersonId))
            .ToListAsync(ct);

        // Load role names for the assigned roles
        var roleIds = roleAssignments.Select(a => a.RoleId).Distinct().ToList();
        var roles = roleIds.Any()
            ? await _db.SpaceRoles.AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct)
            : new Dictionary<Guid, string>();

        // Map person → role (one role per person per group)
        var personRoleMap = roleAssignments
            .GroupBy(a => a.PersonId)
            .ToDictionary(g => g.Key, g => g.First());

        return memberships
            .Where(m => people.ContainsKey(m.PersonId))
            .OrderBy(m => people[m.PersonId].FullName)
            .Select(m => {
                var p = people[m.PersonId];
                personRoleMap.TryGetValue(m.PersonId, out var assignment);
                var roleName = assignment is not null && roles.TryGetValue(assignment.RoleId, out var rn) ? rn : null;
                return new GroupMemberDto(
                    p.Id, p.FullName, p.DisplayName, m.IsOwner, p.PhoneNumber,
                    p.InvitationStatus ?? "accepted", p.ProfileImageUrl,
                    p.Birthday?.ToString("yyyy-MM-dd"), p.LinkedUserId,
                    assignment?.RoleId, roleName, m.HomeLeavePriority);
            })
            .ToList();
    }
}
