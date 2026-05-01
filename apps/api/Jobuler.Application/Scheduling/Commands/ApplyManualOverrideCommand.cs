using Jobuler.Application.Common;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Commands;

// ── Apply override ────────────────────────────────────────────────────────────

/// <summary>
/// Manually override the assignment(s) for a specific slot.
/// Creates a new draft version (cloned from the current published version) if one
/// does not already exist, then replaces the slot's assignments with the supplied
/// person IDs, marking them as Source = Override.
/// Returns the draft version ID.
/// </summary>
public record ApplyManualOverrideCommand(
    Guid SpaceId,
    Guid SlotId,
    List<Guid> NewPersonIds,
    Guid RequestingUserId) : IRequest<Guid>;

public class ApplyManualOverrideCommandHandler : IRequestHandler<ApplyManualOverrideCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;

    public ApplyManualOverrideCommandHandler(AppDbContext db, IAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Guid> Handle(ApplyManualOverrideCommand req, CancellationToken ct)
    {
        // ── Permission check ──────────────────────────────────────────────────
        // Handled at the controller level via IPermissionService.

        // ── Find or create a draft version ───────────────────────────────────
        var draft = await _db.ScheduleVersions
            .FirstOrDefaultAsync(v => v.SpaceId == req.SpaceId
                && v.Status == ScheduleVersionStatus.Draft, ct);

        if (draft is null)
        {
            // Clone from the current published version
            var published = await _db.ScheduleVersions
                .FirstOrDefaultAsync(v => v.SpaceId == req.SpaceId
                    && v.Status == ScheduleVersionStatus.Published, ct);

            var nextVersion = await _db.ScheduleVersions
                .Where(v => v.SpaceId == req.SpaceId)
                .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
            nextVersion++;

            draft = ScheduleVersion.CreateDraft(
                req.SpaceId, nextVersion,
                baselineVersionId: published?.Id,
                sourceRunId: null,
                createdByUserId: req.RequestingUserId);

            _db.ScheduleVersions.Add(draft);
            await _db.SaveChangesAsync(ct);

            // Clone all assignments from the published version into the draft
            if (published is not null)
            {
                var sourceAssignments = await _db.Assignments.AsNoTracking()
                    .Where(a => a.ScheduleVersionId == published.Id && a.SpaceId == req.SpaceId)
                    .ToListAsync(ct);

                var cloned = sourceAssignments.Select(a => Assignment.Create(
                    req.SpaceId, draft.Id, a.TaskSlotId, a.PersonId,
                    a.Source, a.ChangeReasonSummary)).ToList();

                _db.Assignments.AddRange(cloned);
                await _db.SaveChangesAsync(ct);
            }
        }

        // ── Capture previous assignees for audit log ──────────────────────────
        var previousAssignees = await _db.Assignments
            .Where(a => a.ScheduleVersionId == draft.Id
                && a.SpaceId == req.SpaceId
                && a.TaskSlotId == req.SlotId)
            .Select(a => a.PersonId)
            .ToListAsync(ct);

        // ── Remove existing assignments for this slot in the draft ────────────
        var existing = await _db.Assignments
            .Where(a => a.ScheduleVersionId == draft.Id
                && a.SpaceId == req.SpaceId
                && a.TaskSlotId == req.SlotId)
            .ToListAsync(ct);

        _db.Assignments.RemoveRange(existing);

        // ── Insert new override assignments ───────────────────────────────────
        foreach (var personId in req.NewPersonIds)
        {
            _db.Assignments.Add(Assignment.Create(
                req.SpaceId, draft.Id, req.SlotId, personId,
                AssignmentSource.Override,
                $"Manual override by user {req.RequestingUserId}"));
        }

        await _db.SaveChangesAsync(ct);

        // ── Audit log ─────────────────────────────────────────────────────────
        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            slot_id = req.SlotId,
            previous_person_ids = previousAssignees
        });
        var afterJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            slot_id = req.SlotId,
            new_person_ids = req.NewPersonIds,
            draft_version_id = draft.Id
        });

        await _audit.LogAsync(
            req.SpaceId, req.RequestingUserId,
            "manual_override_assignment",
            "assignment", req.SlotId,
            beforeJson: beforeJson,
            afterJson: afterJson,
            ct: ct);

        return draft.Id;
    }
}

// ── Remove override (clear slot) ─────────────────────────────────────────────

/// <summary>
/// Removes all assignments for a slot in the draft version, recording the slot
/// as explicitly unassigned (no assignment row). Creates a draft if needed.
/// </summary>
public record RemoveManualOverrideCommand(
    Guid SpaceId,
    Guid SlotId,
    Guid RequestingUserId) : IRequest<Guid>;

public class RemoveManualOverrideCommandHandler : IRequestHandler<RemoveManualOverrideCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;

    public RemoveManualOverrideCommandHandler(AppDbContext db, IAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Guid> Handle(RemoveManualOverrideCommand req, CancellationToken ct)
    {
        // Find or create draft (same logic as apply)
        var draft = await _db.ScheduleVersions
            .FirstOrDefaultAsync(v => v.SpaceId == req.SpaceId
                && v.Status == ScheduleVersionStatus.Draft, ct);

        if (draft is null)
        {
            var published = await _db.ScheduleVersions
                .FirstOrDefaultAsync(v => v.SpaceId == req.SpaceId
                    && v.Status == ScheduleVersionStatus.Published, ct);

            var nextVersion = await _db.ScheduleVersions
                .Where(v => v.SpaceId == req.SpaceId)
                .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
            nextVersion++;

            draft = ScheduleVersion.CreateDraft(
                req.SpaceId, nextVersion,
                baselineVersionId: published?.Id,
                sourceRunId: null,
                createdByUserId: req.RequestingUserId);

            _db.ScheduleVersions.Add(draft);
            await _db.SaveChangesAsync(ct);

            if (published is not null)
            {
                var sourceAssignments = await _db.Assignments.AsNoTracking()
                    .Where(a => a.ScheduleVersionId == published.Id && a.SpaceId == req.SpaceId)
                    .ToListAsync(ct);

                var cloned = sourceAssignments.Select(a => Assignment.Create(
                    req.SpaceId, draft.Id, a.TaskSlotId, a.PersonId,
                    a.Source, a.ChangeReasonSummary)).ToList();

                _db.Assignments.AddRange(cloned);
                await _db.SaveChangesAsync(ct);
            }
        }

        // Remove all assignments for this slot in the draft
        var existing = await _db.Assignments
            .Where(a => a.ScheduleVersionId == draft.Id
                && a.SpaceId == req.SpaceId
                && a.TaskSlotId == req.SlotId)
            .ToListAsync(ct);

        var previousPersonIds = existing.Select(a => a.PersonId).ToList();
        _db.Assignments.RemoveRange(existing);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            req.SpaceId, req.RequestingUserId,
            "remove_override_assignment",
            "assignment", req.SlotId,
            beforeJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                slot_id = req.SlotId,
                previous_person_ids = previousPersonIds
            }),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                slot_id = req.SlotId,
                new_person_ids = Array.Empty<Guid>(),
                draft_version_id = draft.Id
            }),
            ct: ct);

        return draft.Id;
    }
}
