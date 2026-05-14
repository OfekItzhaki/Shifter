using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

public record MyAssignmentDto(
    Guid Id,
    Guid GroupId,
    string GroupName,
    string TaskTypeName,
    DateTime SlotStartsAt,
    DateTime SlotEndsAt,
    string Source);

/// <summary>
/// Returns all assignments for the current user across all groups they belong to,
/// filtered by a date range.
/// </summary>
public record GetMyAssignmentsQuery(
    Guid SpaceId,
    Guid UserId,
    DateTime From,
    DateTime To) : IRequest<List<MyAssignmentDto>>;

public class GetMyAssignmentsQueryHandler : IRequestHandler<GetMyAssignmentsQuery, List<MyAssignmentDto>>
{
    private readonly AppDbContext _db;
    public GetMyAssignmentsQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<MyAssignmentDto>> Handle(GetMyAssignmentsQuery req, CancellationToken ct)
    {
        // Find the person linked to this user in this space
        var person = await _db.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SpaceId == req.SpaceId && p.LinkedUserId == req.UserId, ct);

        if (person is null) return [];

        // Get latest published version
        var version = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (version is null) return [];

        // Get all groups this person belongs to
        var groupIds = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.PersonId == person.Id && m.SpaceId == req.SpaceId)
            .Select(m => m.GroupId)
            .ToListAsync(ct);

        // Load all assignments for this person in this version
        var rawAssignments = await _db.Assignments.AsNoTracking()
            .Where(a => a.ScheduleVersionId == version.Id
                && a.SpaceId == req.SpaceId
                && a.PersonId == person.Id)
            .ToListAsync(ct);

        if (rawAssignments.Count == 0) return [];

        var slotIds = rawAssignments.Select(a => a.TaskSlotId).ToHashSet();

        // ── Legacy task_slots lookup ──────────────────────────────────────────
        var taskSlots = await _db.TaskSlots.AsNoTracking()
            .Where(s => slotIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var taskTypeIds = taskSlots.Values.Select(s => s.TaskTypeId).ToHashSet();
        var taskTypes = await _db.TaskTypes.AsNoTracking()
            .Where(t => taskTypeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        // ── GroupTask shift-GUID reverse lookup ───────────────────────────────
        // Solver-generated assignments use derived shift GUIDs, not real task_slot rows.
        var missingIds = slotIds.Where(id => !taskSlots.ContainsKey(id)).ToHashSet();
        var shiftGuidToTask = new Dictionary<Guid, (string Name, DateTime StartsAt, DateTime EndsAt, Guid GroupId)>();

        if (missingIds.Count > 0)
        {
            var allGroupTasks = await _db.GroupTasks.AsNoTracking()
                .Where(t => t.SpaceId == req.SpaceId)
                .ToListAsync(ct);

            foreach (var gt in allGroupTasks)
            {
                if (gt.ShiftDurationMinutes < 1) continue;
                var shiftDuration = TimeSpan.FromMinutes(gt.ShiftDurationMinutes);
                var shiftStart = gt.StartsAt;
                var shiftIndex = 0;
                while (shiftStart + shiftDuration <= gt.EndsAt)
                {
                    var shiftEnd = shiftStart + shiftDuration;
                    var shiftGuid = ShiftGuidHelper.DeriveShiftGuid(gt.Id, shiftIndex);
                    if (missingIds.Contains(shiftGuid))
                        shiftGuidToTask[shiftGuid] = (gt.Name, shiftStart, shiftEnd, gt.GroupId);
                    shiftStart = shiftEnd;
                    shiftIndex++;
                }
            }
        }

        // ── Map group names ───────────────────────────────────────────────────
        var groups = await _db.Groups.AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var firstGroupId = groupIds.FirstOrDefault();
        var firstGroupName = firstGroupId != Guid.Empty && groups.TryGetValue(firstGroupId, out var gn) ? gn : "—";

        // ── Build result, filtering by date range ─────────────────────────────
        var result = new List<MyAssignmentDto>();
        foreach (var a in rawAssignments)
        {
            string taskName;
            DateTime startsAt;
            DateTime endsAt;
            Guid groupId = firstGroupId;
            string groupName = firstGroupName;

            if (taskSlots.TryGetValue(a.TaskSlotId, out var slot))
            {
                taskName = taskTypes.TryGetValue(slot.TaskTypeId, out var tn) ? tn : "Unknown";
                startsAt = slot.StartsAt;
                endsAt = slot.EndsAt;
            }
            else if (shiftGuidToTask.TryGetValue(a.TaskSlotId, out var shiftInfo))
            {
                taskName = shiftInfo.Name;
                startsAt = shiftInfo.StartsAt;
                endsAt = shiftInfo.EndsAt;
                // Use the group the task belongs to if the person is a member
                if (groups.TryGetValue(shiftInfo.GroupId, out var shiftGroupName))
                {
                    groupId = shiftInfo.GroupId;
                    groupName = shiftGroupName;
                }
            }
            else continue;

            // Apply date range filter
            if (startsAt < req.From || startsAt >= req.To) continue;

            // Convert UTC to Israel local time (+3h) for display
            var localStartsAt = DateTime.SpecifyKind(startsAt.AddHours(3), DateTimeKind.Unspecified);
            var localEndsAt = DateTime.SpecifyKind(endsAt.AddHours(3), DateTimeKind.Unspecified);
            result.Add(new MyAssignmentDto(
                a.Id, groupId, groupName, taskName,
                localStartsAt, localEndsAt, a.Source.ToString()));
        }

        return result.OrderBy(x => x.SlotStartsAt).ToList();
    }

    /// <summary>
    /// Derives a deterministic shift GUID from a GroupTask ID and shift index.
    /// Must match the logic in SolverPayloadNormalizer.
    /// </summary>
    private static class ShiftGuidHelper
    {
        internal static Guid DeriveShiftGuid(Guid taskId, int shiftIndex)
        {
            var bytes = taskId.ToByteArray();
            var indexBytes = BitConverter.GetBytes(shiftIndex);
            for (int i = 0; i < 4; i++)
                bytes[12 + i] ^= indexBytes[i];
            return new Guid(bytes);
        }
    }
}
