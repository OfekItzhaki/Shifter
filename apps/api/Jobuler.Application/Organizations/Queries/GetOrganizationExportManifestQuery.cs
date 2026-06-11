using Jobuler.Domain.Organizations;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Organizations.Queries;

public record OrganizationExportSpaceDto(
    Guid SpaceId,
    string Name,
    Guid OwnerUserId,
    int GroupCount,
    int PeopleCount,
    int SpaceMemberCount,
    int TaskCount,
    int ConstraintCount,
    int ScheduleVersionCount,
    int AssignmentCount);

public record OrganizationExportCountsDto(
    int Spaces,
    int Groups,
    int People,
    int SpaceMemberships,
    int GroupMemberships,
    int GroupTasks,
    int TaskTypes,
    int TaskSlots,
    int Constraints,
    int ScheduleRuns,
    int ScheduleVersions,
    int Assignments,
    int SpaceSelfServiceDefaults,
    int SpaceSpecialDays,
    int SelfServiceConfigs,
    int SchedulingCycles,
    int ShiftTemplates,
    int ShiftSlots,
    int ShiftRequests,
    int ShiftAttendanceRecords,
    int ShiftAbsenceReports,
    int ShiftChangeRequests,
    int WaitlistEntries,
    int SwapRequests,
    int SpecialLeaveRequests,
    int Notifications,
    int AuditLogs);

public record OrganizationExportManifestDto(
    Guid OrganizationId,
    string OrganizationName,
    string? CountryCode,
    string? SetupTemplate,
    OrganizationStatus Status,
    DateTime GeneratedAt,
    OrganizationExportCountsDto Counts,
    List<OrganizationExportSpaceDto> Spaces,
    List<string> Warnings);

public record GetOrganizationExportManifestQuery(Guid OrganizationId) : IRequest<OrganizationExportManifestDto>;

