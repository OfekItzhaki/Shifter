using System.Text.Json;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Billing.Commands;

public record HandleSubscriptionUpdatedCommand(
    string Payload,
    Dictionary<string, string> Metadata) : IRequest;

public class HandleSubscriptionUpdatedCommandHandler : IRequestHandler<HandleSubscriptionUpdatedCommand>
{
    private readonly AppDbContext _db;
    private readonly ILogger<HandleSubscriptionUpdatedCommandHandler> _logger;

    private static readonly Dictionary<string, SubscriptionStatus> StatusMapping =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["active"] = SubscriptionStatus.Active,
            ["on_trial"] = SubscriptionStatus.Trialing,
            ["past_due"] = SubscriptionStatus.PastDue,
            ["cancelled"] = SubscriptionStatus.Canceled,
            ["expired"] = SubscriptionStatus.Expired,
        };

    public HandleSubscriptionUpdatedCommandHandler(
        AppDbContext db,
        ILogger<HandleSubscriptionUpdatedCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(HandleSubscriptionUpdatedCommand req, CancellationToken ct)
    {
        // ── Parse payload ────────────────────────────────────────────────────
        using var doc = JsonDocument.Parse(req.Payload);
        var root = doc.RootElement;

        var attributes = root.GetProperty("data").GetProperty("attributes");
        var lsSubscriptionId = root.GetProperty("data").GetProperty("id").GetString()!;
        var lsStatus = attributes.GetProperty("status").GetString()!;

        // ── Map status ───────────────────────────────────────────────────────
        if (!StatusMapping.TryGetValue(lsStatus, out var mappedStatus))
        {
            _logger.LogWarning(
                "Unrecognized LemonSqueezy status '{Status}' in subscription_updated event for subscription {SubscriptionId}. Skipping",
                lsStatus, lsSubscriptionId);
            return;
        }

        // ── Look up subscription ─────────────────────────────────────────────
        var subscription = await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.LemonSqueezySubscriptionId == lsSubscriptionId, ct);

        if (subscription is null)
        {
            _logger.LogWarning(
                "No GroupSubscription found for LemonSqueezy subscription ID {SubscriptionId}. Skipping subscription_updated",
                lsSubscriptionId);
            return;
        }

        // ── Parse period dates ───────────────────────────────────────────────
        DateTime? periodStart = null;
        DateTime? periodEnd = null;

        if (attributes.TryGetProperty("renews_at", out var renewsAtEl) && renewsAtEl.ValueKind == JsonValueKind.String)
            periodEnd = DateTime.Parse(renewsAtEl.GetString()!).ToUniversalTime();

        if (attributes.TryGetProperty("created_at", out var createdAtEl) && createdAtEl.ValueKind == JsonValueKind.String)
            periodStart = DateTime.Parse(createdAtEl.GetString()!).ToUniversalTime();

        // Use current_period_start/end if available (more accurate)
        if (attributes.TryGetProperty("current_period_start", out var cpStartEl) && cpStartEl.ValueKind == JsonValueKind.String)
            periodStart = DateTime.Parse(cpStartEl.GetString()!).ToUniversalTime();

        if (attributes.TryGetProperty("current_period_end", out var cpEndEl) && cpEndEl.ValueKind == JsonValueKind.String)
            periodEnd = DateTime.Parse(cpEndEl.GetString()!).ToUniversalTime();

        // ── No-op check: if status and period dates match, skip ──────────────
        var statusUnchanged = subscription.Status == mappedStatus;
        var periodUnchanged = periodStart == subscription.CurrentPeriodStart
                              && periodEnd == subscription.CurrentPeriodEnd;

        if (statusUnchanged && periodUnchanged)
        {
            _logger.LogInformation(
                "subscription_updated for {SubscriptionId} is a no-op (status={Status}, period unchanged). Skipping",
                lsSubscriptionId, mappedStatus);
            return;
        }

        // ── Update period dates if they differ ───────────────────────────────
        if (!periodUnchanged && periodStart.HasValue && periodEnd.HasValue)
        {
            subscription.UpdatePeriod(periodStart.Value, periodEnd.Value);
        }

        // ── Update status if it differs ──────────────────────────────────────
        var previousStatus = subscription.Status;
        if (!statusUnchanged)
        {
            subscription.UpdateStatus(mappedStatus);
        }

        // ── Reactivate group if transitioning to Active ──────────────────────
        if (mappedStatus == SubscriptionStatus.Active &&
            previousStatus is SubscriptionStatus.Trialing
                or SubscriptionStatus.PastDue
                or SubscriptionStatus.Canceled
                or SubscriptionStatus.Expired)
        {
            var group = await _db.Groups
                .FirstOrDefaultAsync(g => g.Id == subscription.GroupId && g.SpaceId == subscription.SpaceId, ct);

            if (group is not null && !group.IsActive)
            {
                group.Reactivate();
                _logger.LogInformation(
                    "Reactivated group {GroupId} after subscription {SubscriptionId} transitioned to Active from {PreviousStatus}",
                    subscription.GroupId, lsSubscriptionId, previousStatus);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Processed subscription_updated for {SubscriptionId}: status {PreviousStatus} → {NewStatus}",
            lsSubscriptionId, previousStatus, mappedStatus);
    }
}
