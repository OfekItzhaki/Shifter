using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Queries;

public record GroupScheduleAssignmentDto(
    Guid Id,
    Guid PersonId,
    string PersonName,
    string TaskTypeName,
    DateTime SlotStartsAt,
    DateTime SlotEndsAt,
    string Source);

/// <summary>
/// Returns the published schedule assignments for members of a specific group.
/// Supports both legacy TaskSlots and the newer GroupTasks model.
/// </summary>
public record GetGroupScheduleQuery(Guid SpaceId, Guid GroupId) : IRequest<List<GroupScheduleAssignmentDto>>;

public class GetGroupScheduleQueryHandler : IRequestHandler<GetGroupScheduleQuery, List<GroupScheduleAssignmentDto>>
{
    private readonly AppDbContext _db;
    public GetGroupScheduleQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<GroupScheduleAssignmentDto>> Handle(GetGroupScheduleQuery req, CancellationToken ct)
    {
        // Get the latest published version
        var version = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (version is null) return [];

        // Get member person IDs for this group
        var memberIds = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.GroupId == req.GroupId && m.SpaceId == req.SpaceId)
            .Select(m => m.PersonId)
            .ToListAsync(ct);

        if (memberIds.Count == 0) return [];

        // Load raw assignments for those members
        var rawAssignments = await _db.Assignments.AsNoTracking()
            .Where(a => a.ScheduleVersionId == version.Id
                && a.SpaceId == req.SpaceId
                && memberIds.Contains(a.PersonId))
            .ToListAsync(ct);

        if (rawAssignments.Count == 0) return [];

        // Load people names
        var people = await _db.People.AsNoTracking()
            .Where(p => memberIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName ?? p.FullName, ct);

        var slotIds = rawAssignments.Select(a => a.TaskSlotId).ToHashSet();

        // Try legacy TaskSlots first
        var taskSlots = await _db.TaskSlots.AsNoTracking()
            .Where(s => slotIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var taskTypeIds = taskSlots.Values.Select(s => s.TaskTypeId).ToHashSet();
        var taskTypes = await _db.TaskTypes.AsNoTracking()
            .Where(t => taskTypeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        // Try GroupTasks for any slot IDs not found in task_slots
        // Only include tasks that belong to THIS group — prevents cross-group leakage
        var missingSlotIds = slotIds.Where(id => !taskSlots.ContainsKey(id)).ToHashSet();

        // Build shift-GUID → task lookup for this group's tasks (solver generates derived GUIDs)
        var shiftGuidToTask = new Dictionary<Guid, (string Name, DateTime StartsAt, DateTime EndsAt)>();
        if (missingSlotIds.Count > 0)
        {
            var thisGroupTasks = await _db.GroupTasks.AsNoTracking()
                .Where(t => t.GroupId == req.GroupId && t.SpaceId == req.SpaceId && t.IsActive)
                .ToListAsync(ct);

            foreach (var gt in thisGroupTasks)
            {
                if (gt.ShiftDurationMinutes < 1) continue;
                var shiftDuration = TimeSpan.FromMinutes(gt.ShiftDurationMinutes);
                var shiftStart = gt.StartsAt;
                var shiftIndex = 0;
                const int maxShifts = 10_000; // safety cap — prevents runaway loops on long horizons
                while (shiftStart + shiftDuration <= gt.EndsAt && shiftIndex < maxShifts)
                {
                    var shiftEnd = shiftStart + shiftDuration;
                    var shiftGuid = DeriveShiftGuid(gt.Id, shiftIndex);
                    if (missingSlotIds.Contains(shiftGuid))
                        shiftGuidToTask[shiftGuid] = (gt.Name, shiftStart, shiftEnd);
                    shiftStart = shiftEnd;
                    shiftIndex++;
                }
            }
        }

        var result = new List<GroupScheduleAssignmentDto>();

        foreach (var a in rawAssignments)
        {
            string taskName;
            DateTime startsAt;
            DateTime endsAt;

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
            }
            else
            {
                // Slot belongs to a different group or not found — skip
                continue;
            }

            var personName = people.TryGetValue(a.PersonId, out var pn) ? pn : "Unknown";
            result.Add(new GroupScheduleAssignmentDto(
                a.Id, a.PersonId, personName, taskName,
                startsAt, endsAt, a.Source.ToString()));
        }

        return result.OrderBy(r => r.SlotStartsAt).ToList();
    }

    private static Guid DeriveShiftGuid(Guid taskId, int shiftIndex)
    {
        var bytes = taskId.ToByteArray();
        var indexBytes = BitConverter.GetBytes(shiftIndex);
        for (int i = 0; i < 4; i++)
            bytes[12 + i] ^= indexBytes[i];
        return new Guid(bytes);
    }
}
