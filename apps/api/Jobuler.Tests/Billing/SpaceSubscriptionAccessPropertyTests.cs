// Feature: space-billing, Properties 3, 4, 10, 11: Access and expiry logic property tests
// Validates: Requirements 2.1, 2.2, 2.3, 6.3, 6.4

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Domain.Billing;

namespace Jobuler.Tests.Billing;

public class SpaceSubscriptionAccessPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SpaceSubscription CreateActiveSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = SpaceSubscription.CreateTrial(Guid.NewGuid(), trialDays: 14);
        sub.Activate("pro", "ls_sub_123", "ls_cus_123", periodStart, periodEnd);
        return sub;
    }

    private static SpaceSubscription CreateTrialingSubscription(int trialDays)
    {
        // trialDays must be large enough that TrialEndsAt is in the future
        return SpaceSubscription.CreateTrial(Guid.NewGuid(), trialDays);
    }

    private static SpaceSubscription CreateCanceledWithFuturePeriodEnd(DateTime periodStart, DateTime periodEnd)
    {
        var sub = CreateActiveSubscription(periodStart, periodEnd);
        sub.Cancel();
        return sub;
    }

    private static SpaceSubscription CreateCanceledWithPastPeriodEnd(DateTime periodStart, DateTime periodEnd)
    {
        var sub = CreateActiveSubscription(periodStart, periodEnd);
        sub.Cancel();
        return sub;
    }

    private static SpaceSubscription CreateExpiredSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = CreateActiveSubscription(periodStart, periodEnd);
        sub.Cancel();
        sub.Expire();
        return sub;
    }

    // ── Generators ───────────────────────────────────────────────────────────

    /// <summary>
    /// Generates period dates where periodEnd is in the future (1-90 days from now).
    /// </summary>
    private static Arbitrary<(DateTime periodStart, DateTime periodEnd)> FuturePeriodEndArbitrary()
    {
        var gen = from offsetDays in Gen.Choose(1, 365)
                  from futureDays in Gen.Choose(1, 90)
                  let start = DateTime.UtcNow.AddDays(-offsetDays)
                  let end = DateTime.UtcNow.AddDays(futureDays)
                  select (start, end);

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates period dates where periodEnd is in the past (at least 1 day ago).
    /// </summary>
    private static Arbitrary<(DateTime periodStart, DateTime periodEnd)> PastPeriodEndArbitrary()
    {
        var gen = from offsetDays in Gen.Choose(2, 365)
                  from durationDays in Gen.Choose(1, offsetDays - 1)
                  let start = DateTime.UtcNow.AddDays(-offsetDays)
                  let end = start.AddDays(durationDays)
                  select (start, end);

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates trial days that ensure the trial has NOT expired (1-90 days in the future).
    /// </summary>
    private static Arbitrary<int> NonExpiredTrialDaysArbitrary()
    {
        // We need trialDays large enough that TrialEndsAt > DateTime.UtcNow.
        // Since CreateTrial sets TrialEndsAt = now + trialDays, any positive value works
        // as long as the test runs quickly (which it does).
        return Arb.From(Gen.Choose(1, 365));
    }

    // ── Property 3: Access granted when active or trialing ──────────────────────
    // Feature: space-billing, Property 3: Access granted when active or trialing
    // IsAccessGranted returns true for Active/Trialing (non-expired)
    // **Validates: Requirements 2.1, 2.2**

    [Property(MaxTest = 100)]
    public Property AccessGranted_WhenActive()
    {
        return Prop.ForAll(FuturePeriodEndArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateActiveSubscription(periodStart, periodEnd);

            return sub.IsAccessGranted
                .Label($"Active subscription should grant access (Status={sub.Status})");
        });
    }

    [Property(MaxTest = 100)]
    public Property AccessGranted_WhenTrialing_NotExpired()
    {
        return Prop.ForAll(NonExpiredTrialDaysArbitrary(), trialDays =>
        {
            var sub = CreateTrialingSubscription(trialDays);

            // Since we just created the trial, TrialEndsAt is in the future
            return sub.IsAccessGranted
                .Label($"Trialing subscription (not expired) should grant access (TrialEndsAt={sub.TrialEndsAt:O}, now={DateTime.UtcNow:O})");
        });
    }

    // ── Property 4: Access denied when inactive ─────────────────────────────────
    // Feature: space-billing, Property 4: Access denied when inactive
    // IsAccessGranted returns false for Expired/PastDue/Canceled-past-period
    // **Validates: Requirements 2.3**

    [Property(MaxTest = 100)]
    public Property AccessDenied_WhenExpired()
    {
        return Prop.ForAll(PastPeriodEndArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateExpiredSubscription(periodStart, periodEnd);

            return (!sub.IsAccessGranted)
                .Label($"Expired subscription should deny access (Status={sub.Status})");
        });
    }

    [Property(MaxTest = 100)]
    public Property AccessDenied_WhenCanceled_PastPeriodEnd()
    {
        return Prop.ForAll(PastPeriodEndArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateCanceledWithPastPeriodEnd(periodStart, periodEnd);

            // periodEnd is in the past, so CurrentPeriodEnd <= now → access denied
            return (!sub.IsAccessGranted)
                .Label($"Canceled subscription with past period end should deny access (CurrentPeriodEnd={periodEnd:O}, now={DateTime.UtcNow:O})");
        });
    }

    // ── Property 10: Grace period access ────────────────────────────────────────
    // Feature: space-billing, Property 10: Grace period access
    // Canceled with CurrentPeriodEnd > now still grants access
    // **Validates: Requirements 6.3**

    [Property(MaxTest = 100)]
    public Property GracePeriodAccess_WhenCanceled_FuturePeriodEnd()
    {
        return Prop.ForAll(FuturePeriodEndArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateCanceledWithFuturePeriodEnd(periodStart, periodEnd);

            return sub.IsAccessGranted
                .Label($"Canceled subscription with future period end should grant access (CurrentPeriodEnd={periodEnd:O}, now={DateTime.UtcNow:O})");
        });
    }

    // ── Property 11: Expiry state transition ────────────────────────────────────
    // Feature: space-billing, Property 11: Expiry state transition
    // Canceled with CurrentPeriodEnd <= now can be expired
    // **Validates: Requirements 6.4**

    [Property(MaxTest = 100)]
    public Property ExpiryTransition_WhenCanceled_PastPeriodEnd_Succeeds()
    {
        return Prop.ForAll(PastPeriodEndArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateCanceledWithPastPeriodEnd(periodStart, periodEnd);

            // Expire should succeed for canceled subscriptions with past period end
            sub.Expire();

            return (sub.Status == SubscriptionStatus.Expired)
                .Label($"Expire() should transition to Expired (was Canceled with past period end={periodEnd:O})");
        });
    }

    [Property(MaxTest = 100)]
    public Property ExpiryTransition_WhenNotCanceled_Throws()
    {
        var activeGen = from dates in FuturePeriodEndArbitrary().Generator
                        select CreateActiveSubscription(dates.periodStart, dates.periodEnd);

        var trialingGen = from trialDays in Gen.Choose(1, 365)
                          select CreateTrialingSubscription(trialDays);

        var expiredGen = from dates in PastPeriodEndArbitrary().Generator
                         select CreateExpiredSubscription(dates.periodStart, dates.periodEnd);

        var combinedGen = Gen.OneOf(activeGen, trialingGen, expiredGen);

        return Prop.ForAll(Arb.From(combinedGen), sub =>
        {
            var act = () => sub.Expire();
            act.Should().Throw<InvalidOperationException>();
        });
    }
}
