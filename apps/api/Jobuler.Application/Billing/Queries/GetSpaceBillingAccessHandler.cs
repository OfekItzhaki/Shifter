using Jobuler.Domain.Organizations;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Queries;

public class GetSpaceBillingAccessHandler : IRequestHandler<GetSpaceBillingAccessQuery, bool>
{
    private readonly AppDbContext _db;

    public GetSpaceBillingAccessHandler(AppDbContext db) => _db = db;

    public async Task<bool> Handle(GetSpaceBillingAccessQuery request, CancellationToken ct)
    {
        var organization = await _db.Spaces
            .AsNoTracking()
            .Where(s => s.Id == request.SpaceId)
            .GroupJoin(_db.Organizations,
                s => s.OrganizationId,
                o => o.Id,
                (s, organizations) => organizations
                    .Select(o => new
                    {
                        o.Id,
                        o.Status
                    })
                    .FirstOrDefault())
            .FirstOrDefaultAsync(ct);

        if (organization is not null
            && organization.Status != OrganizationStatus.Active)
            return false;

        if (organization is not null)
        {
            var organizationSubscription = await _db.OrganizationSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrganizationId == organization.Id, ct);

            if (organizationSubscription?.IsAccessGranted == true)
                return true;
        }

        var subscription = await _db.SpaceSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SpaceId == request.SpaceId, ct);

        if (subscription == null)
            return false;

        return subscription.IsAccessGranted;
    }
}
