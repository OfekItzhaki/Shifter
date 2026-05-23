using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Billing.Commands;

public record HandleSpaceSubscriptionCancelledCommand(
    string Payload,
    Dictionary<string, string> Metadata) : IRequest;

public class HandleSpaceSubscriptionCancelledCommandHandler : IRequestHandler<HandleSpaceSubscriptionCancelledCommand>
{
    private readonly AppDbContext _db;
    private readonly ILogger<HandleSpaceSubscriptionCancelledCommandHandler> _logger;

    public HandleSpaceSubscriptionCancelledCommandHandler(
        AppDbContext db,
        ILogger<HandleSpaceSubscriptionCancelledCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(HandleSpaceSubscriptionCancelledCommand req, CancellationToken ct)
    {
        // ── Extract space_id from metadata ───────────────────────────────────
        if (!req.Metadata.TryGetValue("space_id", out var spaceIdStr) ||
            !Guid.TryParse(spaceIdStr, out var spaceId))
        {
            _logger.LogWarning(
                "subscription_cancelled webhook missing or invalid space_id in metadata. Skipping");
            return;
        }

        // ── Load SpaceSubscription by SpaceId ────────────────────────────────
        var subscription = await _db.SpaceSubscriptions
            .FirstOrDefaultAsync(s => s.SpaceId == spaceId, ct);

        if (subscription is null)
        {
            _logger.LogWarning(
                "No SpaceSubscription found for SpaceId={SpaceId}. Skipping subscription_cancelled",
                spaceId);
            return;
        }

        // ── If already Canceled or Expired, treat as no-op (webhooks can retry)
        if (subscription.Status == SubscriptionStatus.Canceled
            || subscription.Status == SubscriptionStatus.Expired)
        {
            _logger.LogInformation(
                "SpaceSubscription for SpaceId={SpaceId} is already {Status}. Skipping cancellation",
                spaceId, subscription.Status);
            return;
        }

        // ── Cancel the subscription ──────────────────────────────────────────
        subscription.Cancel();

        // ── Save changes ─────────────────────────────────────────────────────
        await _db.SaveChangesAsync(ct);
    }
}
