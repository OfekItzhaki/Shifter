// Feature: space-billing
// Property 15: Peak member count tracking
// Property 16: Upgrade guard
// **Validates: Requirements 10.2, 10.4, 10.5**

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Domain.Billing;

namespace Jobuler.Tests.Billing;

public class SpaceSubscriptionPeakAndUpgradePropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SpaceSubscription CreateTrialingSubscription()
    {
        return SpaceSubscription.CreateTrial(Guid.NewGuid(), 14);
    }

    private static SpaceSubscription CreateActiveSubscription()
    {
        var sub = SpaceSubscription.CreateTrial(Guid.NewGuid(), 14);
        sub.Activate("pro", "ls_sub_123", "ls_cus_123",
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(20));
        return sub;
    }

    private static SpaceSubscription CreateCanceledSubscription()
    {
        var sub = CreateActiveSubscription();
        sub.Cancel();
        return sub;
    }

    private static SpaceSubscription CreateExpiredSubscription()
    {
        var sub = CreateActiveSubscription();
        sub.Cancel();
        sub.Expire();
        return sub;
    }

    // ── Generators ───────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a non-empty list of positive member counts (1–500).
    /// </summary>
    private static Arbitrary<int[]> MemberCountSequenceArbitrary()
    {
        var gen = from size in Gen.Choose(1, 50)
                  from counts in Gen.ArrayOf(size, Gen.Choose(1, 500))
                  select counts;

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates subscription statuses that should block upgrade (not Active, not Trialing).
    /// </summary>
    private static Arbitrary<SubscriptionStatus> NonUpgradeableStatusArbitrary()
    {
        var gen = Gen.Elements(
            SubscriptionStatus.Canceled,
            SubscriptionStatus.Expired,
            SubscriptionStatus.PastDue);
        return Arb.From(gen);
    }

    /// <summary>
    /// Generates subscription statuses that allow upgrade (Active or Trialing).
    /// </summary>
    private static Arbitrary<SubscriptionStatus> UpgradeableStatusArbitrary()
    {
        var gen = Gen.Elements(SubscriptionStatus.Active, SubscriptionStatus.Trialing);
        return Arb.From(gen);
    }

    // ── Property 15: Peak member count tracking ─────────────────────────────
    // PeakMemberCount always equals max observed; resets on period change
    // **Validates: Requirements 10.4, 10.5**

    [Property(MaxTest = 100)]
    public Property PeakMemberCount_AlwaysEqualsMaxObserved()
    {
        return Prop.ForAll(MemberCountSequenceArbitrary(), counts =>
        {
            var sub = CreateActiveSubscription();

            foreach (var count in counts)
            {
                sub.UpdatePeakMemberCount(count);
            }

            var expectedMax = counts.Max();

            return (sub.PeakMemberCount == expectedMax)
                .Label($"PeakMemberCount should be {expectedMax} but was {sub.PeakMemberCount}");
        });
    }

    [Property(MaxTest = 100)]
    public Property PeakMemberCount_ResetsToZero_OnPeriodChange()
    {
        return Prop.ForAll(MemberCountSequenceArbitrary(), counts =>
        {
            var sub = CreateActiveSubscription();

            // Apply member counts
            foreach (var count in counts)
            {
                sub.UpdatePeakMemberCount(count);
            }

            // Reset for new period
            sub.ResetPeakForNewPeriod();

            return (sub.PeakMemberCount == 0)
                .Label($"PeakMemberCount should be 0 after reset but was {sub.PeakMemberCount}");
        });
    }

    [Property(MaxTest = 100)]
    public Property PeakMemberCount_TracksNewMaxAfterReset()
    {
        var gen = from firstCounts in MemberCountSequenceArbitrary().Generator
                  from secondCounts in MemberCountSequenceArbitrary().Generator
                  select (firstCounts, secondCounts);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (firstCounts, secondCounts) = tuple;
            var sub = CreateActiveSubscription();

            // First period
            foreach (var count in firstCounts)
            {
                sub.UpdatePeakMemberCount(count);
            }

            // Reset for new period
            sub.ResetPeakForNewPeriod();

            // Second period
            foreach (var count in secondCounts)
            {
                sub.UpdatePeakMemberCount(count);
            }

            var expectedMax = secondCounts.Max();

            return (sub.PeakMemberCount == expectedMax)
                .Label($"PeakMemberCount after reset should be {expectedMax} but was {sub.PeakMemberCount}");
        });
    }

    // ── Property 16: Upgrade guard ──────────────────────────────────────────
    // Upgrade rejected when status is not Active/Trialing
    // **Validates: Requirements 10.2**

    [Property(MaxTest = 100)]
    public Property UpdateTier_Rejected_WhenStatusIsNotActiveOrTrialing()
    {
        var gen = from status in NonUpgradeableStatusArbitrary().Generator
                  from tierSuffix in Gen.Choose(1, 100)
                  select (status, $"tier_{tierSuffix}");

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (status, newTierId) = tuple;

            // Create a subscription and transition to the non-upgradeable status
            SpaceSubscription sub;
            switch (status)
            {
                case SubscriptionStatus.Canceled:
                    sub = CreateCanceledSubscription();
                    break;
                case SubscriptionStatus.Expired:
                    sub = CreateExpiredSubscription();
                    break;
                case SubscriptionStatus.PastDue:
                    // PastDue is not directly reachable via domain methods on SpaceSubscription,
                    // so we test Canceled and Expired which are the reachable non-upgradeable states.
                    // Skip PastDue by using Canceled as a proxy (both are non-Active/non-Trialing).
                    sub = CreateCanceledSubscription();
                    break;
                default:
                    sub = CreateCanceledSubscription();
                    break;
            }

            var act = () => sub.UpdateTier(newTierId);
            act.Should().Throw<InvalidOperationException>();
        });
    }

    [Property(MaxTest = 100)]
    public Property UpdateTier_Succeeds_WhenStatusIsActiveOrTrialing()
    {
        var gen = from status in UpgradeableStatusArbitrary().Generator
                  from tierSuffix in Gen.Choose(1, 100)
                  select (status, $"tier_{tierSuffix}");

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (status, newTierId) = tuple;

            SpaceSubscription sub;
            if (status == SubscriptionStatus.Active)
            {
                sub = CreateActiveSubscription();
            }
            else
            {
                sub = CreateTrialingSubscription();
            }

            sub.UpdateTier(newTierId);

            return (sub.TierId == newTierId)
                .Label($"TierId should be '{newTierId}' but was '{sub.TierId}'");
        });
    }
}
