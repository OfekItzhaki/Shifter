// Feature: split-burden-scaling
// Property 1: Burden scaling formula correctness
// Validates: Requirements 2.1, 3.1, 3.5

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Domain.Tasks;
using Xunit;

namespace Jobuler.Tests.Domain;

public class BurdenScalingPropertyTests
{
    /// <summary>
    /// Custom Arbitrary that generates valid (TaskBurdenLevel, splitCount, shiftDurationMinutes) tuples.
    /// splitCount ∈ [1, 10], shiftDurationMinutes ∈ [1, 1440]
    /// </summary>
    private static Arbitrary<(TaskBurdenLevel burden, int splitCount, int shiftDurationMinutes)> BurdenInputArbitrary()
    {
        var gen = from burden in Gen.Elements(TaskBurdenLevel.Easy, TaskBurdenLevel.Normal, TaskBurdenLevel.Hard)
                  from splitCount in Gen.Choose(1, 10)
                  from shiftDurationMinutes in Gen.Choose(1, 1440)
                  select (burden, splitCount, shiftDurationMinutes);

        return Arb.From(gen);
    }

    // ── Property 1a: Returns original burden when splitCount == 1 ─────────────
    // **Validates: Requirements 2.1, 3.1, 3.5**

    [Property(MaxTest = 200)]
    public Property SplitCount1_ReturnsOriginalBurden()
    {
        var gen = from burden in Gen.Elements(TaskBurdenLevel.Easy, TaskBurdenLevel.Normal, TaskBurdenLevel.Hard)
                  from shiftDurationMinutes in Gen.Choose(1, 1440)
                  select (burden, shiftDurationMinutes);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (burden, shiftDurationMinutes) = tuple;
            var result = BurdenScalingService.ComputeEffectiveBurden(burden, splitCount: 1, shiftDurationMinutes);
            return result == burden;
        });
    }

    // ── Property 1b: Returns original burden when original duration < 240 ─────
    // **Validates: Requirements 2.1, 3.1, 3.5**

    [Property(MaxTest = 200)]
    public Property BelowThreshold_ReturnsOriginalBurden()
    {
        // Generate tuples where shiftDurationMinutes * splitCount < 240
        var gen = from burden in Gen.Elements(TaskBurdenLevel.Easy, TaskBurdenLevel.Normal, TaskBurdenLevel.Hard)
                  from splitCount in Gen.Choose(2, 10)
                  from shiftDurationMinutes in Gen.Choose(1, (240 / splitCount) - 1)
                  where shiftDurationMinutes >= 1 && shiftDurationMinutes * splitCount < 240
                  select (burden, splitCount, shiftDurationMinutes);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (burden, splitCount, shiftDurationMinutes) = tuple;
            var result = BurdenScalingService.ComputeEffectiveBurden(burden, splitCount, shiftDurationMinutes);
            return result == burden;
        });
    }

    // ── Property 1c: Returns max(Easy, originalBurden - (splitCount - 1)) when threshold met ──
    // **Validates: Requirements 2.1, 3.1, 3.5**

    [Property(MaxTest = 200)]
    public Property AboveThreshold_ReturnsReducedBurden()
    {
        // Generate tuples where splitCount > 1 and shiftDurationMinutes * splitCount >= 240
        var gen = from burden in Gen.Elements(TaskBurdenLevel.Easy, TaskBurdenLevel.Normal, TaskBurdenLevel.Hard)
                  from splitCount in Gen.Choose(2, 10)
                  from shiftDurationMinutes in Gen.Choose(1, 1440)
                  where shiftDurationMinutes * splitCount >= 240
                  select (burden, splitCount, shiftDurationMinutes);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (burden, splitCount, shiftDurationMinutes) = tuple;
            var result = BurdenScalingService.ComputeEffectiveBurden(burden, splitCount, shiftDurationMinutes);
            var expected = (TaskBurdenLevel)Math.Max(0, (int)burden - (splitCount - 1));
            return result == expected;
        });
    }

    // ── Property 1d: Result is never below Easy (floor invariant) ─────────────
    // **Validates: Requirements 2.1, 3.1, 3.5**

    [Property(MaxTest = 200)]
    public Property Result_NeverBelowEasy()
    {
        return Prop.ForAll(BurdenInputArbitrary(), tuple =>
        {
            var (burden, splitCount, shiftDurationMinutes) = tuple;
            var result = BurdenScalingService.ComputeEffectiveBurden(burden, splitCount, shiftDurationMinutes);
            return result >= TaskBurdenLevel.Easy;
        });
    }
}
