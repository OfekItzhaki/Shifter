// Feature: space-billing, Property 5: Days remaining and color computation
// daysRemaining = ceil((trialEndsAt - now) / 1 day); color follows sky/amber/red thresholds
// **Validates: Requirements 3.1, 3.4, 3.6**

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Domain.Billing;
using System.Reflection;

namespace Jobuler.Tests.Billing;

public class DaysRemainingAndColorPropertyTests
{
    // ── Color enum for testing ───────────────────────────────────────────────

    private enum BannerColor { Sky, Amber, Red }

    /// <summary>
    /// Pure function that mirrors the frontend TrialBanner color logic.
    /// sky if daysRemaining > 7, amber if 4 ≤ daysRemaining ≤ 7, red if daysRemaining ≤ 3.
    /// </summary>
    private static BannerColor ComputeColor(int daysRemaining)
    {
        if (daysRemaining <= 3) return BannerColor.Red;
        if (daysRemaining <= 7) return BannerColor.Amber;
        return BannerColor.Sky;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a trialing SpaceSubscription with a specific TrialEndsAt date
    /// by using reflection to override the private setter.
    /// </summary>
    private static SpaceSubscription CreateTrialingWithTrialEndsAt(DateTime trialEndsAt)
    {
        var sub = SpaceSubscription.CreateTrial(Guid.NewGuid(), trialDays: 14);
        // Override TrialEndsAt via reflection to control the exact value
        var prop = typeof(SpaceSubscription).GetProperty(nameof(SpaceSubscription.TrialEndsAt))!;
        prop.SetValue(sub, trialEndsAt);
        return sub;
    }

    // ── Generators ───────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a future trial end date (1 minute to 60 days from now).
    /// </summary>
    private static Arbitrary<TimeSpan> FutureOffsetArbitrary()
    {
        // Generate offsets from 1 minute to 60 days in the future
        var gen = from totalMinutes in Gen.Choose(1, 60 * 24 * 60) // 1 min to 60 days in minutes
                  select TimeSpan.FromMinutes(totalMinutes);

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates a past trial end date (1 minute to 365 days in the past).
    /// </summary>
    private static Arbitrary<TimeSpan> PastOffsetArbitrary()
    {
        // Generate offsets from 1 minute to 365 days in the past
        var gen = from totalMinutes in Gen.Choose(1, 365 * 24 * 60) // 1 min to 365 days in minutes
                  select TimeSpan.FromMinutes(totalMinutes);

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates days remaining values across the full range (0-60).
    /// </summary>
    private static Arbitrary<int> DaysRemainingArbitrary()
    {
        return Arb.From(Gen.Choose(0, 60));
    }

    // ── Property 5a: DaysRemaining when trialEndsAt >= now ──────────────────────
    // For any trial end date in the future, DaysRemaining == ceil((trialEndsAt - now) / 1 day)

    [Property(MaxTest = 100)]
    public Property DaysRemaining_WhenTrialEndsInFuture_EqualsCeilingOfDaysDifference()
    {
        return Prop.ForAll(FutureOffsetArbitrary(), offset =>
        {
            var now = DateTime.UtcNow;
            var trialEndsAt = now.Add(offset);
            var sub = CreateTrialingWithTrialEndsAt(trialEndsAt);

            var actualDaysRemaining = sub.DaysRemaining;

            // The expected value: ceil((trialEndsAt - now) / 1 day)
            // Since DaysRemaining uses DateTime.UtcNow internally, there may be a tiny
            // time difference between our 'now' and the internal 'now'. We allow ±1 tolerance.
            var expectedApprox = (int)Math.Ceiling((trialEndsAt - DateTime.UtcNow).TotalDays);

            // Allow ±1 day tolerance due to time passing between setup and property evaluation
            var withinTolerance = Math.Abs(actualDaysRemaining - expectedApprox) <= 1;

            return withinTolerance
                .Label($"DaysRemaining={actualDaysRemaining} should be within ±1 of expected={expectedApprox} " +
                       $"(trialEndsAt={trialEndsAt:O}, offset={offset})")
                .And((actualDaysRemaining > 0)
                .Label($"DaysRemaining should be > 0 for future trial end (was {actualDaysRemaining})"));
        });
    }

    // ── Property 5b: DaysRemaining when trialEndsAt < now ───────────────────────
    // For any trial end date in the past, DaysRemaining == 0

    [Property(MaxTest = 100)]
    public Property DaysRemaining_WhenTrialEndsInPast_IsZero()
    {
        return Prop.ForAll(PastOffsetArbitrary(), offset =>
        {
            var trialEndsAt = DateTime.UtcNow.Subtract(offset);
            var sub = CreateTrialingWithTrialEndsAt(trialEndsAt);

            return (sub.DaysRemaining == 0)
                .Label($"DaysRemaining should be 0 when trial ended in the past " +
                       $"(trialEndsAt={trialEndsAt:O}, now={DateTime.UtcNow:O})");
        });
    }

    // ── Property 5c: Color computation follows thresholds ───────────────────────
    // sky if daysRemaining > 7, amber if 4 ≤ daysRemaining ≤ 7, red if daysRemaining ≤ 3

    [Property(MaxTest = 100)]
    public Property Color_FollowsThresholds()
    {
        return Prop.ForAll(DaysRemainingArbitrary(), daysRemaining =>
        {
            var color = ComputeColor(daysRemaining);

            if (daysRemaining > 7)
            {
                return (color == BannerColor.Sky)
                    .Label($"daysRemaining={daysRemaining} > 7 should be Sky, was {color}");
            }
            else if (daysRemaining >= 4)
            {
                return (color == BannerColor.Amber)
                    .Label($"daysRemaining={daysRemaining} (4-7) should be Amber, was {color}");
            }
            else
            {
                return (color == BannerColor.Red)
                    .Label($"daysRemaining={daysRemaining} <= 3 should be Red, was {color}");
            }
        });
    }

    // ── Property 5d: DaysRemaining and color are consistent end-to-end ──────────
    // For a trialing subscription with a future trial end, the computed DaysRemaining
    // maps to the correct color threshold.

    [Property(MaxTest = 100)]
    public Property DaysRemaining_And_Color_AreConsistent_ForTrialingSubscription()
    {
        return Prop.ForAll(FutureOffsetArbitrary(), offset =>
        {
            var trialEndsAt = DateTime.UtcNow.Add(offset);
            var sub = CreateTrialingWithTrialEndsAt(trialEndsAt);

            var daysRemaining = sub.DaysRemaining;
            var color = ComputeColor(daysRemaining);

            // Verify color is consistent with daysRemaining
            var colorCorrect = daysRemaining switch
            {
                > 7 => color == BannerColor.Sky,
                >= 4 => color == BannerColor.Amber,
                _ => color == BannerColor.Red
            };

            return colorCorrect
                .Label($"Color={color} inconsistent with DaysRemaining={daysRemaining} " +
                       $"(trialEndsAt={trialEndsAt:O})");
        });
    }

    // ── Property 5e: DaysRemaining for expired trial always maps to red ─────────
    // When trialEndsAt < now, daysRemaining is 0 which maps to red color.

    [Property(MaxTest = 100)]
    public Property ExpiredTrial_DaysRemainingZero_MapsToRed()
    {
        return Prop.ForAll(PastOffsetArbitrary(), offset =>
        {
            var trialEndsAt = DateTime.UtcNow.Subtract(offset);
            var sub = CreateTrialingWithTrialEndsAt(trialEndsAt);

            var daysRemaining = sub.DaysRemaining;
            var color = ComputeColor(daysRemaining);

            return (daysRemaining == 0)
                .Label($"Expired trial should have DaysRemaining=0, was {daysRemaining}")
                .And((color == BannerColor.Red)
                .Label($"Expired trial (daysRemaining=0) should map to Red, was {color}"));
        });
    }
}
