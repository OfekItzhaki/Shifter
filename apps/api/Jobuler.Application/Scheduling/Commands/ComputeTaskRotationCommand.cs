using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Commands;

/// <summary>
/// Recomputes task rotation progress for all members of an army-template group.
/// Called after UpdateFairnessCountersCommand when a schedule is published.
/// </summary>
public record ComputeTaskRotationCommand(
    Guid SpaceId,
    Guid GroupId) : IRequest;

public class ComputeTaskRotationCommandHandler
    : IRequestHandler<ComputeTaskRotationCommand>
{
    private readonly AppDbContext _db;

    public ComputeTaskRotationCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(ComputeTaskRotationCommand req, CancellationToken ct)
    {
        // 1. Load all active group tasks for the group → distinct task type IDs
        //    In this system, each GroupTask IS a task type — its ID is the task type ID.
        var groupTaskIds = await _db.GroupTasks.AsNoTracking()
            .Where(t => t.SpaceId == req.SpaceId && t.GroupId == req.GroupId && t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (groupTaskIds.Count == 0)
            return;

        var groupTaskIdSet = groupTaskIds.ToHashSet();

        // 2. Load all group members
        var memberIds = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.SpaceId == req.SpaceId && m.GroupId == req.GroupId)
            .Select(m => m.PersonId)
            .ToListAsync(ct);

        if (memberIds.Count == 0)
            return;

        // 3. Load assignments from published schedule versions for this space
        var publishedVersionIds = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .Select(v => v.Id)
            .ToListAsync(ct);

        if (publishedVersionIds.Count == 0)
            return;

        // Load all assignments for published versions, joined with task slots to get task type IDs
        // We need: PersonId → set of distinct task type IDs they've been assigned to
        var assignmentData = await _db.Assignments.AsNoTracking()
            .Where(a => a.SpaceId == req.SpaceId &&
                        publishedVersionIds.Contains(a.ScheduleVersionId) &&
                        memberIds.Contains(a.PersonId))
            .Join(_db.TaskSlots.AsNoTracking(),
                a => a.TaskSlotId, s => s.Id,
                (a, s) => new { a.PersonId, s.TaskTypeId })
            .ToListAsync(ct);

        // Also include assignments from GroupTask-generated slots (where TaskTypeId = GroupTask.Id)
        // GroupTask slots use DeriveShiftGuid so they won't be in TaskSlots table.
        // Instead, we look at assignments where the task_slot_id maps to a group task.
        // The SolverPayloadNormalizer uses GroupTask.Id as the TaskTypeId for generated slots.
        // So we filter assignmentData to only include task types that are in our group's task set.

        // 4. For each member: compute distinct task types completed (within this group's tasks)
        foreach (var personId in memberIds)
        {
            var completedTaskTypeIds = assignmentData
                .Where(a => a.PersonId == personId && groupTaskIdSet.Contains(a.TaskTypeId))
                .Select(a => a.TaskTypeId)
                .Distinct()
                .ToList();

            // Determine how many task types this person is qualified for
            // A person is qualified for a task type if they have the required qualifications
            // (or if the task has no qualification requirements)
            var personQualifications = await _db.PersonQualifications.AsNoTracking()
                .Where(q => q.SpaceId == req.SpaceId && q.PersonId == personId && q.IsActive)
                .Select(q => q.Qualification)
                .ToListAsync(ct);

            var qualifiedTaskTypes = await _db.GroupTasks.AsNoTracking()
                .Where(t => t.SpaceId == req.SpaceId && t.GroupId == req.GroupId && t.IsActive)
                .ToListAsync(ct);

            var qualifiedCount = qualifiedTaskTypes.Count(task =>
            {
                var requiredQuals = task.RequiredQualificationNames;
                if (requiredQuals.Count == 0)
                    return true; // No requirements = everyone is qualified
                return requiredQuals.All(rq => personQualifications.Contains(rq));
            });

            if (qualifiedCount == 0)
                continue;

            // Filter completed to only include qualified task types
            var qualifiedTaskIds = qualifiedTaskTypes
                .Where(task =>
                {
                    var requiredQuals = task.RequiredQualificationNames;
                    if (requiredQuals.Count == 0)
                        return true;
                    return requiredQuals.All(rq => personQualifications.Contains(rq));
                })
                .Select(t => t.Id)
                .ToHashSet();

            var qualifiedCompleted = completedTaskTypeIds
                .Where(id => qualifiedTaskIds.Contains(id))
                .ToList();

            // 5. Compute cycle and handle reset
            var cycleNumber = 1;
            var currentCompleted = qualifiedCompleted;

            // Load existing progress to preserve cycle number
            var existing = await _db.TaskRotationProgress
                .FirstOrDefaultAsync(p => p.SpaceId == req.SpaceId &&
                    p.PersonId == personId && p.GroupId == req.GroupId, ct);

            if (existing is not null)
                cycleNumber = existing.CycleNumber;

            // Check if all qualified types are completed → cycle reset
            if (qualifiedCompleted.Count >= qualifiedCount && qualifiedCount > 0)
            {
                // Increment cycle, keep only the most recently completed task type
                cycleNumber++;
                var lastAssignment = assignmentData
                    .Where(a => a.PersonId == personId && qualifiedTaskIds.Contains(a.TaskTypeId))
                    .LastOrDefault();

                currentCompleted = lastAssignment is not null
                    ? new List<Guid> { lastAssignment.TaskTypeId }
                    : new List<Guid>();
            }

            // 6. Upsert TaskRotationProgress record
            if (existing is null)
            {
                var progress = TaskRotationProgress.Create(req.SpaceId, personId, req.GroupId, qualifiedCount);
                progress.SetCompletedTaskTypes(currentCompleted, cycleNumber);
                _db.TaskRotationProgress.Add(progress);
            }
            else
            {
                existing.UpdateQualifiedCount(qualifiedCount);
                existing.SetCompletedTaskTypes(currentCompleted, cycleNumber);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
