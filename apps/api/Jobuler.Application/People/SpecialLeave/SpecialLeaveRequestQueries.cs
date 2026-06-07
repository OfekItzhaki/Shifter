using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.SpecialLeave;

public record GetMySpecialLeaveRequestsQuery(
    Guid SpaceId,
    Guid PersonId,
    DateTime? From = null,
    DateTime? To = null) : IRequest<IReadOnlyList<SpecialLeaveRequestDto>>;

public class GetMySpecialLeaveRequestsQueryHandler
    : IRequestHandler<GetMySpecialLeaveRequestsQuery, IReadOnlyList<SpecialLeaveRequestDto>>
{
    private readonly AppDbContext _db;

    public GetMySpecialLeaveRequestsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SpecialLeaveRequestDto>> Handle(
        GetMySpecialLeaveRequestsQuery req,
        CancellationToken ct) =>
        await _db.SpecialLeaveRequests.AsNoTracking()
            .Where(r => r.SpaceId == req.SpaceId && r.PersonId == req.PersonId)
            .ApplyWindow(req.From, req.To)
            .Join(
                _db.People.AsNoTracking(),
                request => request.PersonId,
                person => person.Id,
                (request, person) => new { Request = request, PersonName = person.DisplayName ?? person.FullName })
            .OrderByDescending(r => r.Request.StartsAt)
            .Select(r => new SpecialLeaveRequestDto(
                r.Request.Id,
                r.Request.SpaceId,
                r.Request.PersonId,
                r.PersonName,
                r.Request.StartsAt,
                r.Request.EndsAt,
                r.Request.Reason,
                r.Request.Status.ToString(),
                r.Request.RequestedByUserId,
                r.Request.ProcessedByUserId,
                r.Request.ProcessedAt,
                r.Request.AdminNote,
                r.Request.PresenceWindowId,
                r.Request.CreatedAt,
                r.Request.UpdatedAt))
            .ToListAsync(ct);
}

public record GetSpecialLeaveRequestsForAdminQuery(
    Guid SpaceId,
    string? Status = null,
    DateTime? From = null,
    DateTime? To = null) : IRequest<IReadOnlyList<SpecialLeaveRequestDto>>;

public class GetSpecialLeaveRequestsForAdminQueryHandler
    : IRequestHandler<GetSpecialLeaveRequestsForAdminQuery, IReadOnlyList<SpecialLeaveRequestDto>>
{
    private readonly AppDbContext _db;

    public GetSpecialLeaveRequestsForAdminQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SpecialLeaveRequestDto>> Handle(
        GetSpecialLeaveRequestsForAdminQuery req,
        CancellationToken ct)
    {
        var query = _db.SpecialLeaveRequests.AsNoTracking()
            .Where(r => r.SpaceId == req.SpaceId)
            .ApplyWindow(req.From, req.To);

        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            if (!Enum.TryParse<SpecialLeaveRequestStatus>(req.Status, true, out var status))
                throw new ArgumentException($"Invalid special leave request status: {req.Status}");

            query = query.Where(r => r.Status == status);
        }

        return await query
            .Join(
                _db.People.AsNoTracking(),
                request => request.PersonId,
                person => person.Id,
                (request, person) => new { Request = request, PersonName = person.DisplayName ?? person.FullName })
            .OrderBy(r => r.Request.StartsAt)
            .Select(r => new SpecialLeaveRequestDto(
                r.Request.Id,
                r.Request.SpaceId,
                r.Request.PersonId,
                r.PersonName,
                r.Request.StartsAt,
                r.Request.EndsAt,
                r.Request.Reason,
                r.Request.Status.ToString(),
                r.Request.RequestedByUserId,
                r.Request.ProcessedByUserId,
                r.Request.ProcessedAt,
                r.Request.AdminNote,
                r.Request.PresenceWindowId,
                r.Request.CreatedAt,
                r.Request.UpdatedAt))
            .ToListAsync(ct);
    }
}

internal static class SpecialLeaveRequestQueryExtensions
{
    public static IQueryable<SpecialLeaveRequest> ApplyWindow(
        this IQueryable<SpecialLeaveRequest> query,
        DateTime? from,
        DateTime? to)
    {
        if (from.HasValue)
            query = query.Where(r => r.EndsAt >= from.Value);
        if (to.HasValue)
            query = query.Where(r => r.StartsAt <= to.Value);
        return query;
    }
}
