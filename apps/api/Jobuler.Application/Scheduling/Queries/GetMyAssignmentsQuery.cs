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

        // Load assignments in date range
        var assignments = await _db.Assignments.AsNoTracking()
            .Where(a => a.ScheduleVersionId == version.Id
                && a.SpaceId == req.SpaceId
                && a.PersonId == person.Id)
            .Join(_db.TaskSlots, a => a.TaskSlotId, s => s.Id,
                (a, s) => new { a, Slot = s })
            .Where(x => x.Slot.StartsAt >= req.From && x.Slot.StartsAt < req.To)
            .Join(_db.TaskTypes, x => x.Slot.TaskTypeId, t => t.Id,
                (x, t) => new { x.a, x.Slot, TaskName = t.Name })
            .OrderBy(x => x.Slot.StartsAt)
            .ToListAsync(ct);

        if (assignments.Count == 0) return [];

        // Map group names
        var groups = await _db.Groups.AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        // For each assignment, find which group it belongs to via task slot's space
        // (assignments don't directly reference groups — we show the first group the person is in)
        var firstGroupId = groupIds.FirstOrDefault();
        var firstGroupName = firstGroupId != Guid.Empty && groups.TryGetValue(firstGroupId, out var gn) ? gn : "—";

        return assignments.Select(x => new MyAssignmentDto(
            x.a.Id,
            firstGroupId,
            firstGroupName,
            x.TaskName,
            x.Slot.StartsAt,
            x.Slot.EndsAt,
            x.a.Source.ToString()
        )).ToList();
    }
}
