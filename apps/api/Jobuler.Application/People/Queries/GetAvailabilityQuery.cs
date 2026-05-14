using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.Queries;

public record AvailabilityWindowDto(
    Guid Id, Guid PersonId, DateTime StartsAt, DateTime EndsAt, string? Note);

public record PresenceWindowDto(
    Guid Id, Guid PersonId, string State,
    DateTime StartsAt, DateTime EndsAt, string? Note, bool IsDerived,
    Guid? ReasonId = null, string? ReasonDisplayName = null);

public record GetAvailabilityQuery(Guid SpaceId, Guid PersonId)
    : IRequest<List<AvailabilityWindowDto>>;

public class GetAvailabilityQueryHandler
    : IRequestHandler<GetAvailabilityQuery, List<AvailabilityWindowDto>>
{
    private readonly AppDbContext _db;
    public GetAvailabilityQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<AvailabilityWindowDto>> Handle(
        GetAvailabilityQuery req, CancellationToken ct) =>
        await _db.AvailabilityWindows.AsNoTracking()
            .Where(a => a.SpaceId == req.SpaceId && a.PersonId == req.PersonId)
            .OrderBy(a => a.StartsAt)
            .Select(a => new AvailabilityWindowDto(a.Id, a.PersonId, a.StartsAt, a.EndsAt, a.Note))
            .ToListAsync(ct);
}

public record GetPresenceQuery(Guid SpaceId, Guid PersonId)
    : IRequest<List<PresenceWindowDto>>;

public class GetPresenceQueryHandler
    : IRequestHandler<GetPresenceQuery, List<PresenceWindowDto>>
{
    private readonly AppDbContext _db;
    public GetPresenceQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<PresenceWindowDto>> Handle(
        GetPresenceQuery req, CancellationToken ct) =>
        await _db.PresenceWindows.AsNoTracking()
            .Where(p => p.SpaceId == req.SpaceId && p.PersonId == req.PersonId)
            .OrderBy(p => p.StartsAt)
            .GroupJoin(
                _db.UnavailabilityReasons.AsNoTracking(),
                p => p.UnavailabilityReasonId,
                r => r.Id,
                (p, reasons) => new { p, reasons })
            .SelectMany(
                x => x.reasons.DefaultIfEmpty(),
                (x, reason) => new PresenceWindowDto(
                    x.p.Id, x.p.PersonId, x.p.State.ToString(),
                    x.p.StartsAt, x.p.EndsAt, x.p.Note, x.p.IsDerived,
                    x.p.UnavailabilityReasonId,
                    reason != null ? reason.DisplayName : null))
            .ToListAsync(ct);
}
