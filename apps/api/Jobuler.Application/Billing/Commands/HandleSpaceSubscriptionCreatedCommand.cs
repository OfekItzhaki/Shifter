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

        // ── Parse payload ────────────────────────────────────────────────────
        using var doc = JsonDocument.Parse(req.Payload);
        var root = doc.RootElement;

        var data = root.GetProperty("data");
        var attributes = data.GetProperty("attributes");

        var lsSubscriptionId = data.GetProperty("id").GetString()!;
        var lsCustomerId = attributes.GetProperty("customer_id").GetInt64().ToString();

        // ── Determine tier from variant_id ───────────────────────────────────
        var tierId = "pro"; // Default tier
        if (attributes.TryGetProperty("variant_id", out var variantIdProp))
        {
            tierId = variantIdProp.GetInt64().ToString();
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
            _logger.LogWarning(
                "No SpaceSubscription found for SpaceId={SpaceId}. Skipping subscription_created",
                spaceId);
            return;
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
