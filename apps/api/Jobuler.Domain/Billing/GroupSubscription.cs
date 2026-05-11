using Jobuler.Domain.Common;

namespace Jobuler.Domain.Billing;

public enum SubscriptionStatus { Trialing, Active, PastDue, Canceled }

public class GroupSubscription : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public string TierId { get; private set; } = "trial";
    public SubscriptionStatus Status { get; private set; } = SubscriptionStatus.Trialing;
    public string? StripeSubscriptionId { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public DateTime? TrialEndsAt { get; private set; }
    public DateTime? CurrentPeriodStart { get; private set; }
    public DateTime? CurrentPeriodEnd { get; private set; }
    public int PeakMemberCount { get; private set; }
    public string? CouponCode { get; private set; }
    public int DiscountPercent { get; private set; }
    public DateTime? CanceledAt { get; private set; }

    private GroupSubscription() { }

    public static GroupSubscription CreateTrial(Guid spaceId, Guid groupId, int trialDays = 14) =>
        new()
        {
            SpaceId = spaceId,
            GroupId = groupId,
            TierId = "trial",
            Status = SubscriptionStatus.Trialing,
            TrialEndsAt = DateTime.UtcNow.AddDays(trialDays),
        };

    public void Activate(string tierId, string stripeSubscriptionId, string stripeCustomerId,
        DateTime periodStart, DateTime periodEnd)
    {
        TierId = tierId;
        Status = SubscriptionStatus.Active;
        StripeSubscriptionId = stripeSubscriptionId;
        StripeCustomerId = stripeCustomerId;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
    }

    public void UpdatePeakMemberCount(int currentCount)
    {
        if (currentCount > PeakMemberCount)
            PeakMemberCount = currentCount;
    }

    public void ResetPeakForNewPeriod(DateTime periodStart, DateTime periodEnd)
    {
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        PeakMemberCount = 0;
    }

    public void ApplyCoupon(string code, int discountPercent)
    {
        CouponCode = code;
        DiscountPercent = discountPercent;
    }

    public void Cancel()
    {
        Status = SubscriptionStatus.Canceled;
        CanceledAt = DateTime.UtcNow;
    }

    public bool IsTrialExpired => Status == SubscriptionStatus.Trialing
        && TrialEndsAt.HasValue && TrialEndsAt.Value < DateTime.UtcNow;

    public bool IsActive => Status == SubscriptionStatus.Active
        || (Status == SubscriptionStatus.Trialing && !IsTrialExpired);
}
