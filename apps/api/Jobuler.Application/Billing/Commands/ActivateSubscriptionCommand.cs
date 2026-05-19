using Jobuler.Application.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Commands;

public record ActivateSubscriptionCommand(
    Guid SpaceId,
    Guid GroupId,
    string TierId,
    string LemonSqueezySubscriptionId,
    string LemonSqueezyCustomerId,
    DateTime PeriodStart,
    DateTime PeriodEnd) : IRequest;

public class ActivateSubscriptionCommandHandler : IRequestHandler<ActivateSubscriptionCommand>
{
    private readonly AppDbContext _db;
    private readonly IPeriodManager _periodManager;

    public ActivateSubscriptionCommandHandler(AppDbContext db, IPeriodManager periodManager)
    {
        _db = db;
        _periodManager = periodManager;
    }

    public async Task Handle(ActivateSubscriptionCommand req, CancellationToken ct)
    {
        var sub = await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.GroupId == req.GroupId && s.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Subscription not found for group.");

        sub.Activate(req.TierId, req.LemonSqueezySubscriptionId, req.LemonSqueezyCustomerId,
            req.PeriodStart, req.PeriodEnd);

        await _db.SaveChangesAsync(ct);

        // Open a new subscription period for cumulative tracking
        await _periodManager.OpenPeriodAsync(req.SpaceId, req.GroupId, ct);
    }
}
