using System.Text.Json;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Commands;

public record DeactivateFreezeWithDiscardCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId,
    bool DiscardFreezeChanges) : IRequest<DeactivateFreezeResult>;

public record DeactivateFreezeResult(
    Guid ConfigId,
    bool DiscardPerformed,
    Guid? DiscardVersionId,
    int DiscardedChangeCount,
    HomeLeaveConfigResult Config);

public class DeactivateFreezeWithDiscardCommandHandler
    : IRequestHandler<DeactivateFreezeWithDiscardCommand, DeactivateFreezeResult>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogger _audit;
    private readonly ICumulativeTracker _cumulativeTracker;
    private readonly ICacheService _cache;

    public DeactivateFreezeWithDiscardCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IAuditLogger audit,
        ICumulativeTracker cumulativeTracker,
        ICacheService cache)
    {
        _db = db;
        _permissions = permissions;
        _audit = audit;
        _cumulativeTracker = cumulativeTracker;
        _cache = cache;
    }

    public async Task<DeactivateFreezeResult> Handle(
        DeactivateFreezeWithDiscardCommand req, CancellationToken ct)
    {
        await SetRlsSessionVariables(req.SpaceId, req.RequestingUserId, ct);

        // Require constraints.manage permission for all deactivation
        await _permissions.RequirePermissionAsync(
            req.RequestingUserId, req.SpaceId, Permissions.ConstraintsManage, ct);

        // If discarding, additionally require schedule.rollback permission
        if (req.DiscardFreezeChanges)
        {
            try
            {
                await _permissions.RequirePermissionAsync(
                    req.RequestingUserId, req.SpaceId, Permissions.ScheduleRollback, ct);
            }
            catch (UnauthorizedAccessException)
            {
                // Record denied attempt in audit log before re-throwing
                await _audit.LogAsync(
                    req.SpaceId,
                    req.RequestingUserId,
                    "permission_denied",
                    entityType: "home_leave_config",
                    entityId: null,
                    beforeJson: JsonSerializer.Serialize(new
                    {
                        group_id = req.GroupId,
                        action_attempted = "discard_freeze_changes",
                        required_permission = "schedule.rollback"
                    }),
                    afterJson: null,
                    ct: ct);

                throw;
            }
        }

        // Load HomeLeaveConfig — throw if freeze is not active
        var config = await _db.HomeLeaveConfigs
            .FirstOrDefaultAsync(c => c.GroupId == req.GroupId && c.SpaceId == req.SpaceId, ct)
            ?? throw new InvalidOperationException("Home leave configuration not found for this group.");

        if (!config.EmergencyFreezeActive)
            throw new InvalidOperationException("Emergency freeze is not active for this group.");

        // Perform discard if requested
        var (discardPerformed, discardVersionId, discardedChangeCount) =
            await PerformDiscardAsync(req, config, ct);

        // Capture freeze_started_at before clearing freeze state (DeactivateEmergencyFreeze nulls it)
        var freezeStartedAtForAudit = config.FreezeStartedAt;

        // Clear freeze state
        config.DeactivateEmergencyFreeze();
        await _db.SaveChangesAsync(ct);

        // Audit log for deactivation (when no discard was performed)
        if (!discardPerformed)
        {
            await _audit.LogAsync(
                req.SpaceId,
                req.RequestingUserId,
                "deactivate_freeze",
                entityType: "home_leave_config",
                entityId: config.Id,
                beforeJson: JsonSerializer.Serialize(new
                {
                    group_id = req.GroupId,
                    freeze_started_at = freezeStartedAtForAudit
                }),
                afterJson: JsonSerializer.Serialize(new
                {
                    discard_performed = false
                }),
                ct: ct);
        }

        return new DeactivateFreezeResult(
            config.Id,
            discardPerformed,
            discardVersionId,
            discardedChangeCount,
            BuildResultConfig(config));
    }

    /// <summary>
    /// Sets PostgreSQL session variables for RLS policies. No-op for non-relational (in-memory) databases.
    /// </summary>
    private async Task SetRlsSessionVariables(Guid spaceId, Guid userId, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                spaceId.ToString(),
                userId.ToString());
        }
    }

    /// <summary>
    /// Performs the discard operation: counts freeze-period changes, creates a rollback version,
    /// copies assignments, recomputes cumulative hours, and invalidates cache.
    /// Returns (discardPerformed, discardVersionId, discardedChangeCount).
    /// </summary>
    private async Task<(bool Performed, Guid? VersionId, int ChangeCount)> PerformDiscardAsync(
        DeactivateFreezeWithDiscardCommand req, HomeLeaveConfig config, CancellationToken ct)
    {
        if (!req.DiscardFreezeChanges)
            return (false, null, 0);

        var freezeStartedAt = config.FreezeStartedAt
            ?? throw new InvalidOperationException("Freeze started timestamp is missing.");

        // Find the most recent published version with PublishedAt < FreezeStartedAt
        var preFreezeBaseline = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId
                && v.Status == ScheduleVersionStatus.Published
                && v.PublishedAt != null
                && v.PublishedAt < freezeStartedAt)
            .OrderByDescending(v => v.PublishedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "No pre-freeze baseline version is available. Cannot discard freeze-period changes.");

        // Count freeze-period changes (shared logic with GetFreezePeriodChangesCountQuery)
        var discardedChangeCount = await CountFreezeChangesAsync(req.SpaceId, freezeStartedAt, ct);

        // If there are freeze-period changes, create a rollback version
        if (discardedChangeCount == 0)
            return (false, null, 0);

        // Wrap in a transaction to ensure atomicity (version + assignments saved together).
        // In-memory DB (used in tests) doesn't support transactions, so skip there.
        var useTransaction = _db.Database.IsRelational();
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

        if (useTransaction)
        {
            transaction = await _db.Database.BeginTransactionAsync(ct);
        }

        try
        {
            // Get next version number
            var nextVersion = await _db.ScheduleVersions
                .Where(v => v.SpaceId == req.SpaceId)
                .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
            nextVersion++;

            // Create new draft version as rollback from pre-freeze baseline
            var rollbackVersion = ScheduleVersion.CreateRollback(
                req.SpaceId, nextVersion, preFreezeBaseline.Id, req.RequestingUserId);

            _db.ScheduleVersions.Add(rollbackVersion);
            await _db.SaveChangesAsync(ct);

            // NOTE: For schedules with 10,000+ assignments, consider batching this operation
            // or using a SQL-level bulk insert to reduce memory pressure.
            var sourceAssignments = await _db.Assignments.AsNoTracking()
                .Where(a => a.ScheduleVersionId == preFreezeBaseline.Id && a.SpaceId == req.SpaceId)
                .ToListAsync(ct);

            var newAssignments = sourceAssignments.Select(a => Assignment.Create(
                req.SpaceId, rollbackVersion.Id, a.TaskSlotId, a.PersonId,
                AssignmentSource.Solver, "Rollback from pre-freeze baseline"))
                .ToList();

            _db.Assignments.AddRange(newAssignments);
            await _db.SaveChangesAsync(ct);

            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }

            // Recompute cumulative hours for affected persons
            var affectedPersonIds = sourceAssignments.Select(a => a.PersonId).Distinct().ToList();
            await Task.WhenAll(affectedPersonIds.Select(personId =>
                _cumulativeTracker.RecomputeForPersonAsync(req.SpaceId, personId, ct)));

            // Invalidate cache
            await _cache.RemoveByPatternAsync($"schedule:{req.SpaceId}:*", ct);
            await _cache.RemoveByPatternAsync($"status:{req.SpaceId}:*", ct);

            // Audit log for discard action
            await _audit.LogAsync(
                req.SpaceId,
                req.RequestingUserId,
                "discard_freeze_changes",
                entityType: "schedule_version",
                entityId: rollbackVersion.Id,
                beforeJson: JsonSerializer.Serialize(new
                {
                    group_id = req.GroupId,
                    freeze_started_at = freezeStartedAt,
                    change_count = discardedChangeCount
                }),
                afterJson: JsonSerializer.Serialize(new
                {
                    new_version_id = rollbackVersion.Id,
                    baseline_version_id = preFreezeBaseline.Id
                }),
                ct: ct);

            return (true, rollbackVersion.Id, discardedChangeCount);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Counts override assignments created during the freeze period in draft versions.
    /// This logic is shared with GetFreezePeriodChangesCountQuery (simple count variant).
    /// </summary>
    private async Task<int> CountFreezeChangesAsync(Guid spaceId, DateTime freezeStartedAt, CancellationToken ct)
    {
        var draftVersionIds = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Draft)
            .Select(v => v.Id)
            .ToListAsync(ct);

        if (draftVersionIds.Count == 0)
            return 0;

        return await _db.Assignments.AsNoTracking()
            .CountAsync(a => a.SpaceId == spaceId
                && draftVersionIds.Contains(a.ScheduleVersionId)
                && a.Source == AssignmentSource.Override
                && a.CreatedAt >= freezeStartedAt, ct);
    }

    /// <summary>
    /// Builds the HomeLeaveConfigResult DTO from the domain entity.
    /// </summary>
    private static HomeLeaveConfigResult BuildResultConfig(HomeLeaveConfig config)
    {
        return new HomeLeaveConfigResult(
            config.Id,
            config.GroupId,
            config.SpaceId,
            config.MinRestHours,
            config.EligibilityThresholdHours,
            config.LeaveCapacity,
            config.LeaveDurationHours,
            config.BalanceValue,
            config.MinPeopleAtBase,
            config.Mode.ToString().ToLowerInvariant(),
            config.BaseDays,
            config.HomeDays,
            config.EmergencyFreezeActive,
            config.EmergencyUseForScheduling,
            config.FreezeStartedAt,
            Feasibility: null,
            OptimalBaseDays: null,
            OptimalHomeDays: null,
            OptimalIsReduced: null);
    }
}
