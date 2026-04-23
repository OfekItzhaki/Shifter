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

        // Load assignments for those members
        var assignments = await _db.Assignments.AsNoTracking()
            .Where(a => a.ScheduleVersionId == version.Id && a.SpaceId == req.SpaceId
                && memberIds.Contains(a.PersonId))
            .Join(_db.People, a => a.PersonId, p => p.Id,
                (a, p) => new { a, PersonName = p.DisplayName ?? p.FullName })
            .Join(_db.TaskSlots, x => x.a.TaskSlotId, s => s.Id,
                (x, s) => new { x.a, x.PersonName, Slot = s })
            .Join(_db.TaskTypes, x => x.Slot.TaskTypeId, t => t.Id,
                (x, t) => new { x.a, x.PersonName, x.Slot, TaskName = t.Name })
            .OrderBy(x => x.Slot.StartsAt)
            .Select(x => new GroupScheduleAssignmentDto(
                x.a.Id, x.a.PersonId, x.PersonName, x.TaskName,
                x.Slot.StartsAt, x.Slot.EndsAt, x.a.Source.ToString()))
            .ToListAsync(ct);

        return assignments;
    }
}
