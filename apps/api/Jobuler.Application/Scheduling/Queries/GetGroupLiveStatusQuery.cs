using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record MemberLiveStatusDto(
    string PersonId,
    string DisplayName,
    string Status,          // on_mission | at_home | blocked | free_in_base
    string? TaskName,
    DateTime? SlotEndsAt,
    string? Location);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetGroupLiveStatusQuery(
    Guid SpaceId,
    Guid GroupId) : IRequest<List<MemberLiveStatusDto>>;

public class GetGroupLiveStatusQueryHandler
    : IRequestHandler<GetGroupLiveStatusQuery, List<MemberLiveStatusDto>>
{
    private readonly AppDbContext _db;
    public GetGroupLiveStatusQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<MemberLiveStatusDto>> Handle(
        GetGroupLiveStatusQuery req, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // ── Load group members ────────────────────────────────────────────────
        var members = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.GroupId == req.GroupId && m.SpaceId == req.SpaceId)
            .Join(_db.People.AsNoTracking(),
                m => m.PersonId,
                p => p.Id,
                (m, p) => new { p.Id, Name = p.DisplayName ?? p.FullName })
            .ToListAsync(ct);

        if (members.Count == 0)
            return new List<MemberLiveStatusDto>();

        var memberIds = members.Select(m => m.Id).ToHashSet();

        // ── Load active presence windows (manual overrides win) ───────────────
        var presenceWindows = await _db.PresenceWindows.AsNoTracking()
            .Where(pw => pw.SpaceId == req.SpaceId
                && memberIds.Contains(pw.PersonId)
                && pw.StartsAt <= now
                && pw.EndsAt >= now)
            .ToListAsync(ct);

        var presenceByPerson = presenceWindows
            .GroupBy(pw => pw.PersonId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(pw => pw.StartsAt).First());

        // ── Load current published assignments ────────────────────────────────
        var publishedVersion = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        // Map: personId → (taskName, slotEndsAt)
        var activeAssignments = new Dictionary<Guid, (string TaskName, DateTime SlotEndsAt)>();

        if (publishedVersion is not null)
        {
            // Load assignments active right now
            var rawAssignments = await _db.Assignments.AsNoTracking()
                .Where(a => a.ScheduleVersionId == publishedVersion.Id
                    && a.SpaceId == req.SpaceId
                    && memberIds.Contains(a.PersonId))
                .ToListAsync(ct);

            var slotIds = rawAssignments.Select(a => a.TaskSlotId).ToHashSet();

            // Resolve task names from legacy task_slots
            var taskSlots = await _db.TaskSlots.AsNoTracking()
                .Where(s => slotIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, ct);

            var taskTypeIds = taskSlots.Values.Select(s => s.TaskTypeId).ToHashSet();
            var taskTypes = await _db.TaskTypes.AsNoTracking()
                .Where(t => taskTypeIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

            // Resolve task names from group tasks (shift GUIDs)
            var missingSlotIds = slotIds.Where(id => !taskSlots.ContainsKey(id)).ToHashSet();
            var shiftGuidToTask = new Dictionary<Guid, (string Name, DateTime StartsAt, DateTime EndsAt)>();

            if (missingSlotIds.Count > 0)
            {
                var groupTasks = await _db.GroupTasks.AsNoTracking()
                    .Where(t => t.SpaceId == req.SpaceId)
                    .ToListAsync(ct);

                foreach (var gt in groupTasks)
                {
                    if (gt.ShiftDurationMinutes < 1) continue;
                    var shiftDuration = TimeSpan.FromMinutes(gt.ShiftDurationMinutes);
                    var shiftStart = gt.StartsAt;
                    var shiftIndex = 0;
                    while (shiftStart + shiftDuration <= gt.EndsAt)
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

            // Build active assignment map for members currently on a slot
            foreach (var a in rawAssignments)
            {
                string taskName;
                DateTime slotEndsAt;

                if (taskSlots.TryGetValue(a.TaskSlotId, out var slot))
                {
                    // Only include if the slot is active right now
                    if (slot.StartsAt > now || slot.EndsAt < now) continue;
                    taskName = taskTypes.TryGetValue(slot.TaskTypeId, out var tn) ? tn : "Unknown";
                    slotEndsAt = slot.EndsAt;
                }
                else if (shiftGuidToTask.TryGetValue(a.TaskSlotId, out var shiftInfo))
                {
                    if (shiftInfo.StartsAt > now || shiftInfo.EndsAt < now) continue;
                    taskName = shiftInfo.Name;
                    slotEndsAt = shiftInfo.EndsAt;
                }
                else continue;

                activeAssignments[a.PersonId] = (taskName, slotEndsAt);
            }
        }

        // ── Build result ──────────────────────────────────────────────────────
        var result = new List<MemberLiveStatusDto>();

        foreach (var member in members)
        {
            string status;
            string? taskName = null;
            DateTime? slotEndsAt = null;
            string? location = null;

            if (presenceByPerson.TryGetValue(member.Id, out var pw))
            {
                // Manual presence window overrides assignment-based status
                status = pw.State.ToString().ToLower() switch
                {
                    "blocked"      => "blocked",
                    "at_home"      => "at_home",
                    "on_mission"   => "on_mission",
                    _              => "free_in_base"
                };
            }
            else if (activeAssignments.TryGetValue(member.Id, out var assignment))
            {
                status = "on_mission";
                taskName = assignment.TaskName;
                slotEndsAt = assignment.SlotEndsAt;
            }
            else
            {
                status = "free_in_base";
            }

            result.Add(new MemberLiveStatusDto(
                member.Id.ToString(),
                member.Name,
                status,
                taskName,
                slotEndsAt,
                location));
        }

        return result.OrderBy(r => r.DisplayName).ToList();
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
