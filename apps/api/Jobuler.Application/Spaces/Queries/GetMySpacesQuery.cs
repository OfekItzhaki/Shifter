using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Queries;

public record GetMySpacesQuery(Guid UserId) : IRequest<List<SpaceDto>>;

public class GetMySpacesQueryHandler : IRequestHandler<GetMySpacesQuery, List<SpaceDto>>
{
    private readonly AppDbContext _db;

    public GetMySpacesQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<SpaceDto>> Handle(GetMySpacesQuery request, CancellationToken ct)
    {
        return await _db.SpaceMemberships
            .AsNoTracking()
            .Where(m => m.UserId == request.UserId && m.IsActive)
            .Join(_db.Spaces.Where(s => s.IsActive && s.DeletedAt == null),
                m => m.SpaceId,
                s => s.Id,
                (m, s) => new SpaceDto(s.Id, s.Name, s.Description, s.OwnerUserId, s.Locale, s.IsActive, s.CreatedAt))
            .ToListAsync(ct);
    }
}
