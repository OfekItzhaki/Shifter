using Jobuler.Domain.Organizations;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Organizations.Queries;

public record OrganizationCandidateDto(
    Guid Id,
    string DisplayName,
    string NormalizedName,
    Guid PrimaryOwnerUserId,
    string? PrimaryOwnerEmail,
    string? PrimaryOwnerDisplayName,
    string? CountryCode,
    string? SetupTemplate,
    string? DefaultLocale,
    OrganizationStatus Status,
    DateTime? DisabledAt,
    DateTime? PurgeEligibleAt,
    string? DedicatedDeploymentKey,
    int SpaceCount,
    int GroupCount,
    int MemberCount,
    DateTime CreatedAt);

public record SearchOrganizationsQuery(
    string? Search = null,
    string? CountryCode = null,
    string? SetupTemplate = null,
    OrganizationStatus? Status = null,
    int Limit = 50) : IRequest<List<OrganizationCandidateDto>>;

public class SearchOrganizationsQueryHandler : IRequestHandler<SearchOrganizationsQuery, List<OrganizationCandidateDto>>
{
    private readonly AppDbContext _db;

    public SearchOrganizationsQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<OrganizationCandidateDto>> Handle(SearchOrganizationsQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Limit, 1, 200);
        var search = request.Search?.Trim().ToUpperInvariant();
        var countryCode = request.CountryCode?.Trim().ToUpperInvariant();
        var setupTemplate = request.SetupTemplate?.Trim().ToLowerInvariant();

        var query = _db.Organizations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(o =>
                o.NormalizedName.Contains(search)
                || o.DisplayName.ToUpper().Contains(search)
                || _db.Spaces.Any(s => s.OrganizationId == o.Id && s.Name.ToUpper().Contains(search))
                || _db.Users.Any(u => u.Id == o.PrimaryOwnerUserId
                    && (u.Email.ToUpper().Contains(search) || u.DisplayName.ToUpper().Contains(search))));
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
            query = query.Where(o => o.CountryCode == countryCode);

        if (!string.IsNullOrWhiteSpace(setupTemplate))
            query = query.Where(o => o.SetupTemplate == setupTemplate);

        if (request.Status.HasValue)
            query = query.Where(o => o.Status == request.Status.Value);

        var organizations = await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        var organizationIds = organizations.Select(o => o.Id).ToList();
        var ownerIds = organizations.Select(o => o.PrimaryOwnerUserId).Distinct().ToList();

        var owners = await _db.Users.AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var spaceCounts = await _db.Spaces.AsNoTracking()
            .Where(s => organizationIds.Contains(s.OrganizationId) && s.DeletedAt == null)
            .GroupBy(s => s.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.Count, ct);

        var groupCounts = await _db.Groups.AsNoTracking()
            .Join(_db.Spaces.AsNoTracking(),
                g => g.SpaceId,
                s => s.Id,
                (g, s) => new { Group = g, Space = s })
            .Where(x => organizationIds.Contains(x.Space.OrganizationId)
                && x.Space.DeletedAt == null
                && x.Group.DeletedAt == null)
            .GroupBy(x => x.Space.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.Count, ct);

        var memberCounts = await _db.SpaceMemberships.AsNoTracking()
            .Join(_db.Spaces.AsNoTracking(),
                m => m.SpaceId,
                s => s.Id,
                (m, s) => new { Membership = m, Space = s })
            .Where(x => organizationIds.Contains(x.Space.OrganizationId)
                && x.Space.DeletedAt == null
                && x.Membership.IsActive)
            .GroupBy(x => x.Space.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, Count = g.Select(x => x.Membership.UserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.OrganizationId, x => x.Count, ct);

        return organizations.Select(o =>
        {
            owners.TryGetValue(o.PrimaryOwnerUserId, out var owner);
            return new OrganizationCandidateDto(
                o.Id,
                o.DisplayName,
                o.NormalizedName,
                o.PrimaryOwnerUserId,
                owner?.Email,
                owner?.DisplayName,
                o.CountryCode,
                o.SetupTemplate,
                o.DefaultLocale,
                o.Status,
                o.DisabledAt,
                o.PurgeEligibleAt,
                o.DedicatedDeploymentKey,
                spaceCounts.GetValueOrDefault(o.Id),
                groupCounts.GetValueOrDefault(o.Id),
                memberCounts.GetValueOrDefault(o.Id),
                o.CreatedAt);
        }).ToList();
    }
}