public class GetOrganizationExportManifestQueryHandler
    : IRequestHandler<GetOrganizationExportManifestQuery, OrganizationExportManifestDto>
{
    private readonly AppDbContext _db;

    public GetOrganizationExportManifestQueryHandler(AppDbContext db) => _db = db;

    public async Task<OrganizationExportManifestDto> Handle(
        GetOrganizationExportManifestQuery request,
        CancellationToken ct)
    {
        var organization = await _db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        var spaces = await _db.Spaces.AsNoTracking()
            .Where(s => s.OrganizationId == request.OrganizationId && s.DeletedAt == null)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        var spaceIds = spaces.Select(s => s.Id).ToList();
        var warnings = new List<string>();

        if (spaces.Count == 0)
            warnings.Add("Organization has no active spaces to export.");

        if (organization.Status != OrganizationStatus.Active)
            warnings.Add($"Organization status is {organization.Status}; export should usually happen before final purge.");

        var hasOrganizationSubscription = await _db.OrganizationSubscriptions.AsNoTracking()
            .AnyAsync(s => s.OrganizationId == request.OrganizationId, ct);
        if (hasOrganizationSubscription)
            warnings.Add("Organization billing coverage exists; recreate billing in the target deployment instead of exporting provider payment identifiers.");

        var groupCountBySpace = await CountBySpaceAsync(_db.Groups
            .Where(g => spaceIds.Contains(g.SpaceId) && g.DeletedAt == null), ct);
        var peopleCountBySpace = await CountBySpaceAsync(_db.People
            .Where(p => spaceIds.Contains(p.SpaceId) && p.IsActive), ct);
        var spaceMemberCountBySpace = await CountBySpaceAsync(_db.SpaceMemberships
            .Where(m => spaceIds.Contains(m.SpaceId) && m.IsActive), ct);
        var taskCountBySpace = await CountBySpaceAsync(_db.GroupTasks
            .Where(t => spaceIds.Contains(t.SpaceId) && t.IsActive), ct);
        var constraintCountBySpace = await CountBySpaceAsync(_db.ConstraintRules
            .Where(c => spaceIds.Contains(c.SpaceId) && c.IsActive), ct);
        var scheduleVersionCountBySpace = await CountBySpaceAsync(_db.ScheduleVersions
            .Where(v => spaceIds.Contains(v.SpaceId)), ct);
        var assignmentCountBySpace = await CountBySpaceAsync(_db.Assignments
            .Where(a => spaceIds.Contains(a.SpaceId)), ct);

        var spaceDtos = spaces.Select(space => new OrganizationExportSpaceDto(
            space.Id,
            space.Name,
            space.OwnerUserId,
            groupCountBySpace.GetValueOrDefault(space.Id),
            peopleCountBySpace.GetValueOrDefault(space.Id),
            spaceMemberCountBySpace.GetValueOrDefault(space.Id),
            taskCountBySpace.GetValueOrDefault(space.Id),
            constraintCountBySpace.GetValueOrDefault(space.Id),
            scheduleVersionCountBySpace.GetValueOrDefault(space.Id),
            assignmentCountBySpace.GetValueOrDefault(space.Id))).ToList();

        var counts = new OrganizationExportCountsDto(
            Spaces: spaces.Count,
            Groups: groupCountBySpace.Values.Sum(),
            People: peopleCountBySpace.Values.Sum(),
            SpaceMemberships: spaceMemberCountBySpace.Values.Sum(),
            GroupMemberships: await _db.GroupMemberships.AsNoTracking()
                .CountAsync(m => spaceIds.Contains(m.SpaceId), ct),
            GroupTasks: taskCountBySpace.Values.Sum(),
            TaskTypes: await _db.TaskTypes.AsNoTracking()
                .CountAsync(t => spaceIds.Contains(t.SpaceId), ct),
            TaskSlots: await _db.TaskSlots.AsNoTracking()
                .CountAsync(s => spaceIds.Contains(s.SpaceId), ct),
            Constraints: constraintCountBySpace.Values.Sum(),
            ScheduleRuns: await _db.ScheduleRuns.AsNoTracking()
                .CountAsync(r => spaceIds.Contains(r.SpaceId), ct),
            ScheduleVersions: scheduleVersionCountBySpace.Values.Sum(),
            Assignments: assignmentCountBySpace.Values.Sum(),
            SpaceSelfServiceDefaults: await _db.SpaceSelfServiceDefaults.AsNoTracking()
                .CountAsync(d => spaceIds.Contains(d.SpaceId), ct),
            SpaceSpecialDays: await _db.SpaceSpecialDays.AsNoTracking()
                .CountAsync(d => spaceIds.Contains(d.SpaceId), ct),
            SelfServiceConfigs: await _db.SelfServiceConfigs.AsNoTracking()
                .CountAsync(c => spaceIds.Contains(c.SpaceId), ct),
            SchedulingCycles: await _db.SchedulingCycles.AsNoTracking()
                .CountAsync(c => spaceIds.Contains(c.SpaceId), ct),
            ShiftTemplates: await _db.ShiftTemplates.AsNoTracking()
                .CountAsync(t => spaceIds.Contains(t.SpaceId), ct),
            ShiftSlots: await _db.ShiftSlots.AsNoTracking()
                .CountAsync(s => spaceIds.Contains(s.SpaceId), ct),
            ShiftRequests: await _db.ShiftRequests.AsNoTracking()
                .CountAsync(r => spaceIds.Contains(r.SpaceId), ct),
            ShiftAttendanceRecords: await _db.ShiftAttendanceRecords.AsNoTracking()
                .CountAsync(r => spaceIds.Contains(r.SpaceId), ct),
            ShiftAbsenceReports: await _db.ShiftAbsenceReports.AsNoTracking()
                .CountAsync(r => spaceIds.Contains(r.SpaceId), ct),
            ShiftChangeRequests: await _db.ShiftChangeRequests.AsNoTracking()
                .CountAsync(r => spaceIds.Contains(r.SpaceId), ct),
            WaitlistEntries: await _db.WaitlistEntries.AsNoTracking()
                .CountAsync(e => spaceIds.Contains(e.SpaceId), ct),
            SwapRequests: await _db.SwapRequests.AsNoTracking()
                .CountAsync(r => spaceIds.Contains(r.SpaceId), ct),
            SpecialLeaveRequests: await _db.SpecialLeaveRequests.AsNoTracking()
                .CountAsync(r => spaceIds.Contains(r.SpaceId), ct),
            Notifications: await _db.Notifications.AsNoTracking()
                .CountAsync(n => spaceIds.Contains(n.SpaceId), ct),
            AuditLogs: await _db.AuditLogs.AsNoTracking()
                .CountAsync(l => l.SpaceId.HasValue && spaceIds.Contains(l.SpaceId.Value), ct));

        if (counts.Groups == 0)
            warnings.Add("No groups found in the selected organization.");

        return new OrganizationExportManifestDto(
            organization.Id,
            organization.DisplayName,
            organization.CountryCode,
            organization.SetupTemplate,
            organization.Status,
            DateTime.UtcNow,
            counts,
            spaceDtos,
            warnings);
    }

    private static async Task<Dictionary<Guid, int>> CountBySpaceAsync<T>(
        IQueryable<T> query,
        CancellationToken ct)
        where T : class, Jobuler.Domain.Common.ITenantScoped
    {
        return await query.AsNoTracking()
            .GroupBy(x => x.SpaceId)
            .Select(g => new { SpaceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SpaceId, x => x.Count, ct);
    }
}
