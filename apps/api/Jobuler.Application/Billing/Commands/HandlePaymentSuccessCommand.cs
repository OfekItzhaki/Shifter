using System.Text.Json;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Billing.Commands;

public record HandlePaymentSuccessCommand(
    string Payload,
    Dictionary<string, string> Metadata) : IRequest;

public class HandlePaymentSuccessCommandHandler : IRequestHandler<HandlePaymentSuccessCommand>
{
    private readonly AppDbContext _db;
    private readonly ILogger<HandlePaymentSuccessCommandHandler> _logger;

    public HandlePaymentSuccessCommandHandler(
        AppDbContext db,
        ILogger<HandlePaymentSuccessCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(HandlePaymentSuccessCommand req, CancellationToken ct)
    {
        // ── Parse payload ────────────────────────────────────────────────────
        using var doc = JsonDocument.Parse(req.Payload);
        var root = doc.RootElement;

        var attributes = root.GetProperty("data").GetProperty("attributes");
        var subscriptionId = attributes.GetProperty("subscription_id").GetInt64().ToString();

        // ── Look up GroupSubscription by LemonSqueezy subscription ID ────────
        var subscription = await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.LemonSqueezySubscriptionId == subscriptionId, ct);

        if (subscription is null)
        {
            _logger.LogWarning(
                "No GroupSubscription found for LemonSqueezySubscriptionId={SubscriptionId}. Skipping subscription_payment_success",
                subscriptionId);
            return;
        }

        // ── Extract new period dates from payload ────────────────────────────
        DateTime periodStart;
        DateTime periodEnd;

        if (attributes.TryGetProperty("current_period_start", out var periodStartProp)
            && periodStartProp.ValueKind != JsonValueKind.Null)
        {
            periodStart = periodStartProp.GetDateTime();
        }
        else
        {
            // Fallback: use current period start if not provided
            periodStart = subscription.CurrentPeriodStart ?? DateTime.UtcNow;
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
            // Fallback: use current period end if not provided
            periodEnd = subscription.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1);
        }

        // ── Update billing period (resets PeakMemberCount if period start differs) ──
        subscription.UpdatePeriod(periodStart, periodEnd);

        // ── If subscription is PastDue, transition to Active ─────────────────
        if (subscription.Status == SubscriptionStatus.PastDue)
        {
            subscription.UpdateStatus(SubscriptionStatus.Active);
        }

        await _db.SaveChangesAsync(ct);
    }
}
