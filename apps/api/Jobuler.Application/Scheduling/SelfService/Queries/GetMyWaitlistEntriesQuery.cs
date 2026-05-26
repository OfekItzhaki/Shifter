using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.SelfService.Queries;

/// <summary>
/// Query to retrieve a member's active waitlist entries across all slots in a space.
/// Returns entries in Waiting or Offered status.
/// </summary>
public record GetMyWaitlistEntriesQuery(
    Guid SpaceId,
    Guid PersonId) : IRequest<IReadOnlyList<WaitlistEntryDto>>;

public record WaitlistEntryDto(
    Guid Id,
    Guid ShiftSlotId,
    DateOnly SlotDate,
    TimeOnly SlotStartTime,
    TimeOnly SlotEndTime,
    string TaskName,
    int Position,
    string Status,
    DateTime? OfferedAt,
    DateTime? ExpiresAt);

public class GetMyWaitlistEntriesQueryHandler : IRequestHandler<GetMyWaitlistEntriesQuery, IReadOnlyList<WaitlistEntryDto>>
{
    private readonly AppDbContext _db;

    public GetMyWaitlistEntriesQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WaitlistEntryDto>> Handle(GetMyWaitlistEntriesQuery request, CancellationToken ct)
    {
        var entries = await _db.WaitlistEntries
            .AsNoTracking()
            .Where(e => e.SpaceId == request.SpaceId
                        && e.PersonId == request.PersonId
                        && (e.Status == WaitlistEntryStatus.Waiting || e.Status == WaitlistEntryStatus.Offered))
            .Join(
                _db.ShiftSlots.AsNoTracking(),
                entry => entry.ShiftSlotId,
                slot => slot.Id,
                (entry, slot) => new { Entry = entry, Slot = slot })
            .Join(
                _db.GroupTasks.AsNoTracking(),
                combined => combined.Slot.GroupTaskId,
                task => task.Id,
                (combined, task) => new { combined.Entry, combined.Slot, TaskName = task.Name })
            .OrderBy(x => x.Slot.Date)
            .ThenBy(x => x.Slot.StartTime)
            .Select(x => new WaitlistEntryDto(
                x.Entry.Id,
                x.Entry.ShiftSlotId,
                x.Slot.Date,
                x.Slot.StartTime,
                x.Slot.EndTime,
                x.TaskName,
                x.Entry.Position,
                x.Entry.Status.ToString(),
                x.Entry.OfferedAt,
                x.Entry.ExpiresAt))
            .ToListAsync(ct);

        return entries;
    }
}
