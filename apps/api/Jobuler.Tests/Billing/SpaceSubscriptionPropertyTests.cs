// Feature: space-billing, Properties 1, 9, 12: SpaceSubscription entity property tests
// Validates: Requirements 1.3, 1.4, 6.1, 6.2, 6.5, 6.6, 6.7

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Domain.Billing;

namespace Jobuler.Tests.Billing;

public class SpaceSubscriptionPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SpaceSubscription CreateActiveSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = SpaceSubscription.CreateTrial(Guid.NewGuid(), trialDays: 14);
        sub.Activate("pro", "ls_sub_123", "ls_cus_123", periodStart, periodEnd);
        return sub;
    }

    private static SpaceSubscription CreateTrialingSubscription(int trialDays = 14)
    {
        return SpaceSubscription.CreateTrial(Guid.NewGuid(), trialDays);
    }

    private static SpaceSubscription CreateCanceledSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = CreateActiveSubscription(periodStart, periodEnd);
        sub.Cancel();
        return sub;
    }

    private static SpaceSubscription CreateExpiredSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = CreateCanceledSubscription(periodStart, periodEnd);
        sub.Expire();
        return sub;
    }

    // ── Generators ───────────────────────────────────────────────────────────

    private static Arbitrary<int> TrialDaysArbitrary()
    {
        return Arb.From(Gen.Choose(1, 365));
    }

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

    private static Arbitrary<(DateTime periodStart, DateTime periodEnd)> PastPeriodEndArbitrary()
    {
        var gen = from offsetDays in Gen.Choose(2, 365)
                  from durationDays in Gen.Choose(1, (offsetDays - 1))
                  let start = DateTime.UtcNow.AddDays(-offsetDays)
                  let end = start.AddDays(durationDays)
                  select (start, end);

        return Arb.From(gen);
    }

    // ── Property 1: Trial date computation ──────────────────────────────────────
    // Feature: space-billing, Property 1: Trial date computation
    // For any positive trial duration (1–365), CreateTrial produces correct TrialStartsAt and TrialEndsAt
    // **Validates: Requirements 1.3, 1.4**

    [Property(MaxTest = 100)]
    public Property TrialDateComputation()
    {
        return Prop.ForAll(TrialDaysArbitrary(), trialDays =>
        {
            var before = DateTime.UtcNow;
            var sub = SpaceSubscription.CreateTrial(Guid.NewGuid(), trialDays);
            var after = DateTime.UtcNow;

            var startsAtInRange = sub.TrialStartsAt >= before && sub.TrialStartsAt <= after;
            var expectedEnd = sub.TrialStartsAt.AddDays(trialDays);
            var endsAtCorrect = sub.TrialEndsAt == expectedEnd;

            return startsAtInRange
                .Label($"TrialStartsAt should be between {before:O} and {after:O}, was {sub.TrialStartsAt:O}")
                .And(endsAtCorrect
                .Label($"TrialEndsAt should be {expectedEnd:O}, was {sub.TrialEndsAt:O} (trialDays={trialDays})"));
        });
    }

    // ── Property 9: Cancel state transition ─────────────────────────────────────
    // Feature: space-billing, Property 9: Cancel state transition
    // Active/Trialing → Cancel succeeds; Canceled/Expired → Cancel throws
    // **Validates: Requirements 6.1, 6.2**

    [Property(MaxTest = 100)]
    public Property Cancel_ActiveOrTrialing_Succeeds()
    {
        var activeGen = from dates in PeriodDatesArbitrary().Generator
                        select CreateActiveSubscription(dates.periodStart, dates.periodEnd);

        var trialingGen = from trialDays in Gen.Choose(1, 365)
                          select CreateTrialingSubscription(trialDays);

        var combinedGen = Gen.OneOf(activeGen, trialingGen);

        return Prop.ForAll(Arb.From(combinedGen), sub =>
        {
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

    [Property(MaxTest = 100)]
    public Property Cancel_CanceledOrExpired_Throws()
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

    // ── Property 12: Renewal preserves or resets period ─────────────────────────
    // Feature: space-billing, Property 12: Renewal preserves or resets period
    // Within grace period preserves dates; after expiry sets new dates; active throws
    // **Validates: Requirements 6.5, 6.6, 6.7**

    [Property(MaxTest = 100)]
    public Property RenewWithinGracePeriod_PreservesDates()
    {
        return Prop.ForAll(FuturePeriodEndArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateCanceledSubscription(periodStart, periodEnd);

            sub.RenewWithinGracePeriod();

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

    [Property(MaxTest = 100)]
    public Property RenewAfterExpiry_SetsNewDates()
    {
        var gen = from pastDates in PastPeriodEndArbitrary().Generator
                  from newDurationDays in Gen.Choose(1, 90)
                  let newStart = DateTime.UtcNow
                  let newEnd = DateTime.UtcNow.AddDays(newDurationDays)
                  select (pastDates.periodStart, pastDates.periodEnd, newStart, newEnd);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (oldStart, oldEnd, newStart, newEnd) = tuple;
            var sub = CreateExpiredSubscription(oldStart, oldEnd);

            sub.RenewAfterExpiry(newStart, newEnd);

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

    [Property(MaxTest = 100)]
    public Property RenewActive_Throws()
    {
        return Prop.ForAll(PeriodDatesArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateActiveSubscription(periodStart, periodEnd);

            var actGrace = () => sub.RenewWithinGracePeriod();
            actGrace.Should().Throw<InvalidOperationException>();

            var actExpiry = () => sub.RenewAfterExpiry(DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
            actExpiry.Should().Throw<InvalidOperationException>();
        });
    }
}
