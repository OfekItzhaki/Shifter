using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.Queries;

public record PersonDto(
    Guid Id, Guid SpaceId, string FullName, string? DisplayName,
    string? ProfileImageUrl, bool IsActive, DateTime CreatedAt);

public record PersonRoleDto(Guid RoleId, string Name);

public record PersonDetailDto(
    Guid Id, Guid SpaceId, string FullName, string? DisplayName,
    string? ProfileImageUrl, bool IsActive, DateTime CreatedAt,
    List<string> Qualifications,
    List<string> RoleNames,
    List<string> GroupNames,
    List<RestrictionDto> Restrictions,
    List<PersonRoleDto> Roles);

public record RestrictionDto(
    Guid Id, string RestrictionType, DateOnly EffectiveFrom,
    DateOnly? EffectiveUntil, string? OperationalNote,
    string? SensitiveReason);  // null unless caller has manage_sensitive

// ── List ──────────────────────────────────────────────────────────────────────
public record GetPeopleQuery(Guid SpaceId, bool ActiveOnly = true) : IRequest<List<PersonDto>>;

public class GetPeopleQueryHandler : IRequestHandler<GetPeopleQuery, List<PersonDto>>
{
    private readonly AppDbContext _db;
    public GetPeopleQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<PersonDto>> Handle(GetPeopleQuery req, CancellationToken ct)
    {
        var query = _db.People.AsNoTracking().Where(p => p.SpaceId == req.SpaceId);
        if (req.ActiveOnly) query = query.Where(p => p.IsActive);

        return await query
            .OrderBy(p => p.FullName)
            .Select(p => new PersonDto(p.Id, p.SpaceId, p.FullName, p.DisplayName,
                p.ProfileImageUrl, p.IsActive, p.CreatedAt))
            .ToListAsync(ct);
    }
}

// ── Detail ────────────────────────────────────────────────────────────────────
public record GetPersonDetailQuery(
    Guid SpaceId, Guid PersonId, bool IncludeSensitive) : IRequest<PersonDetailDto?>;

public class GetPersonDetailQueryHandler : IRequestHandler<GetPersonDetailQuery, PersonDetailDto?>
{
    private readonly AppDbContext _db;
    public GetPersonDetailQueryHandler(AppDbContext db) => _db = db;

    public async Task<PersonDetailDto?> Handle(GetPersonDetailQuery req, CancellationToken ct)
    {
        var person = await _db.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == req.PersonId && p.SpaceId == req.SpaceId, ct);
        if (person is null) return null;

        var qualifications = await _db.PersonQualifications.AsNoTracking()
            .Where(q => q.PersonId == req.PersonId && q.IsActive)
            .Select(q => q.Qualification)
            .ToListAsync(ct);

        var roles = await _db.PersonRoleAssignments.AsNoTracking()
            .Where(r => r.PersonId == req.PersonId && r.SpaceId == req.SpaceId)
            .Join(_db.SpaceRoles, r => r.RoleId, sr => sr.Id,
                  (r, sr) => new PersonRoleDto(sr.Id, sr.Name))
            .ToListAsync(ct);

        var groupNames = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.PersonId == req.PersonId)
            .Join(_db.Groups, m => m.GroupId, g => g.Id, (m, g) => g.Name)
            .ToListAsync(ct);

        var restrictions = await _db.PersonRestrictions.AsNoTracking()
            .Where(r => r.PersonId == req.PersonId)
            .ToListAsync(ct);

        var sensitiveMap = new Dictionary<Guid, string>();
        if (req.IncludeSensitive)
        {
            var ids = restrictions.Select(r => r.Id).ToList();
            sensitiveMap = await _db.SensitiveRestrictionReasons.AsNoTracking()
                .Where(s => ids.Contains(s.RestrictionId))
                .ToDictionaryAsync(s => s.RestrictionId, s => s.Reason, ct);
        }

        var restrictionDtos = restrictions.Select(r => new RestrictionDto(
            r.Id, r.RestrictionType, r.EffectiveFrom, r.EffectiveUntil,
            r.OperationalNote, sensitiveMap.GetValueOrDefault(r.Id))).ToList();

        return new PersonDetailDto(
            person.Id, person.SpaceId, person.FullName, person.DisplayName,
            person.ProfileImageUrl, person.IsActive, person.CreatedAt,
            qualifications, roles.Select(r => r.Name).ToList(),
            groupNames, restrictionDtos, roles);
    }
}
