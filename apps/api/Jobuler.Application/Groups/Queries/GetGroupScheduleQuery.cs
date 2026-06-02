using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Queries;

public record GroupScheduleAssignmentDto(
    Guid Id,
    Guid PersonId,
    string PersonName,
    string? PersonPhoneNumber,
    string TaskTypeName,
    DateTime SlotStartsAt,
    DateTime SlotEndsAt,
    string Source);

/// <summary>
/// Wraps the schedule assignments together with task configuration data,
/// allowing the frontend to display task info badges without additional API calls.
/// </summary>
public record GroupScheduleResponseDto(
    List<GroupScheduleAssignmentDto> Assignments,
    Dictionary<string, TaskConfigSummaryDto> TaskConfigurations);

/// <summary>
/// Returns the published schedule assignments for members of a specific group,
/// along with task configuration summaries for all tasks in the schedule.
/// Supports both legacy TaskSlots and the newer GroupTasks model.
/// </summary>
public record GetGroupScheduleQuery(Guid SpaceId, Guid GroupId) : IRequest<GroupScheduleResponseDto>;

public class GetGroupScheduleQueryHandler : IRequestHandler<GetGroupScheduleQuery, GroupScheduleResponseDto>
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;

    public GetGroupScheduleQueryHandler(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<GroupScheduleResponseDto> Handle(GetGroupScheduleQuery req, CancellationToken ct)
    {
        var cacheKey = $"schedule:{req.SpaceId}:{req.GroupId}";
        var cached = await _cache.GetAsync<GroupScheduleResponseDto>(cacheKey, ct);
        if (cached is not null)
            return cached;

        // Get the latest published version
        var version = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (version is null)
            return new GroupScheduleResponseDto([], new Dictionary<string, TaskConfigSummaryDto>());

        // Also include the previous published version (now archived) so that
        // recent past assignments (yesterday, etc.) remain visible after a new publish.
        // This prevents the "schedule disappears after publish" bug.
        var previousVersion = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId
                && (v.Status == ScheduleVersionStatus.Published || v.Status == ScheduleVersionStatus.Archived)
                && v.Id != version.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        var versionIds = new List<Guid> { version.Id };
        if (previousVersion is not null)
            versionIds.Add(previousVersion.Id);

        // Get member person IDs for this group
        var memberIds = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.GroupId == req.GroupId && m.SpaceId == req.SpaceId)
            .Select(m => m.PersonId)
            .ToListAsync(ct);

        if (memberIds.Count == 0)
            return new GroupScheduleResponseDto([], new Dictionary<string, TaskConfigSummaryDto>());

        // Load raw assignments for those members (from current + previous version)
        var rawAssignments = await _db.Assignments.AsNoTracking()
            .Where(a => versionIds.Contains(a.ScheduleVersionId)
                && a.SpaceId == req.SpaceId
                && memberIds.Contains(a.PersonId))
            .ToListAsync(ct);

        if (rawAssignments.Count == 0)
            return new GroupScheduleResponseDto([], new Dictionary<string, TaskConfigSummaryDto>());

        // Load people contact details used by the schedule search filter.
        var people = await _db.People.AsNoTracking()
            .Where(p => memberIds.Contains(p.Id))
            .ToDictionaryAsync(
                p => p.Id,
                p => new { Name = p.DisplayName ?? p.FullName, p.PhoneNumber },
                ct);

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

        // Load all active GroupTasks for this group (needed for both shift resolution and task config)
        var thisGroupTasks = await _db.GroupTasks.AsNoTracking()
            .Where(t => t.GroupId == req.GroupId && t.SpaceId == req.SpaceId && t.IsActive)
            .ToListAsync(ct);

        // Build shift-GUID → task lookup for this group's tasks (solver generates derived GUIDs)
        var shiftGuidToTask = new Dictionary<Guid, (string Name, DateTime StartsAt, DateTime EndsAt)>();
        if (missingSlotIds.Count > 0)
        {
            foreach (var gt in thisGroupTasks)
            {
                if (gt.ShiftDurationMinutes < 1) continue;
                var shiftDuration = TimeSpan.FromMinutes(gt.ShiftDurationMinutes);
                
                // The normalizer uses relative indices starting from windowStart.
                // To resolve GUIDs back to times, we need to figure out what windowStart was.
                // Since we don't know the exact horizon, we use the EARLIEST assignment time
                // as a hint. For now, just iterate from task start — the GUIDs will match
                // (same index = same GUID) and the times will be correct because the iteration
                // covers the full task range.
                var shiftStart = gt.StartsAt;
                var shiftIndex = 0;
                const int maxShifts = 10_000;
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

        // Build task configurations dictionary keyed by task name
        var taskConfigurations = thisGroupTasks.ToDictionary(
            gt => gt.Name,
            gt => new TaskConfigSummaryDto(
                TaskId: gt.Id.ToString(),
                AllowsDoubleShift: gt.AllowsDoubleShift,
                AllowsOverlap: gt.AllowsOverlap,
                DailyStartTime: gt.DailyStartTime?.ToString("HH:mm"),
                DailyEndTime: gt.DailyEndTime?.ToString("HH:mm"),
                BurdenLevel: gt.BurdenLevel.ToString(),
                RequiredQualificationNames: gt.RequiredQualificationNames,
                SplitCount: gt.SplitCount));

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

            var person = people.TryGetValue(a.PersonId, out var personInfo) ? personInfo : null;
            var personName = person?.Name ?? "Unknown";
            // Convert UTC to Israel local time (+3h) so the frontend displays correct dates
            var localStartsAt = DateTime.SpecifyKind(startsAt.AddHours(3), DateTimeKind.Unspecified);
            var localEndsAt = DateTime.SpecifyKind(endsAt.AddHours(3), DateTimeKind.Unspecified);
            result.Add(new GroupScheduleAssignmentDto(
                a.Id, a.PersonId, personName, person?.PhoneNumber, taskName,
                localStartsAt, localEndsAt, a.Source.ToString()));
        }

        var ordered = result.OrderBy(r => r.SlotStartsAt).ToList();
        var response = new GroupScheduleResponseDto(ordered, taskConfigurations);
        await _cache.SetAsync(cacheKey, response, TimeSpan.FromSeconds(30), ct);
        return response;
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
