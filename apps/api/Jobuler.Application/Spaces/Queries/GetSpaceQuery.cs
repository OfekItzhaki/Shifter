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
        var space = await (from candidate in _db.Spaces.AsNoTracking()
                           join organization in _db.Organizations on candidate.OrganizationId equals organization.Id into organizations
                           from organization in organizations.DefaultIfEmpty()
                           where candidate.Id == request.SpaceId
                               && candidate.IsActive
                               && candidate.DeletedAt == null
                               && (organization == null || organization.Status == Jobuler.Domain.Organizations.OrganizationStatus.Active)
                           select candidate)
            .FirstOrDefaultAsync(ct);

        return space is null ? null : new SpaceDto(
            space.Id, space.Name, space.Description,
            space.OwnerUserId, space.Locale, space.IsActive, space.CreatedAt);
    }
}
