// Feature: subscription-cancellation
// Properties 1, 2, 8, 9, 11: Subscription lifecycle state transitions
// Validates: Requirements 1.1, 1.3, 3.1, 3.2, 3.5

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Domain.Billing;

namespace Jobuler.Tests.Billing;

public class SubscriptionLifecyclePropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GroupSubscription CreateActiveSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = GroupSubscription.CreateTrial(Guid.NewGuid(), Guid.NewGuid(), trialDays: 14);
        sub.Activate("pro", "stripe_sub_123", "stripe_cus_123", periodStart, periodEnd);
        return sub;
    }

    private static GroupSubscription CreateCanceledSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = CreateActiveSubscription(periodStart, periodEnd);
        sub.Cancel();
        return sub;
    }

    private static GroupSubscription CreateExpiredSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = CreateCanceledSubscription(periodStart, periodEnd);
        sub.Expire();
        return sub;
    }

    // ── Generators ───────────────────────────────────────────────────────────

    private static Arbitrary<(DateTime periodStart, DateTime periodEnd)> PeriodDatesArbitrary()
    {
        var gen = from offsetDays in Gen.Choose(1, 365)
                  from durationDays in Gen.Choose(1, 90)
                  let start = DateTime.UtcNow.AddDays(-offsetDays)
                  let end = start.AddDays(durationDays)
                  select (start, end);

        return Arb.From(gen);
    }

    private static Arbitrary<(DateTime periodStart, DateTime periodEnd)> FuturePeriodEndArbitrary()
    {
        var gen = from offsetDays in Gen.Choose(1, 365)
                  from futureDays in Gen.Choose(1, 90)
                  let start = DateTime.UtcNow.AddDays(-offsetDays)
                  let end = DateTime.UtcNow.AddDays(futureDays)
                  select (start, end);

        return Arb.From(gen);
    }

    // ── Property 1: Cancel transitions subscription to Canceled with timestamp ──
    // **Validates: Requirements 1.1**

    [Property(MaxTest = 100)]
    public Property Cancel_TransitionsToCanceled_WithTimestamp()
    {
        return Prop.ForAll(PeriodDatesArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateActiveSubscription(periodStart, periodEnd);

            var beforeCancel = DateTime.UtcNow;
            sub.Cancel();

            return (sub.Status == SubscriptionStatus.Canceled)
                .Label("Status should be Canceled")
                .And((sub.CanceledAt != null)
                .Label("CanceledAt should not be null"))
                .And((sub.CanceledAt!.Value >= beforeCancel)
                .Label("CanceledAt should be >= time before cancel"));
        });
    }

    // ── Property 2: Already-canceled subscription rejects cancellation ──────────
    // **Validates: Requirements 1.3**

    [Property(MaxTest = 100)]
    public Property AlreadyCanceled_RejectsCancellation()
    {
        var canceledGen = from dates in PeriodDatesArbitrary().Generator
                          select CreateCanceledSubscription(dates.periodStart, dates.periodEnd);

        var expiredGen = from dates in PeriodDatesArbitrary().Generator
                         select CreateExpiredSubscription(dates.periodStart, dates.periodEnd);

        var combinedGen = Gen.OneOf(canceledGen, expiredGen);

        return Prop.ForAll(Arb.From(combinedGen), sub =>
        {
            var act = () => sub.Cancel();
            act.Should().Throw<InvalidOperationException>();
        });
    }

    // ── Property 11: Active subscription rejects renewal ────────────────────────
    // **Validates: Requirements 3.5**

    [Property(MaxTest = 100)]
    public Property ActiveSubscription_RejectsRenewal()
    {
        return Prop.ForAll(PeriodDatesArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateActiveSubscription(periodStart, periodEnd);

            var act = () => sub.Renew(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
            act.Should().Throw<InvalidOperationException>();
        });
    }

    // ── Property 8: Renew canceled subscription within period reverts to active ─
    // **Validates: Requirements 3.1**

    [Property(MaxTest = 100)]
    public Property RenewCanceled_WithinPeriod_RevertsToActive_PreservesDates()
    {
        return Prop.ForAll(FuturePeriodEndArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateCanceledSubscription(periodStart, periodEnd);

            // Renew with the same period dates (preserving existing period)
            sub.Renew(periodStart, periodEnd);

            return (sub.Status == SubscriptionStatus.Active)
                .Label("Status should be Active")
                .And((sub.CanceledAt == null)
                .Label("CanceledAt should be null"))
                .And((sub.CurrentPeriodStart == periodStart)
                .Label("CurrentPeriodStart should be preserved"))
                .And((sub.CurrentPeriodEnd == periodEnd)
                .Label("CurrentPeriodEnd should be preserved"));
        });
    }

    // ── Property 9: Renew expired subscription creates new billing period ───────
    // **Validates: Requirements 3.2**

    [Property(MaxTest = 100)]
    public Property RenewExpired_CreatesNewBillingPeriod()
    {
        var gen = from oldOffsetDays in Gen.Choose(1, 365)
                  from oldDurationDays in Gen.Choose(1, 90)
                  from newDurationDays in Gen.Choose(1, 90)
                  let oldStart = DateTime.UtcNow.AddDays(-(oldOffsetDays + oldDurationDays))
                  let oldEnd = DateTime.UtcNow.AddDays(-oldOffsetDays)
                  let newStart = DateTime.UtcNow
                  let newEnd = DateTime.UtcNow.AddDays(newDurationDays)
                  select (oldStart, oldEnd, newStart, newEnd);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (oldStart, oldEnd, newStart, newEnd) = tuple;
            var sub = CreateExpiredSubscription(oldStart, oldEnd);

            sub.Renew(newStart, newEnd);

            return (sub.Status == SubscriptionStatus.Active)
                .Label("Status should be Active")
                .And((sub.CanceledAt == null)
                .Label("CanceledAt should be null"))
                .And((sub.CurrentPeriodStart == newStart)
                .Label("CurrentPeriodStart should be updated to new value"))
                .And((sub.CurrentPeriodEnd == newEnd)
                .Label("CurrentPeriodEnd should be updated to new value"));
        });
    }
}
