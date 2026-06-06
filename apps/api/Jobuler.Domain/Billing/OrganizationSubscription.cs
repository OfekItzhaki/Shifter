using Jobuler.Domain.Common;

namespace Jobuler.Domain.Billing;

public class OrganizationSubscription : AuditableEntity
{
    public Guid OrganizationId { get; private set; }
    public OrganizationBillingMode BillingMode { get; private set; } = OrganizationBillingMode.SharedSaaS;
    public string TierId { get; private set; } = "enterprise";
    public SubscriptionStatus Status { get; private set; } = SubscriptionStatus.Active;
    public string? ProviderSubscriptionId { get; private set; }
    public string? ProviderCustomerId { get; private set; }
    public DateTime CurrentPeriodStart { get; private set; }
    public DateTime? CurrentPeriodEnd { get; private set; }
    public int? CoveredSpaceLimit { get; private set; }
    public int? CoveredMemberLimit { get; private set; }
    public bool AutoRenew { get; private set; }
    public DateTime? CanceledAt { get; private set; }

    private OrganizationSubscription() { }

    public static OrganizationSubscription Create(
        Guid organizationId,
        OrganizationBillingMode billingMode,
        string tierId,
        DateTime currentPeriodStart,
        DateTime? currentPeriodEnd,
        bool autoRenew,
        string? providerSubscriptionId = null,
        string? providerCustomerId = null,
        int? coveredSpaceLimit = null,
        int? coveredMemberLimit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tierId);
        ValidateLimits(coveredSpaceLimit, coveredMemberLimit);
        ValidatePeriod(currentPeriodStart, currentPeriodEnd);

        return new OrganizationSubscription
        {
            OrganizationId = organizationId,
            BillingMode = billingMode,
            TierId = tierId.Trim(),
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = currentPeriodStart,
            CurrentPeriodEnd = currentPeriodEnd,
            AutoRenew = autoRenew,
            ProviderSubscriptionId = NormalizeOptional(providerSubscriptionId),
            ProviderCustomerId = NormalizeOptional(providerCustomerId),
            CoveredSpaceLimit = coveredSpaceLimit,
            CoveredMemberLimit = coveredMemberLimit
        };
    }

    public void UpdateCoverage(
        OrganizationBillingMode billingMode,
        string tierId,
        DateTime currentPeriodStart,
        DateTime? currentPeriodEnd,
        bool autoRenew,
        string? providerSubscriptionId,
        string? providerCustomerId,
        int? coveredSpaceLimit,
        int? coveredMemberLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tierId);
        ValidateLimits(coveredSpaceLimit, coveredMemberLimit);
        ValidatePeriod(currentPeriodStart, currentPeriodEnd);

        BillingMode = billingMode;
        TierId = tierId.Trim();
        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        AutoRenew = autoRenew;
        ProviderSubscriptionId = NormalizeOptional(providerSubscriptionId);
        ProviderCustomerId = NormalizeOptional(providerCustomerId);
        CoveredSpaceLimit = coveredSpaceLimit;
        CoveredMemberLimit = coveredMemberLimit;
        CanceledAt = null;
        Touch();
    }

    public void Cancel()
    {
        if (Status == SubscriptionStatus.Canceled || Status == SubscriptionStatus.Expired)
            throw new InvalidOperationException("Organization subscription is already canceled or expired.");

        Status = SubscriptionStatus.Canceled;
        CanceledAt = DateTime.UtcNow;
        Touch();
    }

    public void Expire()
    {
        if (Status != SubscriptionStatus.Canceled)
            throw new InvalidOperationException("Only canceled organization subscriptions can expire.");

        Status = SubscriptionStatus.Expired;
        Touch();
    }

    public bool IsAccessGranted
    {
        get
        {
            if (Status == SubscriptionStatus.Active)
                return CurrentPeriodEnd is null || CurrentPeriodEnd.Value > DateTime.UtcNow;

            if (Status == SubscriptionStatus.Canceled && CurrentPeriodEnd.HasValue)
                return CurrentPeriodEnd.Value > DateTime.UtcNow;

            return false;
        }
    }

    private static void ValidateLimits(int? coveredSpaceLimit, int? coveredMemberLimit)
    {
        if (coveredSpaceLimit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(coveredSpaceLimit), "Covered space limit must be positive.");

        if (coveredMemberLimit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(coveredMemberLimit), "Covered member limit must be positive.");
    }

    private static void ValidatePeriod(DateTime currentPeriodStart, DateTime? currentPeriodEnd)
    {
        if (currentPeriodEnd.HasValue && currentPeriodEnd.Value <= currentPeriodStart)
            throw new ArgumentException("Current period end must be after current period start.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
