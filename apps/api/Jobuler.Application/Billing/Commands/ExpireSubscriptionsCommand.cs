using Jobuler.Domain.Billing;
using Jobuler.Domain.Logs;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Commands;

public record ExpireSubscriptionsCommand() : IRequest;

public class ExpireSubscriptionsCommandHandler : IRequestHandler<ExpireSubscriptionsCommand>
{
    private readonly AppDbContext _db;

    public ExpireSubscriptionsCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task Handle(ExpireSubscriptionsCommand req, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Query canceled subscriptions past their billing period end
        var expiredSubscriptions = await _db.GroupSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Canceled && s.CurrentPeriodEnd < now)
            .ToListAsync(ct);

        // Query trialing subscriptions that were canceled and are past their trial end
        var expiredTrialSubscriptions = await _db.GroupSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Canceled && s.TrialEndsAt < now && s.CurrentPeriodEnd == null)
            .ToListAsync(ct);

        var allToExpire = expiredSubscriptions.Concat(expiredTrialSubscriptions).ToList();

        if (allToExpire.Count == 0)
            return;

        var groupIds = allToExpire.Select(s => s.GroupId).Distinct().ToList();
        var groups = await _db.Groups
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, ct);

        foreach (var subscription in allToExpire)
        {
            subscription.Expire();

            if (groups.TryGetValue(subscription.GroupId, out var group))
            {
                group.Deactivate();
            }

            _db.AuditLogs.Add(AuditLog.Create(
                spaceId: subscription.SpaceId,
                actorUserId: Guid.Empty,
                action: "subscription.expire",
                entityType: "GroupSubscription",
                entityId: subscription.Id));
        }

        await _db.SaveChangesAsync(ct);
    }
}
