using System.Text.Json;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Billing.Commands;

public record HandleSubscriptionCreatedCommand(
    string Payload,
    Dictionary<string, string> Metadata) : IRequest;

public class HandleSubscriptionCreatedCommandHandler : IRequestHandler<HandleSubscriptionCreatedCommand>
{
    private readonly AppDbContext _db;
    private readonly ILogger<HandleSubscriptionCreatedCommandHandler> _logger;

    public HandleSubscriptionCreatedCommandHandler(
        AppDbContext db,
        ILogger<HandleSubscriptionCreatedCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(HandleSubscriptionCreatedCommand req, CancellationToken ct)
    {
        // ── Extract space/group from metadata ────────────────────────────────
        if (!req.Metadata.TryGetValue("space_id", out var spaceIdStr) ||
            !req.Metadata.TryGetValue("group_id", out var groupIdStr) ||
            !Guid.TryParse(spaceIdStr, out var spaceId) ||
            !Guid.TryParse(groupIdStr, out var groupId))
        {
            _logger.LogWarning(
                "subscription_created webhook missing or invalid space_id/group_id in metadata");
            return;
        }

        // ── Parse payload ────────────────────────────────────────────────────
        using var doc = JsonDocument.Parse(req.Payload);
        var root = doc.RootElement;

        var attributes = root.GetProperty("data").GetProperty("attributes");
        var status = attributes.GetProperty("status").GetString();
        var subscriptionId = root.GetProperty("data").GetProperty("id").GetString()!;
        var customerId = attributes.GetProperty("customer_id").GetInt64().ToString();

        // ── Look up GroupSubscription ────────────────────────────────────────
        var subscription = await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.SpaceId == spaceId && s.GroupId == groupId, ct);

        if (subscription is null)
        {
            _logger.LogWarning(
                "No GroupSubscription found for SpaceId={SpaceId}, GroupId={GroupId}. Skipping subscription_created",
                spaceId, groupId);
            return;
        }

        // ── Already activated — no-op ────────────────────────────────────────
        if ((subscription.Status == SubscriptionStatus.Active || subscription.Status == SubscriptionStatus.Trialing)
            && !string.IsNullOrEmpty(subscription.LemonSqueezySubscriptionId))
        {
            _logger.LogInformation(
                "GroupSubscription for GroupId={GroupId} already has status {Status} with LemonSqueezy ID. No-op",
                groupId, subscription.Status);
            return;
        }

        // ── Handle based on status ───────────────────────────────────────────
        switch (status)
        {
            case "active":
            {
                var periodStart = attributes.GetProperty("renews_at").GetDateTime();
                var periodEnd = periodStart; // Default if no explicit end

                if (attributes.TryGetProperty("current_period_start", out var periodStartProp)
                    && periodStartProp.ValueKind != JsonValueKind.Null)
                {
                    periodStart = periodStartProp.GetDateTime();
                }

                if (attributes.TryGetProperty("current_period_end", out var periodEndProp)
                    && periodEndProp.ValueKind != JsonValueKind.Null)
                {
                    periodEnd = periodEndProp.GetDateTime();
                }
                else if (attributes.TryGetProperty("renews_at", out var renewsAtProp)
                         && renewsAtProp.ValueKind != JsonValueKind.Null)
                {
                    periodEnd = renewsAtProp.GetDateTime();
                }

                // Determine tier from product metadata or default
                var tierId = "pro"; // Default tier
                if (attributes.TryGetProperty("product_id", out var productIdProp))
                {
                    tierId = productIdProp.GetInt64().ToString();
                }
                if (attributes.TryGetProperty("variant_id", out var variantIdProp))
                {
                    tierId = variantIdProp.GetInt64().ToString();
                }

                subscription.Activate(tierId, subscriptionId, customerId, periodStart, periodEnd);
                break;
            }

            case "on_trial":
            {
                var trialEndsAt = DateTime.UtcNow.AddDays(14); // Default fallback
                if (attributes.TryGetProperty("trial_ends_at", out var trialEndProp)
                    && trialEndProp.ValueKind != JsonValueKind.Null)
                {
                    trialEndsAt = trialEndProp.GetDateTime();
                }

                subscription.StartTrial(subscriptionId, customerId, trialEndsAt);
                break;
            }

            default:
                _logger.LogWarning(
                    "subscription_created received with unexpected status '{Status}' for GroupId={GroupId}. Skipping",
                    status, groupId);
                return;
        }

        await _db.SaveChangesAsync(ct);
    }
}
