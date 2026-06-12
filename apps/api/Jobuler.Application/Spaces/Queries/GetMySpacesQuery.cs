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
        return await (from membership in _db.SpaceMemberships.AsNoTracking()
                      join space in _db.Spaces on membership.SpaceId equals space.Id
                      join organization in _db.Organizations on space.OrganizationId equals organization.Id into organizations
                      from organization in organizations.DefaultIfEmpty()
                      where membership.UserId == request.UserId
                          && membership.IsActive
                          && space.IsActive
                          && space.DeletedAt == null
                          && (organization == null || organization.Status == Jobuler.Domain.Organizations.OrganizationStatus.Active)
                      select new SpaceDto(
                          space.Id,
                          space.Name,
                          space.Description,
                          space.OwnerUserId,
                          space.Locale,
                          space.IsActive,
                          space.CreatedAt))
            .ToListAsync(ct);
    }
}
