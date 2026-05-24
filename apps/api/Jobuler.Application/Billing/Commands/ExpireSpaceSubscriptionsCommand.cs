using Jobuler.Application.Billing;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Logs;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Billing.Commands;

/// <summary>
/// Background job command that expires canceled space subscriptions whose grace period has ended.
/// No user context or permission check needed — runs on a schedule.
/// </summary>
public record ExpireSpaceSubscriptionsCommand() : IRequest;

public class ExpireSpaceSubscriptionsCommandHandler : IRequestHandler<ExpireSpaceSubscriptionsCommand>
{
    private readonly AppDbContext _db;
    private readonly IStatisticsPeriodService _statisticsPeriodService;
    private readonly ILogger<ExpireSpaceSubscriptionsCommandHandler> _logger;

    public ExpireSpaceSubscriptionsCommandHandler(
        AppDbContext db,
        IStatisticsPeriodService statisticsPeriodService,
        ILogger<ExpireSpaceSubscriptionsCommandHandler> logger)
    {
        _db = db;
        _statisticsPeriodService = statisticsPeriodService;
        _logger = logger;
    }

    public async Task Handle(ExpireSpaceSubscriptionsCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Query all SpaceSubscriptions with status Canceled and CurrentPeriodEnd <= now
        var expiredSubscriptions = await _db.SpaceSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Canceled && s.CurrentPeriodEnd != null && s.CurrentPeriodEnd <= now)
            .ToListAsync(ct);

        if (expiredSubscriptions.Count == 0)
            return;

        _logger.LogInformation(
            "Expiring {Count} canceled space subscriptions past their period end.",
            expiredSubscriptions.Count);

        foreach (var subscription in expiredSubscriptions)
        {
            subscription.Expire();

            // Trigger statistics period closure for all groups in the space
            await _statisticsPeriodService.OnSubscriptionExpiredAsync(
                subscription.SpaceId,
                subscription.CurrentPeriodEnd!.Value,
                ct);

            _db.AuditLogs.Add(AuditLog.Create(
                spaceId: subscription.SpaceId,
                actorUserId: Guid.Empty,
                action: "space_subscription.expire",
                entityType: "SpaceSubscription",
                entityId: subscription.Id));
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Successfully expired {Count} space subscriptions.",
            expiredSubscriptions.Count);
    }
}
