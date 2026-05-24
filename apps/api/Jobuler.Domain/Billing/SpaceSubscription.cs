using Jobuler.Domain.Common;

namespace Jobuler.Domain.Billing;

public class SpaceSubscription : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public string TierId { get; private set; } = "trial";
    public SubscriptionStatus Status { get; private set; } = SubscriptionStatus.Trialing;
    public string? LemonSqueezySubscriptionId { get; private set; }
    public string? LemonSqueezyCustomerId { get; private set; }
    public DateTime TrialStartsAt { get; private set; }
    public DateTime TrialEndsAt { get; private set; }
    public DateTime? CurrentPeriodStart { get; private set; }
    public DateTime? CurrentPeriodEnd { get; private set; }
    public int PeakMemberCount { get; private set; }
    public DateTime? CanceledAt { get; private set; }
    public bool AutoRenew { get; private set; } = true;

    private SpaceSubscription() { }

    // Factory method
    public static SpaceSubscription CreateTrial(Guid spaceId, int trialDays)
    {
        if (trialDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(trialDays), "Trial duration must be positive.");

        var now = DateTime.UtcNow;
        return new SpaceSubscription
        {
            SpaceId = spaceId,
            TierId = "trial",
            Status = SubscriptionStatus.Trialing,
            TrialStartsAt = now,
            TrialEndsAt = now.AddDays(trialDays),
        };
    }

    // State transitions

    public void Activate(string tierId, string lsSubscriptionId, string lsCustomerId,
        DateTime periodStart, DateTime periodEnd)
    {
        if (Status == SubscriptionStatus.Active)
            throw new InvalidOperationException("Subscription is already active.");

        TierId = tierId;
        Status = SubscriptionStatus.Active;
        LemonSqueezySubscriptionId = lsSubscriptionId;
        LemonSqueezyCustomerId = lsCustomerId;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        CanceledAt = null;
        Touch();
    }

    public void Cancel()
    {
        if (Status == SubscriptionStatus.Canceled || Status == SubscriptionStatus.Expired)
            throw new InvalidOperationException("Subscription is already canceled or expired.");

        Status = SubscriptionStatus.Canceled;
        CanceledAt = DateTime.UtcNow;
        Touch();
    }

    public void Expire()
    {
        if (Status != SubscriptionStatus.Canceled)
            throw new InvalidOperationException("Only canceled subscriptions can expire.");

        Status = SubscriptionStatus.Expired;
        Touch();
    }

    public void RenewWithinGracePeriod()
    {
        if (Status == SubscriptionStatus.Active)
            throw new InvalidOperationException("Subscription is already active and does not need renewal.");

        if (Status != SubscriptionStatus.Canceled)
            throw new InvalidOperationException("Only canceled subscriptions can be renewed within grace period.");

        if (CurrentPeriodEnd.HasValue && CurrentPeriodEnd.Value <= DateTime.UtcNow)
            throw new InvalidOperationException("Grace period has passed. Use RenewAfterExpiry instead.");

        Status = SubscriptionStatus.Active;
        CanceledAt = null;
        Touch();
    }

    public void RenewAfterExpiry(DateTime newPeriodStart, DateTime newPeriodEnd)
    {
        if (Status == SubscriptionStatus.Active)
            throw new InvalidOperationException("Subscription is already active and does not need renewal.");

        if (Status != SubscriptionStatus.Expired && Status != SubscriptionStatus.Canceled)
            throw new InvalidOperationException("Only expired or canceled subscriptions can be renewed after expiry.");

        Status = SubscriptionStatus.Active;
        CanceledAt = null;
        CurrentPeriodStart = newPeriodStart;
        CurrentPeriodEnd = newPeriodEnd;
        Touch();
    }

    public void UpdatePeriod(DateTime periodStart, DateTime periodEnd)
    {
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        Touch();
    }

    public void UpdateTier(string newTierId)
    {
        if (Status != SubscriptionStatus.Active && Status != SubscriptionStatus.Trialing)
            throw new InvalidOperationException("Subscription must be active or trialing to change tier.");

        TierId = newTierId;
        Touch();
    }

    public void UpdatePeakMemberCount(int currentCount)
    {
        if (currentCount > PeakMemberCount)
        {
            PeakMemberCount = currentCount;
            Touch();
        }
    }

    public void ResetPeakForNewPeriod()
    {
        PeakMemberCount = 0;
        Touch();
    }

    public void SetAutoRenew(bool autoRenew)
    {
        AutoRenew = autoRenew;
        Touch();
    }

    // Computed properties

    public bool IsAccessGranted
    {
        get
        {
            if (Status == SubscriptionStatus.Active)
                return true;

            if (Status == SubscriptionStatus.Trialing)
                return !IsTrialExpired;

            if (Status == SubscriptionStatus.Canceled && CurrentPeriodEnd.HasValue)
                return CurrentPeriodEnd.Value > DateTime.UtcNow;

            return false;
        }
    }

    public bool IsTrialExpired =>
        Status == SubscriptionStatus.Trialing && TrialEndsAt < DateTime.UtcNow;

    public int DaysRemaining
    {
        get
        {
            var now = DateTime.UtcNow;

            if (Status == SubscriptionStatus.Trialing)
            {
                if (TrialEndsAt <= now)
                    return 0;
                return (int)Math.Ceiling((TrialEndsAt - now).TotalDays);
            }

            if (Status == SubscriptionStatus.Active || Status == SubscriptionStatus.Canceled)
            {
                if (CurrentPeriodEnd.HasValue)
                {
                    if (CurrentPeriodEnd.Value <= now)
                        return 0;
                    return (int)Math.Ceiling((CurrentPeriodEnd.Value - now).TotalDays);
                }
            }

            return 0;
        }
    }
}
