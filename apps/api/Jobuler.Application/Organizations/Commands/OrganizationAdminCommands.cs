using Jobuler.Domain.Common;
using Jobuler.Domain.Organizations;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Organizations.Commands;

public record UpdateOrganizationCommand(
    Guid OrganizationId,
    string DisplayName,
    string? CountryCode,
    string? SetupTemplate,
    string? DefaultLocale,
    string? DefaultTimezoneId) : IRequest;

public record MoveSpaceToOrganizationCommand(
    Guid SpaceId,
    Guid OrganizationId) : IRequest;

public record MarkOrganizationRelocatedCommand(
    Guid OrganizationId,
    string DedicatedDeploymentKey,
    int RetentionDays = 90) : IRequest;

public record RestoreRelocatedOrganizationCommand(Guid OrganizationId) : IRequest;

public record MarkOrganizationPurgePendingCommand(Guid OrganizationId) : IRequest;

public record PurgeOrganizationCommand(Guid OrganizationId) : IRequest<PurgeOrganizationResult>;

public record PurgeOrganizationResult(
    Guid OrganizationId,
    int SpaceCount,
    int RemovedTenantScopedRowCount,
    int RemovedAuditLogCount);

public class UpdateOrganizationCommandHandler : IRequestHandler<UpdateOrganizationCommand>
{
    private readonly AppDbContext _db;

