using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Queries;

public record HomeLeaveScheduleEntryDto(
    Guid PersonId,
    string PersonName,
    DateTime StartsAt,
    DateTime EndsAt,
    string Status);

public record GetHomeLeaveScheduleQuery(Guid SpaceId, Guid GroupId) : IRequest<List<HomeLeaveScheduleEntryDto>>;

public class GetHomeLeaveScheduleQueryHandler : IRequestHandler<GetHomeLeaveScheduleQuery, List<HomeLeaveScheduleEntryDto>>
{
    private readonly AppDbContext _db;

    public GetHomeLeaveScheduleQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<HomeLeaveScheduleEntryDto>> Handle(GetHomeLeaveScheduleQuery req, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-7);

        // Get group member person IDs
        var memberPersonIds = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.GroupId == req.GroupId && m.SpaceId == req.SpaceId)
            .Select(m => m.PersonId)
            .ToListAsync(ct);

        if (memberPersonIds.Count == 0)
            return new List<HomeLeaveScheduleEntryDto>();

        // Query presence windows where state = AtHome, for group members, recent + upcoming
        var windows = await _db.PresenceWindows.AsNoTracking()
            .Where(pw => pw.SpaceId == req.SpaceId
                && memberPersonIds.Contains(pw.PersonId)
                && pw.State == PresenceState.AtHome
                && pw.EndsAt > cutoff)
            .OrderBy(pw => pw.StartsAt)
            .ToListAsync(ct);

        // Get person display names
        var personIds = windows.Select(w => w.PersonId).Distinct().ToList();
        var people = await _db.People.AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName ?? p.FullName, ct);

        return windows.Select(w =>
        {
            var status = w.StartsAt <= now && w.EndsAt > now ? "active"
                : w.StartsAt > now ? "upcoming"
                : "completed";

            return new HomeLeaveScheduleEntryDto(
                w.PersonId,
                people.GetValueOrDefault(w.PersonId, ""),
                w.StartsAt,
                w.EndsAt,
                status);
        }).ToList();
    }
}
