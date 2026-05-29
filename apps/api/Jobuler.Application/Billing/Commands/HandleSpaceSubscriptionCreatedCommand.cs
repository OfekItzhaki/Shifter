using System.Text.Json;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Billing.Commands;

public record HandleSpaceSubscriptionCreatedCommand(
    string Payload,
    Dictionary<string, string> Metadata) : IRequest;

public class HandleSpaceSubscriptionCreatedCommandHandler : IRequestHandler<HandleSpaceSubscriptionCreatedCommand>
{
    private readonly AppDbContext _db;
    private readonly IStatisticsPeriodService _statisticsPeriods;
    private readonly ILogger<HandleSpaceSubscriptionCreatedCommandHandler> _logger;

    public HandleSpaceSubscriptionCreatedCommandHandler(
        AppDbContext db,
        IStatisticsPeriodService statisticsPeriods,
        ILogger<HandleSpaceSubscriptionCreatedCommandHandler> logger)
    {
        _db = db;
        _statisticsPeriods = statisticsPeriods;
        _logger = logger;
    }

    public async Task Handle(HandleSpaceSubscriptionCreatedCommand req, CancellationToken ct)
    {
        // ── Extract space_id from metadata ───────────────────────────────────
        if (!req.Metadata.TryGetValue("space_id", out var spaceIdStr) ||
            !Guid.TryParse(spaceIdStr, out var spaceId))
        {
            _logger.LogWarning(
                "subscription_created webhook missing or invalid space_id in metadata. Skipping space subscription activation");
            return;
        }

        try
        {
            await ProcessSubscriptionCreatedAsync(req, spaceId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process subscription_created for SpaceId={SpaceId}. Payload may have unexpected format",
                spaceId);
            throw; // Re-throw so the webhook returns 500 and LemonSqueezy retries
        }
    }

    private async Task ProcessSubscriptionCreatedAsync(HandleSpaceSubscriptionCreatedCommand req, Guid spaceId, CancellationToken ct)
    {

        // ── Parse payload ────────────────────────────────────────────────────
        using var doc = JsonDocument.Parse(req.Payload);
        var root = doc.RootElement;

        var data = root.GetProperty("data");
        var attributes = data.GetProperty("attributes");

        var lsSubscriptionId = data.GetProperty("id").ToString();
        
        // customer_id can be either a number or string depending on LS API version
        string lsCustomerId;
        if (attributes.TryGetProperty("customer_id", out var customerIdProp))
        {
            lsCustomerId = customerIdProp.ValueKind == JsonValueKind.Number
                ? customerIdProp.GetInt64().ToString()
                : customerIdProp.GetString() ?? "";
        }
        else
        {
            lsCustomerId = "";
        }

        // ── Determine tier from variant_id ───────────────────────────────────
        var tierId = "pro"; // Default tier
        if (attributes.TryGetProperty("variant_id", out var variantIdProp))
        {
            tierId = variantIdProp.ValueKind == JsonValueKind.Number
                ? variantIdProp.GetInt64().ToString()
                : variantIdProp.GetString() ?? "pro";
        }

        // ── Extract period dates ─────────────────────────────────────────────
        DateTime periodStart;
        DateTime periodEnd;

        if (attributes.TryGetProperty("current_period_start", out var periodStartProp)
            && periodStartProp.ValueKind != JsonValueKind.Null)
        {
            periodStart = periodStartProp.GetDateTime();
        }
        else
        {
            periodStart = DateTime.UtcNow;
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
        else
        {
            periodEnd = periodStart.AddMonths(1);
        }

        // ── Load SpaceSubscription ───────────────────────────────────────────
        var sub = await _db.SpaceSubscriptions
            .FirstOrDefaultAsync(s => s.SpaceId == spaceId, ct);

        if (sub is null)
        {
            // No SpaceSubscription exists — create one and activate it directly.
            // This handles cases where the trial record wasn't created during space setup.
            _logger.LogWarning(
                "No SpaceSubscription found for SpaceId={SpaceId}. Creating and activating one",
                spaceId);
            sub = SpaceSubscription.CreateTrial(spaceId, 1); // 1-day trial (will be immediately activated below)
            _db.SpaceSubscriptions.Add(sub);
            await _db.SaveChangesAsync(ct);
        }

        // ── Already active with LS ID — no-op (idempotency) ─────────────────
        if (sub.Status == SubscriptionStatus.Active
            && !string.IsNullOrEmpty(sub.LemonSqueezySubscriptionId))
        {
            _logger.LogInformation(
                "SpaceSubscription for SpaceId={SpaceId} already active with LemonSqueezy ID. No-op",
                spaceId);
            return;
        }

        // ── Activate the subscription ────────────────────────────────────────
        sub.Activate(tierId, lsSubscriptionId, lsCustomerId, periodStart, periodEnd);

        // ── Trigger statistics period rotation ────────────────────────────────
        await _statisticsPeriods.OnSubscriptionActivatedAsync(spaceId, periodStart, ct);

        // ── Save changes ─────────────────────────────────────────────────────
        await _db.SaveChangesAsync(ct);
    }
}
