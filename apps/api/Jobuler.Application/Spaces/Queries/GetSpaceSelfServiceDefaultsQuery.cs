using Jobuler.Application.Organizations.Commands;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Spaces.Commands;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jobuler.Application.Spaces.Queries;

public record GetSpaceSelfServiceDefaultsQuery(Guid SpaceId) : IRequest<SpaceSelfServiceDefaultsDto>;

public class GetSpaceSelfServiceDefaultsQueryHandler
    : IRequestHandler<GetSpaceSelfServiceDefaultsQuery, SpaceSelfServiceDefaultsDto>
{
    private readonly AppDbContext _db;
    private readonly SelfServiceDefaultPolicyOptions _installDefaults;

    public GetSpaceSelfServiceDefaultsQueryHandler(
        AppDbContext db,
        IOptions<SelfServiceDefaultPolicyOptions> installDefaults)
    {
        _db = db;
        _installDefaults = installDefaults.Value;
    }

    public async Task<SpaceSelfServiceDefaultsDto> Handle(GetSpaceSelfServiceDefaultsQuery request, CancellationToken ct)
    {
        var space = await _db.Spaces
            .AsNoTracking()
            .Where(s => s.Id == request.SpaceId && s.IsActive)
            .Select(s => new { s.Id, s.OrganizationId })
            .FirstOrDefaultAsync(ct);

        if (space is null)
            throw new KeyNotFoundException("Space not found.");

        var defaults = await _db.SpaceSelfServiceDefaults
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SpaceId == request.SpaceId, ct);

        if (defaults is not null)
            return UpdateSpaceSelfServiceDefaultsCommandHandler.ToDto(defaults, "space");

        var organizationDefaults = await _db.OrganizationSelfServiceDefaults
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.OrganizationId == space.OrganizationId, ct);

        if (organizationDefaults is not null)
            return OrganizationSelfServiceDefaultsMapper.ToDto(organizationDefaults, "organization");

        return OrganizationSelfServiceDefaultsMapper.ToInstallDto(_installDefaults);
    }
}
