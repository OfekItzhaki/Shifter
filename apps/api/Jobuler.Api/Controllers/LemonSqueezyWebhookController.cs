using System.Text.Json;
using Jobuler.Application.Billing;
using Jobuler.Application.Billing.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("webhooks/lemonsqueezy")]
[AllowAnonymous]
public class LemonSqueezyWebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebhookSignatureValidator _signatureValidator;
    private readonly ILogger<LemonSqueezyWebhookController> _logger;

    public LemonSqueezyWebhookController(
        IMediator mediator,
        IWebhookSignatureValidator signatureValidator,
        ILogger<LemonSqueezyWebhookController> logger)
    {
        _mediator = mediator;
        _signatureValidator = signatureValidator;
        _logger = logger;
    }

    /// <summary>
    /// Receives and processes LemonSqueezy webhook events.
    /// Security is enforced via HMAC signature verification (not bearer tokens).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook(CancellationToken ct)
    {
        // ── Read raw request body ────────────────────────────────────────────
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        // ── Verify signature ─────────────────────────────────────────────────
        var signature = Request.Headers["X-Signature"].FirstOrDefault() ?? string.Empty;

        if (!_signatureValidator.Verify(payload, signature))
        {
            _logger.LogWarning("Webhook signature verification failed. Signature: {Signature}", signature);
            return Unauthorized();
        }

        // ── Parse JSON payload ───────────────────────────────────────────────
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(payload);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Webhook payload is not valid JSON. Payload length: {Length}", payload.Length);
            return BadRequest();
        }

        using (doc)
        {
            // ── Extract event type from meta.event_name ──────────────────────
            if (!doc.RootElement.TryGetProperty("meta", out var meta) ||
                !meta.TryGetProperty("event_name", out var eventNameElement))
            {
                _logger.LogError("Webhook payload missing meta.event_name. Payload: {Payload}", payload);
                return BadRequest();
            }

            var eventType = eventNameElement.GetString();
            if (string.IsNullOrWhiteSpace(eventType))
            {
                _logger.LogError("Webhook payload has empty meta.event_name");
                return BadRequest();
            }

            // ── Extract event ID ─────────────────────────────────────────────
            var eventId = ExtractEventId(doc.RootElement);
            if (string.IsNullOrWhiteSpace(eventId))
            {
                _logger.LogError("Webhook payload missing event ID. Payload: {Payload}", payload);
                return BadRequest();
            }

            // ── Extract metadata (custom_data) ───────────────────────────────
            var metadata = ExtractMetadata(meta);

            // ── Dispatch to HandleWebhookCommand via MediatR ─────────────────
            await _mediator.Send(new HandleWebhookCommand(eventId, eventType, payload, metadata), ct);

            return Ok();
        }
    }

    private static string? ExtractEventId(JsonElement root)
    {
        // Try meta.webhook_id first (LemonSqueezy v1 format)
        if (root.TryGetProperty("meta", out var meta) &&
            meta.TryGetProperty("webhook_id", out var webhookId))
        {
            return webhookId.GetString();
        }

        // Fallback: try data.id
        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty("id", out var dataId))
        {
            return dataId.GetString();
        }

        return null;
    }

    private static Dictionary<string, string> ExtractMetadata(JsonElement meta)
    {
        var metadata = new Dictionary<string, string>();

        if (meta.TryGetProperty("custom_data", out var customData) &&
            customData.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in customData.EnumerateObject())
            {
                metadata[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return metadata;
    }
}
