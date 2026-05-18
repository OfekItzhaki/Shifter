using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

public record GetActiveRecommendationsQuery(
    Guid SpaceId,
    Guid GroupId,
    Guid UserId) : IRequest<List<RecommendationDto>>;

public class GetActiveRecommendationsQueryHandler : IRequestHandler<GetActiveRecommendationsQuery, List<RecommendationDto>>
{
    private readonly AppDbContext _db;

    public GetActiveRecommendationsQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<RecommendationDto>> Handle(GetActiveRecommendationsQuery req, CancellationToken ct)
    {
        // ── Permission check: return empty if user doesn't have ViewAndEdit or Owner on the group ──
        var hasPermission = await HasGroupEditPermissionAsync(req.UserId, req.SpaceId, req.GroupId, ct);
        if (!hasPermission)
            return [];

        // ── Emergency freeze check: return empty if freeze is active (Req 6.3) ──
        var homeLeaveConfig = await _db.HomeLeaveConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == req.SpaceId && c.GroupId == req.GroupId, ct);

        if (homeLeaveConfig?.EmergencyFreezeActive == true)
            return [];

        // ── Query active recommendations for this space and group ──
        var recommendations = await _db.DoubleShiftRecommendations.AsNoTracking()
            .Where(r => r.SpaceId == req.SpaceId
                     && r.GroupId == req.GroupId
                     && r.Status == RecommendationStatus.Active)
            .OrderByDescending(r => r.AdditionalSlotsCovered)
            .ThenBy(r => r.TaskName)
            .ToListAsync(ct);

        return recommendations.Select(r => new RecommendationDto(
            r.Id,
            r.GroupTaskId,
            r.TaskName,
            r.Status.ToString(),
            r.AdditionalSlotsCovered,
            r.AffectedDateStart,
            r.AffectedDateEnd,
            r.TotalUncoveredSlotsInRun,
            r.CreatedAt)).ToList();
    }

    /// <summary>
    /// Checks whether the user has ViewAndEdit or Owner permission level on the group.
    /// Space owners implicitly have full access.
    /// </summary>
    private async Task<bool> HasGroupEditPermissionAsync(Guid userId, Guid spaceId, Guid groupId, CancellationToken ct)
    {
        // Space owner always has access
        var isSpaceOwner = await _db.Spaces.AsNoTracking()
            .AnyAsync(s => s.Id == spaceId && s.OwnerUserId == userId && s.IsActive, ct);
        if (isSpaceOwner)
            return true;

        // Find the person linked to this user in this space
        var person = await _db.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SpaceId == spaceId && p.LinkedUserId == userId, ct);
        if (person is null)
            return false;

        // Find the user's role assignment for this group
        var roleAssignment = await _db.PersonRoleAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PersonId == person.Id && a.GroupId == groupId, ct);
        if (roleAssignment is null)
            return false;

        // Load the role and check its permission level
        var role = await _db.SpaceRoles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleAssignment.RoleId, ct);

        return role is not null &&
               (role.PermissionLevel == RolePermissionLevel.ViewAndEdit ||
                role.PermissionLevel == RolePermissionLevel.Owner);
    }
}
