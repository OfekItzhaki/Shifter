using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

public record CheckGroupSubscriptionQuery(Guid SpaceId, Guid GroupId) : IRequest<GroupSubscription?>;

public class CheckGroupSubscriptionQueryHandler : IRequestHandler<CheckGroupSubscriptionQuery, GroupSubscription?>
{
    private readonly AppDbContext _db;
    public CheckGroupSubscriptionQueryHandler(AppDbContext db) => _db = db;

    public async Task<GroupSubscription?> Handle(CheckGroupSubscriptionQuery req, CancellationToken ct)
    {
        return await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.GroupId == req.GroupId && s.SpaceId == req.SpaceId, ct);
    }
}
