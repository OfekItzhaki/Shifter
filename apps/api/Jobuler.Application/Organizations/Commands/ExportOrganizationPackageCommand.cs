using System.Text.Json;
using Jobuler.Application.Organizations.Queries;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Organizations.Commands;

public record ExportOrganizationPackageCommand(Guid OrganizationId) : IRequest<OrganizationExportPackageResult>;

public record OrganizationExportPackageResult(byte[] Content, string FileName);

public record OrganizationExportPackageDto(
    int SchemaVersion,
    DateTime ExportedAt,
    OrganizationExportManifestDto Manifest,
    OrganizationPackageDataDto Data);

public record OrganizationPackageDataDto(
    object Organization,
    List<object> Spaces,
    List<object> Users,
    List<object> SpaceMemberships,
    List<object> Groups,
    List<object> People,
    List<object> GroupMemberships,
    List<object> GroupTasks,
    List<object> TaskTypes,
    List<object> TaskSlots,
    List<object> Constraints,
    List<object> ScheduleRuns,
    List<object> ScheduleVersions,
    List<object> Assignments,
    List<object> SpaceSelfServiceDefaults,
    List<object> SpaceSpecialDays,
    List<object> SelfServiceConfigs,
    List<object> SchedulingCycles,
    List<object> ShiftTemplates,
    List<object> ShiftSlots,
    List<object> ShiftRequests,
    List<object> ShiftAttendanceRecords,
    List<object> ShiftAbsenceReports,
    List<object> ShiftChangeRequests,
    List<object> WaitlistEntries,
    List<object> SwapRequests,
    List<object> SpecialLeaveRequests);

