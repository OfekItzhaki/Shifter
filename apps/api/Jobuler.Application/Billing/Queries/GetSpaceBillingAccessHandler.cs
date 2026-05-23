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
        var subscription = await _db.SpaceSubscriptions
            .FirstOrDefaultAsync(s => s.SpaceId == request.SpaceId, ct);

        if (subscription == null)
            return false;

        return subscription.IsAccessGranted;
    }
}
