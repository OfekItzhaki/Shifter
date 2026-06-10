using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.SelfService.Queries;

/// <summary>
/// Query to retrieve active waitlist entries for admins in a single self-service group.
/// </summary>
public record GetAdminWaitlistEntriesQuery(
    Guid SpaceId,
    Guid GroupId) : IRequest<IReadOnlyList<AdminWaitlistEntryDto>>;

public record AdminWaitlistEntryDto(
    Guid Id,
    Guid ShiftSlotId,
    DateOnly SlotDate,
    TimeOnly SlotStartTime,
    TimeOnly SlotEndTime,
    string TaskName,
    Guid PersonId,
    string PersonName,
    int Position,
    string Status,
    DateTime? OfferedAt,
    DateTime? ExpiresAt);

public class GetAdminWaitlistEntriesQueryHandler : IRequestHandler<GetAdminWaitlistEntriesQuery, IReadOnlyList<AdminWaitlistEntryDto>>
{
    private readonly AppDbContext _db;

    public GetAdminWaitlistEntriesQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdminWaitlistEntryDto>> Handle(GetAdminWaitlistEntriesQuery request, CancellationToken ct)
    {
        var entries = await _db.WaitlistEntries
            .AsNoTracking()
            .Where(e => e.SpaceId == request.SpaceId
                        && (e.Status == WaitlistEntryStatus.Waiting || e.Status == WaitlistEntryStatus.Offered))
            .Join(
                _db.ShiftSlots.AsNoTracking(),
                entry => entry.ShiftSlotId,
                slot => slot.Id,
                (entry, slot) => new { Entry = entry, Slot = slot })
            .Where(x => x.Slot.SpaceId == request.SpaceId && x.Slot.GroupId == request.GroupId)
            .Join(
                _db.GroupTasks.AsNoTracking(),
                combined => combined.Slot.GroupTaskId,
                task => task.Id,
                (combined, task) => new { combined.Entry, combined.Slot, TaskName = task.Name })
            .Join(
                _db.People.AsNoTracking(),
                combined => combined.Entry.PersonId,
                person => person.Id,
                (combined, person) => new { combined.Entry, combined.Slot, combined.TaskName, PersonName = person.DisplayName ?? person.FullName })
            .OrderBy(x => x.Slot.Date)
            .ThenBy(x => x.Slot.StartTime)
            .ThenBy(x => x.Entry.Position)
            .Select(x => new AdminWaitlistEntryDto(
                x.Entry.Id,
                x.Entry.ShiftSlotId,
                x.Slot.Date,
                x.Slot.StartTime,
                x.Slot.EndTime,
                x.TaskName,
                x.Entry.PersonId,
                x.PersonName,
                x.Entry.Position,
                x.Entry.Status.ToString(),
                x.Entry.OfferedAt,
                x.Entry.ExpiresAt))
            .ToListAsync(ct);

        return entries;
    }
}
