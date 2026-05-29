using System.Text.Json;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Billing.Commands;

public record HandleWebhookCommand(
    string EventId,
    string EventType,
    string Payload,
    Dictionary<string, string> Metadata) : IRequest;

public class HandleWebhookCommandHandler : IRequestHandler<HandleWebhookCommand>
{
    private readonly AppDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<HandleWebhookCommandHandler> _logger;

    private static readonly HashSet<string> RecognizedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "subscription_created",
        "subscription_updated",
        "subscription_cancelled",
        "subscription_payment_success"
    };

    public HandleWebhookCommandHandler(
        AppDbContext db,
        IMediator mediator,
        ILogger<HandleWebhookCommandHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(HandleWebhookCommand req, CancellationToken ct)
    {
        // ── Idempotency check ────────────────────────────────────────────────
        var alreadyProcessed = await _db.WebhookEventLogs
            .AnyAsync(e => e.EventId == req.EventId, ct);

        if (alreadyProcessed)
        {
            _logger.LogInformation("Webhook event {EventId} already processed, skipping", req.EventId);
            return;
        }

        // ── Store event ID before processing ─────────────────────────────────
        var eventLog = WebhookEventLog.Create(req.EventId, req.EventType);
        _db.WebhookEventLogs.Add(eventLog);
        await _db.SaveChangesAsync(ct);

        // ── Test charge isolation ────────────────────────────────────────────
        if (req.Metadata.TryGetValue("charge_type", out var chargeType)
            && chargeType == "test-charge")
        {
            _logger.LogInformation(
                "Test charge webhook received (EventId: {EventId}, EventType: {EventType}). Skipping subscription processing",
                req.EventId, req.EventType);
            return;
        }

        // ── Unrecognized event type ──────────────────────────────────────────
        if (!RecognizedEventTypes.Contains(req.EventType))
        {
            _logger.LogInformation(
                "Unrecognized webhook event type '{EventType}' (EventId: {EventId}). Skipping processing",
                req.EventType, req.EventId);
            return;
        }

        // ── Route: space-level vs group-level ────────────────────────────────
        // Space-level events have space_id in metadata WITHOUT group_id.
        // Group-level events have both space_id AND group_id (legacy).
        var isSpaceLevel = req.Metadata.ContainsKey("space_id")
                           && !req.Metadata.ContainsKey("group_id");

        if (isSpaceLevel)
        {
            await DispatchSpaceLevelEventAsync(req, ct);
        }
        else
        {
            await DispatchGroupLevelEventAsync(req, ct);
        }
    }

    /// <summary>
    /// Dispatches webhook events to space-level subscription handlers.
    /// </summary>
    private async Task DispatchSpaceLevelEventAsync(HandleWebhookCommand req, CancellationToken ct)
    {
        switch (req.EventType.ToLowerInvariant())
        {
            case "subscription_created":
                await _mediator.Send(
                    new HandleSpaceSubscriptionCreatedCommand(req.Payload, req.Metadata), ct);
                break;

            case "subscription_updated":
                var updatedCommand = ParseSpaceSubscriptionUpdatedCommand(req);
                if (updatedCommand is not null)
                {
                    await _mediator.Send(updatedCommand, ct);
                }
                break;

            case "subscription_cancelled":
                await _mediator.Send(
                    new HandleSpaceSubscriptionCancelledCommand(req.Payload, req.Metadata), ct);
                break;

            case "subscription_payment_success":
                _logger.LogInformation(
                    "Space-level subscription_payment_success received (EventId: {EventId}). No handler required",
                    req.EventId);
                break;
        }
    }

    /// <summary>
    /// Dispatches webhook events to existing group-level subscription handlers (backward compatibility).
    /// </summary>
    private async Task DispatchGroupLevelEventAsync(HandleWebhookCommand req, CancellationToken ct)
    {
        switch (req.EventType.ToLowerInvariant())
        {
            case "subscription_created":
                await _mediator.Send(new HandleSubscriptionCreatedCommand(req.Payload, req.Metadata), ct);
                break;

            case "subscription_updated":
                await _mediator.Send(new HandleSubscriptionUpdatedCommand(req.Payload, req.Metadata), ct);
                break;

            case "subscription_cancelled":
                await _mediator.Send(new HandleSubscriptionCancelledCommand(req.Payload, req.Metadata), ct);
                break;

            case "subscription_payment_success":
                await _mediator.Send(new HandlePaymentSuccessCommand(req.Payload, req.Metadata), ct);
                break;
        }
    }

    /// <summary>
    /// Parses the webhook payload to construct a HandleSpaceSubscriptionUpdatedCommand.
    /// Returns null if parsing fails (logs warning).
    /// </summary>
    private HandleSpaceSubscriptionUpdatedCommand? ParseSpaceSubscriptionUpdatedCommand(HandleWebhookCommand req)
    {
        if (!req.Metadata.TryGetValue("space_id", out var spaceIdStr)
            || !Guid.TryParse(spaceIdStr, out var spaceId))
        {
            _logger.LogWarning(
                "subscription_updated webhook has space_id key but value is invalid. EventId: {EventId}",
                req.EventId);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(req.Payload);
            var attributes = doc.RootElement.GetProperty("data").GetProperty("attributes");

            // ── Extract variant_id (tier) ────────────────────────────────────
            var variantId = "pro";
            if (attributes.TryGetProperty("variant_id", out var variantIdProp))
            {
                variantId = variantIdProp.ValueKind == JsonValueKind.Number
                    ? variantIdProp.GetInt64().ToString()
                    : variantIdProp.GetString() ?? "pro";
            }

            // ── Extract period dates ─────────────────────────────────────────
            DateTime periodStart;
            DateTime periodEnd;

            if (attributes.TryGetProperty("current_period_start", out var cpStartEl)
                && cpStartEl.ValueKind == JsonValueKind.String)
            {
                periodStart = DateTime.Parse(cpStartEl.GetString()!).ToUniversalTime();
            }
            else if (attributes.TryGetProperty("created_at", out var createdAtEl)
                     && createdAtEl.ValueKind == JsonValueKind.String)
            {
                periodStart = DateTime.Parse(createdAtEl.GetString()!).ToUniversalTime();
            }
            else
            {
                periodStart = DateTime.UtcNow;
            }

            if (attributes.TryGetProperty("current_period_end", out var cpEndEl)
                && cpEndEl.ValueKind == JsonValueKind.String)
            {
                periodEnd = DateTime.Parse(cpEndEl.GetString()!).ToUniversalTime();
            }
            else if (attributes.TryGetProperty("renews_at", out var renewsAtEl)
                     && renewsAtEl.ValueKind == JsonValueKind.String)
            {
                periodEnd = DateTime.Parse(renewsAtEl.GetString()!).ToUniversalTime();
            }
            else
            {
                periodEnd = periodStart.AddMonths(1);
            }

            // ── Extract auto-renew flag ──────────────────────────────────────
            var autoRenew = true;
            if (attributes.TryGetProperty("cancelled", out var cancelledProp)
                && cancelledProp.ValueKind == JsonValueKind.True)
            {
                autoRenew = false;
            }

            return new HandleSpaceSubscriptionUpdatedCommand(
                spaceId, variantId, periodStart, periodEnd, autoRenew);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to parse subscription_updated payload for space-level event. EventId: {EventId}",
                req.EventId);
            return null;
        }
    }
}
