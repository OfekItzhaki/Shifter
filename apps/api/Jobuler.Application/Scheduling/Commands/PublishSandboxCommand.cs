using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Constraints;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobuler.Application.Scheduling.Commands;

public record PublishSandboxCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId,
    PublishSandboxRequest Request) : IRequest;

public class PublishSandboxCommandHandler : IRequestHandler<PublishSandboxCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IMediator _mediator;
    private readonly IAuditLogger _audit;
    private readonly ILogger<PublishSandboxCommandHandler> _logger;

    public PublishSandboxCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IMediator mediator,
        IAuditLogger audit,
        ILogger<PublishSandboxCommandHandler> logger)
    {
        _db = db;
        _permissions = permissions;
        _mediator = mediator;
        _audit = audit;
        _logger = logger;
    }

    public async Task Handle(PublishSandboxCommand cmd, CancellationToken ct)
    {
        // ── Permission check ─────────────────────────────────────────────────
        await _permissions.RequirePermissionAsync(
            cmd.RequestingUserId, cmd.SpaceId, Permissions.SchedulePublish, ct);

        var req = cmd.Request;

        // ── Verify version exists and is a draft ─────────────────────────────
        var version = await _db.ScheduleVersions
            .FirstOrDefaultAsync(v => v.Id == req.VersionId && v.SpaceId == cmd.SpaceId, ct)
            ?? throw new KeyNotFoundException("Schedule version not found.");

        if (version.Status == ScheduleVersionStatus.Published)
            throw new InvalidOperationException("Version is already published.");

        if (version.Status != ScheduleVersionStatus.Draft)
            throw new InvalidOperationException($"Cannot publish a version with status '{version.Status}'.");

        // ── Verify group exists ──────────────────────────────────────────────
        var group = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == cmd.GroupId && g.SpaceId == cmd.SpaceId && g.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException("Group not found.");

        // ── Capture before-snapshot for audit ────────────────────────────────
        var beforeSnapshot = await CaptureSnapshotAsync(cmd.SpaceId, cmd.GroupId, ct);

        // ── Begin transaction — all writes are atomic ────────────────────────
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Set RLS session variables
                if (_db.Database.IsRelational())
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                        cmd.SpaceId.ToString(), cmd.RequestingUserId.ToString());
                }

                // ── Persist task overrides ───────────────────────────────────
                await ApplyTaskOverridesAsync(cmd.SpaceId, cmd.GroupId, cmd.RequestingUserId, req.TaskOverrides, ct);

                // ── Persist constraint overrides ─────────────────────────────
                await ApplyConstraintOverridesAsync(cmd.SpaceId, cmd.RequestingUserId, req.ConstraintOverrides, ct);

                // ── Persist member exclusions ────────────────────────────────
                await ApplyMemberExclusionsAsync(cmd.SpaceId, cmd.GroupId, req.MemberExclusions, ct);

                // ── Persist settings overrides ───────────────────────────────
                await ApplySettingsOverridesAsync(cmd.SpaceId, cmd.GroupId, group, req.SettingsOverrides, ct);

                await _db.SaveChangesAsync(ct);

                // ── Delegate to PublishVersionCommand ─────────────────────────
                await _mediator.Send(new PublishVersionCommand(cmd.SpaceId, req.VersionId, cmd.RequestingUserId), ct);

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });

        // ── Audit log with before/after snapshot ─────────────────────────────
        var afterSnapshot = await CaptureSnapshotAsync(cmd.SpaceId, cmd.GroupId, ct);

        await _audit.LogAsync(
            cmd.SpaceId,
            cmd.RequestingUserId,
            "publish_sandbox",
            "schedule_version",
            req.VersionId,
            beforeJson: JsonSerializer.Serialize(beforeSnapshot),
            afterJson: JsonSerializer.Serialize(afterSnapshot),
            ct: ct);

        _logger.LogInformation(
            "Sandbox published for version {VersionId} in group {GroupId} by user {UserId}",
            req.VersionId, cmd.GroupId, cmd.RequestingUserId);
    }

    // ── Task overrides ───────────────────────────────────────────────────────────

    private async Task ApplyTaskOverridesAsync(
        Guid spaceId, Guid groupId, Guid userId,
        List<TaskOverrideDto> overrides, CancellationToken ct)
    {
        foreach (var taskOverride in overrides)
        {
            var action = taskOverride.Action.ToLowerInvariant();

            switch (action)
            {
                case "add":
                    var newTask = GroupTask.Create(
                        spaceId, groupId,
                        taskOverride.Name!,
                        taskOverride.StartsAt!.Value,
                        taskOverride.EndsAt!.Value,
                        taskOverride.ShiftDurationMinutes ?? 240,
                        taskOverride.RequiredHeadcount ?? 1,
                        ParseBurdenLevel(taskOverride.BurdenLevel),
                        allowsDoubleShift: false,
                        allowsOverlap: false,
                        createdByUserId: userId,
                        qualificationRequirements: taskOverride.RequiredQualificationNames?
                            .Select(name => new QualificationRequirement(name, 1, true))
                            .ToList());
                    _db.GroupTasks.Add(newTask);
                    break;

                case "edit":
                    var existingTask = await _db.GroupTasks
                        .FirstOrDefaultAsync(t => t.Id == taskOverride.ExistingTaskId
                            && t.SpaceId == spaceId
                            && t.GroupId == groupId
                            && t.IsActive, ct)
                        ?? throw new KeyNotFoundException($"Task {taskOverride.ExistingTaskId} not found.");

                    existingTask.Update(
                        taskOverride.Name ?? existingTask.Name,
                        taskOverride.StartsAt ?? existingTask.StartsAt,
                        taskOverride.EndsAt ?? existingTask.EndsAt,
                        taskOverride.ShiftDurationMinutes ?? existingTask.ShiftDurationMinutes,
                        taskOverride.RequiredHeadcount ?? existingTask.RequiredHeadcount,
                        taskOverride.BurdenLevel != null
                            ? ParseBurdenLevel(taskOverride.BurdenLevel)
                            : existingTask.BurdenLevel,
                        existingTask.AllowsDoubleShift,
                        existingTask.AllowsOverlap,
                        userId,
                        existingTask.DailyStartTime,
                        existingTask.DailyEndTime,
                        taskOverride.RequiredQualificationNames != null
                            ? taskOverride.RequiredQualificationNames
                                .Select(name => new QualificationRequirement(name, 1, true))
                                .ToList()
                            : existingTask.QualificationRequirements);
                    break;

                case "remove":
                    var taskToRemove = await _db.GroupTasks
                        .FirstOrDefaultAsync(t => t.Id == taskOverride.ExistingTaskId
                            && t.SpaceId == spaceId
                            && t.GroupId == groupId, ct)
                        ?? throw new KeyNotFoundException($"Task {taskOverride.ExistingTaskId} not found.");

                    taskToRemove.Deactivate(userId);
                    break;
            }
        }
    }

    // ── Constraint overrides ─────────────────────────────────────────────────────

    private async Task ApplyConstraintOverridesAsync(
        Guid spaceId, Guid userId,
        List<ConstraintOverrideDto> overrides, CancellationToken ct)
    {
        foreach (var constraintOverride in overrides)
        {
            var action = constraintOverride.Action.ToLowerInvariant();

            switch (action)
            {
                case "add":
                    var scopeType = Enum.Parse<ConstraintScopeType>(constraintOverride.ScopeType!, true);
                    var severity = Enum.Parse<ConstraintSeverity>(constraintOverride.Severity!, true);
                    var payloadJson = constraintOverride.Payload != null
                        ? JsonSerializer.Serialize(constraintOverride.Payload)
                        : "{}";

                    var newConstraint = ConstraintRule.Create(
                        spaceId, scopeType, constraintOverride.ScopeId,
                        severity, constraintOverride.RuleType!, payloadJson,
                        userId);
                    _db.ConstraintRules.Add(newConstraint);
                    break;

                case "edit":
                    var existingConstraint = await _db.ConstraintRules
                        .FirstOrDefaultAsync(c => c.Id == constraintOverride.ExistingConstraintId
                            && c.SpaceId == spaceId
                            && c.IsActive, ct)
                        ?? throw new KeyNotFoundException($"Constraint {constraintOverride.ExistingConstraintId} not found.");

                    var editPayloadJson = constraintOverride.Payload != null
                        ? JsonSerializer.Serialize(constraintOverride.Payload)
                        : existingConstraint.RulePayloadJson;

                    ConstraintSeverity? editSeverity = constraintOverride.Severity != null
                        ? Enum.Parse<ConstraintSeverity>(constraintOverride.Severity, true)
                        : null;

                    existingConstraint.Update(editPayloadJson, editSeverity, null, userId);
                    break;

                case "remove":
                    var constraintToRemove = await _db.ConstraintRules
                        .FirstOrDefaultAsync(c => c.Id == constraintOverride.ExistingConstraintId
                            && c.SpaceId == spaceId, ct)
                        ?? throw new KeyNotFoundException($"Constraint {constraintOverride.ExistingConstraintId} not found.");

                    constraintToRemove.Deactivate(userId);
                    break;
            }
        }
    }

    // ── Member exclusions ────────────────────────────────────────────────────────

    private async Task ApplyMemberExclusionsAsync(
        Guid spaceId, Guid groupId,
        List<Guid> memberExclusions, CancellationToken ct)
    {
        if (memberExclusions.Count == 0)
            return;

        // Find active memberships for the excluded people in this group
        var memberships = await _db.GroupMemberships
            .Where(m => m.SpaceId == spaceId
                && m.GroupId == groupId
                && memberExclusions.Contains(m.PersonId))
            .ToListAsync(ct);

        // Remove memberships (opt-out) — this effectively excludes them from future scheduling
        foreach (var membership in memberships)
        {
            _db.GroupMemberships.Remove(membership);
        }
    }

    // ── Settings overrides ───────────────────────────────────────────────────────

    private async Task ApplySettingsOverridesAsync(
        Guid spaceId, Guid groupId, Group group,
        SettingsOverrideDto? settings, CancellationToken ct)
    {
        if (settings is null)
            return;

        // Apply min rest between shifts to group
        if (settings.MinRestBetweenShiftsHours.HasValue)
        {
            group.SetMinRestBetweenShifts(settings.MinRestBetweenShiftsHours.Value);
        }

        // Apply home-leave parameters
        var homeLeaveConfig = await _db.HomeLeaveConfigs
            .FirstOrDefaultAsync(h => h.SpaceId == spaceId && h.GroupId == groupId, ct);

        if (homeLeaveConfig != null)
        {
            var eligibility = settings.EligibilityThresholdHours.HasValue
                ? (decimal)settings.EligibilityThresholdHours.Value
                : homeLeaveConfig.EligibilityThresholdHours;

            var leaveDuration = settings.LeaveDurationHours.HasValue
                ? (decimal)settings.LeaveDurationHours.Value
                : homeLeaveConfig.LeaveDurationHours;

            var leaveCapacity = settings.LeaveCapacity ?? homeLeaveConfig.LeaveCapacity;
            var balanceValue = settings.BalanceValue ?? homeLeaveConfig.BalanceValue;
            var minPeopleAtBase = settings.MinPeopleAtBase ?? homeLeaveConfig.MinPeopleAtBase;

            homeLeaveConfig.Update(
                homeLeaveConfig.MinRestHours,
                eligibility,
                leaveCapacity,
                leaveDuration,
                balanceValue,
                minPeopleAtBase: minPeopleAtBase);
        }
        else if (settings.MinPeopleAtBase.HasValue && group.IsClosedBase)
        {
            // If no home-leave config exists but min people at base is set for a closed base,
            // we only update the group's min rest setting (already handled above)
            _logger.LogWarning(
                "Settings override includes home-leave params but no HomeLeaveConfig exists for group {GroupId}",
                groupId);
        }
    }

    // ── Snapshot helpers ─────────────────────────────────────────────────────────

    private async Task<SandboxPublishSnapshot> CaptureSnapshotAsync(
        Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var tasks = await _db.GroupTasks
            .AsNoTracking()
            .Where(t => t.SpaceId == spaceId && t.GroupId == groupId && t.IsActive)
            .Select(t => new TaskSnapshotEntry(t.Id, t.Name, t.StartsAt, t.EndsAt, t.RequiredHeadcount))
            .ToListAsync(ct);

        var constraints = await _db.ConstraintRules
            .AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.IsActive)
            .Select(c => new ConstraintSnapshotEntry(c.Id, c.RuleType, c.Severity.ToString(), c.RulePayloadJson))
            .ToListAsync(ct);

        var memberCount = await _db.GroupMemberships
            .AsNoTracking()
            .CountAsync(m => m.SpaceId == spaceId && m.GroupId == groupId, ct);

        var group = await _db.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId && g.SpaceId == spaceId, ct);

        return new SandboxPublishSnapshot(
            TaskCount: tasks.Count,
            Tasks: tasks,
            ConstraintCount: constraints.Count,
            Constraints: constraints,
            MemberCount: memberCount,
            MinRestBetweenShiftsHours: group?.MinRestBetweenShiftsHours ?? 0);
    }

    private static TaskBurdenLevel ParseBurdenLevel(string? burdenLevel) =>
        burdenLevel != null
            ? Enum.Parse<TaskBurdenLevel>(burdenLevel, true)
            : TaskBurdenLevel.Normal;
}

// ── Audit snapshot records ───────────────────────────────────────────────────────

internal record SandboxPublishSnapshot(
    int TaskCount,
    List<TaskSnapshotEntry> Tasks,
    int ConstraintCount,
    List<ConstraintSnapshotEntry> Constraints,
    int MemberCount,
    int MinRestBetweenShiftsHours);

internal record TaskSnapshotEntry(
    Guid Id, string Name, DateTime StartsAt, DateTime EndsAt, int RequiredHeadcount);

internal record ConstraintSnapshotEntry(
    Guid Id, string RuleType, string Severity, string PayloadJson);
