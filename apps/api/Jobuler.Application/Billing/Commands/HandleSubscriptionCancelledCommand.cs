using System.Text.Json;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Billing.Commands;

public record HandleSubscriptionCancelledCommand(
    string Payload,
    Dictionary<string, string> Metadata) : IRequest;

public class HandleSubscriptionCancelledCommandHandler : IRequestHandler<HandleSubscriptionCancelledCommand>
{
    private readonly AppDbContext _db;
    private readonly ILogger<HandleSubscriptionCancelledCommandHandler> _logger;

    public HandleSubscriptionCancelledCommandHandler(
        AppDbContext db,
        ILogger<HandleSubscriptionCancelledCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(HandleSubscriptionCancelledCommand req, CancellationToken ct)
    {
        // ── Parse payload ────────────────────────────────────────────────────
        using var doc = JsonDocument.Parse(req.Payload);
        var root = doc.RootElement;

        var data = root.GetProperty("data");
        var subscriptionId = data.GetProperty("id").ToString();

        // ── Look up GroupSubscription by LemonSqueezy subscription ID ────────
        var subscription = await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.LemonSqueezySubscriptionId == subscriptionId, ct);

        if (subscription is null)
        {
            _logger.LogWarning(
                "No GroupSubscription found for LemonSqueezySubscriptionId={SubscriptionId}. Skipping subscription_cancelled",
                subscriptionId);
            return;
        }

        // ── If already Canceled or Expired, treat as no-op ───────────────────
        if (subscription.Status == SubscriptionStatus.Canceled
            || subscription.Status == SubscriptionStatus.Expired)
        {
            _logger.LogInformation(
                "GroupSubscription {SubscriptionId} is already {Status}. Skipping cancellation",
                subscriptionId, subscription.Status);
            return;
        }

        // ── Set Status=Canceled and CanceledAt=DateTime.UtcNow ─────────────
        subscription.Cancel();

        // ── Determine whether to deactivate group immediately ────────────────
        var now = DateTime.UtcNow;
        var shouldDeactivateImmediately = subscription.CurrentPeriodEnd is null
                                          || subscription.CurrentPeriodEnd <= now;

        if (shouldDeactivateImmediately)
        {
            var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == subscription.GroupId, ct);
            if (group is not null)
            {
                group.Deactivate();
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
