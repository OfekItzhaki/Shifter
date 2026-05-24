using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Queries;

public record GetSpaceQuery(Guid SpaceId) : IRequest<SpaceDto?>;

public record SpaceDto(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerUserId,
    string Locale,
    bool IsActive,
    DateTime CreatedAt);

public class GetSpaceQueryHandler : IRequestHandler<GetSpaceQuery, SpaceDto?>
{
    private readonly AppDbContext _db;

    public GetSpaceQueryHandler(AppDbContext db) => _db = db;

    public async Task<SpaceDto?> Handle(GetSpaceQuery request, CancellationToken ct)
    {
        var space = await _db.Spaces
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SpaceId && s.IsActive && s.DeletedAt == null, ct);

        return space is null ? null : new SpaceDto(
            space.Id, space.Name, space.Description,
            space.OwnerUserId, space.Locale, space.IsActive, space.CreatedAt);
    }
}
