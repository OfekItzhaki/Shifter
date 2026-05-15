using Jobuler.Application.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Commands;

public record CancelSubscriptionCommand(
    Guid SpaceId,
    Guid GroupId) : IRequest;

public class CancelSubscriptionCommandHandler : IRequestHandler<CancelSubscriptionCommand>
{
    private readonly AppDbContext _db;
    private readonly IPeriodManager _periodManager;

    public CancelSubscriptionCommandHandler(AppDbContext db, IPeriodManager periodManager)
    {
        _db = db;
        _periodManager = periodManager;
    }

    public async Task Handle(CancelSubscriptionCommand req, CancellationToken ct)
    {
        var sub = await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.GroupId == req.GroupId && s.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Subscription not found for group.");

        sub.Cancel();
        await _db.SaveChangesAsync(ct);

        // Close the current subscription period after cancellation
        // Note: In production, this would be called after the 14-day grace period elapses.
        // The grace period logic is handled by the caller (e.g., a background job or webhook).
        await _periodManager.ClosePeriodAsync(req.SpaceId, req.GroupId, ct);
    }
}