    public UpdateOrganizationCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateOrganizationCommand request, CancellationToken ct)
    {
        var organization = await _db.Organizations.FindAsync([request.OrganizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        organization.UpdateIdentity(
            request.DisplayName,
            request.CountryCode,
            request.SetupTemplate,
            request.DefaultLocale,
            request.DefaultTimezoneId);

        await _db.SaveChangesAsync(ct);
    }
}

public class MoveSpaceToOrganizationCommandHandler : IRequestHandler<MoveSpaceToOrganizationCommand>
{
    private readonly AppDbContext _db;

    public MoveSpaceToOrganizationCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(MoveSpaceToOrganizationCommand request, CancellationToken ct)
    {
        var organizationExists = await _db.Organizations
            .AnyAsync(o => o.Id == request.OrganizationId, ct);
        if (!organizationExists)
            throw new KeyNotFoundException("Target organization not found.");

        var space = await _db.Spaces.FindAsync([request.SpaceId], ct)
            ?? throw new KeyNotFoundException("Space not found.");

        space.MoveToOrganization(request.OrganizationId);
        await _db.SaveChangesAsync(ct);
    }
}

public class MarkOrganizationRelocatedCommandHandler : IRequestHandler<MarkOrganizationRelocatedCommand>
{
    private readonly AppDbContext _db;

    public MarkOrganizationRelocatedCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(MarkOrganizationRelocatedCommand request, CancellationToken ct)
    {
        var organization = await _db.Organizations.FindAsync([request.OrganizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        organization.MarkRelocated(
            request.DedicatedDeploymentKey,
            DateTime.UtcNow,
            request.RetentionDays);
        await _db.SaveChangesAsync(ct);
    }
}

public class RestoreRelocatedOrganizationCommandHandler : IRequestHandler<RestoreRelocatedOrganizationCommand>
{
    private readonly AppDbContext _db;

    public RestoreRelocatedOrganizationCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(RestoreRelocatedOrganizationCommand request, CancellationToken ct)
    {
        var organization = await _db.Organizations.FindAsync([request.OrganizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        organization.RestoreAfterRelocationReview();
        await _db.SaveChangesAsync(ct);
    }
}

public class MarkOrganizationPurgePendingCommandHandler : IRequestHandler<MarkOrganizationPurgePendingCommand>
{
    private readonly AppDbContext _db;

    public MarkOrganizationPurgePendingCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(MarkOrganizationPurgePendingCommand request, CancellationToken ct)
    {
        var organization = await _db.Organizations.FindAsync([request.OrganizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        organization.MarkPurgePending(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
    }
}

public class PurgeOrganizationCommandHandler : IRequestHandler<PurgeOrganizationCommand, PurgeOrganizationResult>
{
    private readonly AppDbContext _db;

    public PurgeOrganizationCommandHandler(AppDbContext db) => _db = db;

    public async Task<PurgeOrganizationResult> Handle(PurgeOrganizationCommand request, CancellationToken ct)
    {
        var organization = await _db.Organizations.FindAsync([request.OrganizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        if (organization.Status != OrganizationStatus.PurgePending)
            throw new InvalidOperationException("Organization must be marked purge pending before final purge.");

        var spaceIds = await _db.Spaces
            .Where(s => s.OrganizationId == request.OrganizationId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var removedTenantRows = 0;
        var removedAuditLogs = 0;

        if (spaceIds.Count > 0)
        {
            removedTenantRows += await RemoveTenantScopedAsync(_db.SwapRequests, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.WaitlistEntries, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.ShiftRequests, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.ShiftSlots, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.ShiftTemplates, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.SchedulingCycles, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.SelfServiceConfigs, spaceIds, ct);

            removedTenantRows += await RemoveTenantScopedAsync(_db.Assignments, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.AssignmentChangeSummaries, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.DoubleShiftRecommendations, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.FairnessCounterSnapshots, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.FairnessCounters, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.TaskRotationProgress, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.SubscriptionPeriods, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.CumulativeRecords, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.DailySnapshots, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.ScheduleVersions, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.ScheduleRuns, spaceIds, ct);

            removedTenantRows += await RemoveTenantScopedAsync(_db.ConstraintRules, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.TaskTypeOverlapRules, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.TaskSlots, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.GroupTasks, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.TaskTypes, spaceIds, ct);

            removedTenantRows += await RemoveTenantScopedAsync(_db.GroupMessages, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.GroupAlerts, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.GroupInvitations, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.PendingOwnershipTransfers, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.PersonRoleAssignments, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.MemberQualifications, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.GroupQualifications, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.HomeLeaveConfigs, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.HomeLeaveTemplates, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.GroupMemberships, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.Groups, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.GroupTypes, spaceIds, ct);

            removedTenantRows += await RemoveTenantScopedAsync(_db.AvailabilityWindows, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.PresenceWindows, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.PersonRestrictions, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.SensitiveRestrictionReasons, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.PersonQualifications, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.PendingInvitations, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.People, spaceIds, ct);

            removedTenantRows += await RemoveTenantScopedAsync(_db.PushSubscriptions, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.Notifications, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.SystemLogs, spaceIds, ct);

            removedTenantRows += await RemoveTenantScopedAsync(_db.SpaceHomeLeaveConfigs, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.UnavailabilityReasons, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.OwnershipTransferHistory, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.SpacePermissionGrants, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.SpaceRoles, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.SpaceMemberships, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.GroupSubscriptions, spaceIds, ct);
            removedTenantRows += await RemoveTenantScopedAsync(_db.SpaceSubscriptions, spaceIds, ct);

            var auditLogs = await _db.AuditLogs
                .Where(l => l.SpaceId.HasValue && spaceIds.Contains(l.SpaceId.Value))
                .ToListAsync(ct);
            removedAuditLogs = auditLogs.Count;
            _db.AuditLogs.RemoveRange(auditLogs);

            var spaces = await _db.Spaces
                .Where(s => spaceIds.Contains(s.Id))
                .ToListAsync(ct);
            _db.Spaces.RemoveRange(spaces);
        }

        var organizationSubscriptions = await _db.OrganizationSubscriptions
            .Where(s => s.OrganizationId == request.OrganizationId)
            .ToListAsync(ct);
        _db.OrganizationSubscriptions.RemoveRange(organizationSubscriptions);

        _db.Organizations.Remove(organization);
        await _db.SaveChangesAsync(ct);

        return new PurgeOrganizationResult(
            request.OrganizationId,
            spaceIds.Count,
            removedTenantRows,
            removedAuditLogs);
    }

    private static async Task<int> RemoveTenantScopedAsync<TEntity>(
        DbSet<TEntity> set,
        IReadOnlyCollection<Guid> spaceIds,
        CancellationToken ct)
        where TEntity : class, ITenantScoped
    {
        var rows = await set
            .Where(row => spaceIds.Contains(row.SpaceId))
            .ToListAsync(ct);

        set.RemoveRange(rows);
        return rows.Count;
    }
}
