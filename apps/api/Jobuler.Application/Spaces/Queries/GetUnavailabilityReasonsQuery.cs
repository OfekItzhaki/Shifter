using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Queries;

public record UnavailabilityReasonDto(Guid Id, string DisplayName, int SortOrder);

public record GetUnavailabilityReasonsQuery(Guid SpaceId) : IRequest<List<UnavailabilityReasonDto>>;

public class GetUnavailabilityReasonsQueryHandler
    : IRequestHandler<GetUnavailabilityReasonsQuery, List<UnavailabilityReasonDto>>
{
    private readonly AppDbContext _db;
    public GetUnavailabilityReasonsQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<UnavailabilityReasonDto>> Handle(
        GetUnavailabilityReasonsQuery req, CancellationToken ct) =>
        await _db.UnavailabilityReasons.AsNoTracking()
            .Where(r => r.SpaceId == req.SpaceId && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .Select(r => new UnavailabilityReasonDto(r.Id, r.DisplayName, r.SortOrder))
            .ToListAsync(ct);
}