public class ExportOrganizationPackageCommandHandler
    : IRequestHandler<ExportOrganizationPackageCommand, OrganizationExportPackageResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly AppDbContext _db;
    private readonly IMediator _mediator;

    public ExportOrganizationPackageCommandHandler(AppDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<OrganizationExportPackageResult> Handle(
        ExportOrganizationPackageCommand request,
        CancellationToken ct)
    {
        var manifest = await _mediator.Send(new GetOrganizationExportManifestQuery(request.OrganizationId), ct);

        var organization = await _db.Organizations.AsNoTracking()
            .Where(o => o.Id == request.OrganizationId)
            .Select(o => new
            {
                o.Id,
                o.DisplayName,
                o.NormalizedName,
                o.PrimaryOwnerUserId,
                o.CountryCode,
                o.SetupTemplate,
                o.DefaultLocale,
                o.DefaultTimezoneId,
                o.Status,
                o.RelocatedAt,
                o.DisabledAt,
                o.PurgeEligibleAt,
                o.DedicatedDeploymentKey,
                o.CreatedAt,
                o.UpdatedAt
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        var spaces = await _db.Spaces.AsNoTracking()
            .Where(s => s.OrganizationId == request.OrganizationId && s.DeletedAt == null)
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.OrganizationId,
                s.Name,
                s.Description,
                s.OwnerUserId,
                s.IsActive,
                s.Locale,
                s.InviteCode,
                s.DeletedAt,
                s.ManagementTimeoutMinutes,
                s.CreatedAt,
                s.UpdatedAt
            })
            .ToListAsync(ct);

        var spaceIds = spaces.Select(s => s.Id).ToList();

        var userIds = await _db.SpaceMemberships.AsNoTracking()
            .Where(m => spaceIds.Contains(m.SpaceId))
            .Select(m => m.UserId)
            .Union(_db.People.AsNoTracking()
                .Where(p => spaceIds.Contains(p.SpaceId) && p.LinkedUserId.HasValue)
                .Select(p => p.LinkedUserId!.Value))
            .Distinct()
            .ToListAsync(ct);

        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .OrderBy(u => u.Email)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.IsActive,
                u.PreferredLocale,
                u.ProfileImageUrl,
                u.LastLoginAt,
                u.PhoneNumber,
                u.EmailVerified,
                u.CountryCode,
                u.StateCode,
                u.Birthday,
                u.CreatedAt,
                u.UpdatedAt
            })
            .ToListAsync(ct);

        var data = new OrganizationPackageDataDto(
            Organization: organization,
            Spaces: spaces.Cast<object>().ToList(),
            Users: users.Cast<object>().ToList(),
            SpaceMemberships: await _db.SpaceMemberships.AsNoTracking()
                .Where(m => spaceIds.Contains(m.SpaceId))
                .OrderBy(m => m.SpaceId)
                .Select(m => new { m.Id, m.SpaceId, m.UserId, m.JoinedAt, m.IsActive, m.PermissionLevel, m.CreatedAt })
                .Cast<object>()
                .ToListAsync(ct),
            Groups: await _db.Groups.AsNoTracking()
                .Where(g => spaceIds.Contains(g.SpaceId))
                .OrderBy(g => g.SpaceId).ThenBy(g => g.Name)
                .Select(g => new
                {
                    g.Id,
                    g.SpaceId,
                    g.GroupTypeId,
                    g.CreatedByUserId,
                    g.Name,
                    g.Description,
                    g.IsActive,
                    g.SolverHorizonDays,
                    g.SolverStartDateTime,
                    g.AutoPublish,
                    g.IsClosedBase,
                    g.MinRestBetweenShiftsHours,
                    g.JoinCode,
                    g.DeletedAt,
                    g.DeletedBySpaceDeletion,
                    g.TemplateType,
                    g.AllowMembersViewHistory,
                    g.AllowMembersViewStats,
                    g.ManagementTimeoutMinutes,
                    g.ParentGroupId,
                    g.SchedulingMode,
                    g.CreatedAt,
                    g.UpdatedAt
                })
                .Cast<object>()
                .ToListAsync(ct),
            People: await _db.People.AsNoTracking()
                .Where(p => spaceIds.Contains(p.SpaceId))
                .OrderBy(p => p.SpaceId).ThenBy(p => p.FullName)
                .Select(p => new
                {
                    p.Id,
                    p.SpaceId,
                    p.LinkedUserId,
                    p.FullName,
                    p.DisplayName,
                    p.ProfileImageUrl,
                    p.IsActive,
                    p.PhoneNumber,
                    p.Email,
                    p.InvitationStatus,
                    p.Birthday,
                    p.CreatedAt,
                    p.UpdatedAt
                })
                .Cast<object>()
                .ToListAsync(ct),
            GroupMemberships: await _db.GroupMemberships.AsNoTracking()
                .Where(m => spaceIds.Contains(m.SpaceId))
                .OrderBy(m => m.SpaceId)
                .Select(m => new
                {
                    m.Id,
                    m.SpaceId,
                    m.GroupId,
                    m.PersonId,
                    m.IsOwner,
                    m.JoinedAt,
                    m.HomeLeavePriority,
                    m.CreatedAt
                })
                .Cast<object>()
                .ToListAsync(ct),
            GroupTasks: await _db.GroupTasks.AsNoTracking()
                .Where(t => spaceIds.Contains(t.SpaceId))
                .OrderBy(t => t.SpaceId).ThenBy(t => t.StartsAt)
                .Select(t => new
                {
                    t.Id,
                    t.SpaceId,
                    t.GroupId,
                    t.Name,
                    t.StartsAt,
                    t.EndsAt,
                    t.ShiftDurationMinutes,
                    t.RequiredHeadcount,
                    t.BurdenLevel,
                    t.AllowsDoubleShift,
                    t.AllowsOverlap,
                    t.DailyStartTime,
                    t.DailyEndTime,
                    t.QualificationRequirements,
                    t.SplitCount,
                    t.IsActive,
                    t.CreatedByUserId,
                    t.UpdatedByUserId,
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .Cast<object>()
                .ToListAsync(ct),
            TaskTypes: await _db.TaskTypes.AsNoTracking()
                .Where(t => spaceIds.Contains(t.SpaceId))
                .OrderBy(t => t.SpaceId).ThenBy(t => t.Name)
                .Cast<object>()
                .ToListAsync(ct),
            TaskSlots: await _db.TaskSlots.AsNoTracking()
                .Where(s => spaceIds.Contains(s.SpaceId))
                .OrderBy(s => s.SpaceId).ThenBy(s => s.StartsAt)
                .Cast<object>()
                .ToListAsync(ct),
            Constraints: await _db.ConstraintRules.AsNoTracking()
                .Where(c => spaceIds.Contains(c.SpaceId))
                .OrderBy(c => c.SpaceId).ThenBy(c => c.RuleType)
                .Select(c => new
                {
                    c.Id,
                    c.SpaceId,
                    c.ScopeType,
                    c.ScopeId,
                    c.Severity,
                    c.RuleType,
                    c.RulePayloadJson,
                    c.IsActive,
                    c.EffectiveFrom,
                    c.EffectiveUntil,
                    c.CreatedByUserId,
                    c.UpdatedByUserId,
                    c.CreatedAt,
                    c.UpdatedAt
                })
                .Cast<object>()
                .ToListAsync(ct),
            ScheduleRuns: await _db.ScheduleRuns.AsNoTracking()
                .Where(r => spaceIds.Contains(r.SpaceId))
                .OrderBy(r => r.SpaceId).ThenBy(r => r.CreatedAt)
                .Cast<object>()
                .ToListAsync(ct),
            ScheduleVersions: await _db.ScheduleVersions.AsNoTracking()
                .Where(v => spaceIds.Contains(v.SpaceId))
                .OrderBy(v => v.SpaceId).ThenBy(v => v.VersionNumber)
                .Select(v => new
                {
                    v.Id,
                    v.SpaceId,
                    v.VersionNumber,
                    v.Status,
                    v.BaselineVersionId,
                    v.SourceRunId,
                    v.RollbackSourceVersionId,
                    v.CreatedByUserId,
                    v.PublishedByUserId,
                    v.PublishedAt,
                    v.SummaryJson,
                    v.SupersedesVersionId,
                    v.SourceType,
                    v.CreatedAt
                })
                .Cast<object>()
                .ToListAsync(ct),
            Assignments: await _db.Assignments.AsNoTracking()
                .Where(a => spaceIds.Contains(a.SpaceId))
                .OrderBy(a => a.SpaceId)
                .Select(a => new
                {
                    a.Id,
                    a.SpaceId,
                    a.ScheduleVersionId,
                    a.TaskSlotId,
                    a.PersonId,
                    a.Source,
                    a.ChangeReasonSummary,
                    a.CreatedAt
                })
                .Cast<object>()
                .ToListAsync(ct),
            SpaceSelfServiceDefaults: await _db.SpaceSelfServiceDefaults.AsNoTracking()
                .Where(d => spaceIds.Contains(d.SpaceId))
                .OrderBy(d => d.SpaceId)
                .Cast<object>()
                .ToListAsync(ct),
            SpaceSpecialDays: await _db.SpaceSpecialDays.AsNoTracking()
                .Where(d => spaceIds.Contains(d.SpaceId))
                .OrderBy(d => d.SpaceId).ThenBy(d => d.Date)
                .Cast<object>()
                .ToListAsync(ct),
            SelfServiceConfigs: await _db.SelfServiceConfigs.AsNoTracking()
                .Where(c => spaceIds.Contains(c.SpaceId))
                .OrderBy(c => c.SpaceId).ThenBy(c => c.GroupId)
                .Cast<object>()
                .ToListAsync(ct),
            SchedulingCycles: await _db.SchedulingCycles.AsNoTracking()
                .Where(c => spaceIds.Contains(c.SpaceId))
                .OrderBy(c => c.SpaceId).ThenBy(c => c.StartsAt)
                .Cast<object>()
                .ToListAsync(ct),
            ShiftTemplates: await _db.ShiftTemplates.AsNoTracking()
                .Where(t => spaceIds.Contains(t.SpaceId))
                .OrderBy(t => t.SpaceId).ThenBy(t => t.GroupId)
                .Cast<object>()
                .ToListAsync(ct),
            ShiftSlots: await _db.ShiftSlots.AsNoTracking()
                .Where(s => spaceIds.Contains(s.SpaceId))
                .OrderBy(s => s.SpaceId).ThenBy(s => s.StartsAt)
                .Cast<object>()
                .ToListAsync(ct),
            ShiftRequests: await _db.ShiftRequests.AsNoTracking()
                .Where(r => spaceIds.Contains(r.SpaceId))
                .OrderBy(r => r.SpaceId).ThenBy(r => r.GroupId)
                .Cast<object>()
                .ToListAsync(ct),
            ShiftAttendanceRecords: await _db.ShiftAttendanceRecords.AsNoTracking()
                .Where(r => spaceIds.Contains(r.SpaceId))
                .OrderBy(r => r.SpaceId)
                .Cast<object>()
                .ToListAsync(ct),
            ShiftAbsenceReports: await _db.ShiftAbsenceReports.AsNoTracking()
                .Where(r => spaceIds.Contains(r.SpaceId))
                .OrderBy(r => r.SpaceId)
                .Cast<object>()
                .ToListAsync(ct),
            ShiftChangeRequests: await _db.ShiftChangeRequests.AsNoTracking()
                .Where(r => spaceIds.Contains(r.SpaceId))
                .OrderBy(r => r.SpaceId)
                .Cast<object>()
                .ToListAsync(ct),
            WaitlistEntries: await _db.WaitlistEntries.AsNoTracking()
                .Where(e => spaceIds.Contains(e.SpaceId))
                .OrderBy(e => e.SpaceId)
                .Cast<object>()
                .ToListAsync(ct),
            SwapRequests: await _db.SwapRequests.AsNoTracking()
                .Where(r => spaceIds.Contains(r.SpaceId))
                .OrderBy(r => r.SpaceId)
                .Cast<object>()
                .ToListAsync(ct),
            SpecialLeaveRequests: await _db.SpecialLeaveRequests.AsNoTracking()
                .Where(r => spaceIds.Contains(r.SpaceId))
                .OrderBy(r => r.SpaceId).ThenBy(r => r.StartsAt)
                .Cast<object>()
                .ToListAsync(ct));

        var package = new OrganizationExportPackageDto(
            SchemaVersion: 1,
            ExportedAt: DateTime.UtcNow,
            Manifest: manifest,
            Data: data);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(package, JsonOptions);
        var fileName = $"organization-{request.OrganizationId:N}-export-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        return new OrganizationExportPackageResult(bytes, fileName);
    }
}
