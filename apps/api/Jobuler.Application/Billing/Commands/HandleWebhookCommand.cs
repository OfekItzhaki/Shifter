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

        // ── Dispatch to specific sub-handler ─────────────────────────────────
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
}
