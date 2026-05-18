// Feature: split-burden-scaling
// Unit tests for snapshot and fairness integration with BurdenScalingService.
// Validates: Requirements 5.1, 5.2, 5.3, 6.1, 6.2

using FluentAssertions;
using Jobuler.Domain.Tasks;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class SnapshotAndFairnessIntegrationTests
{
    // ── Requirement 5.1, 5.2: Split task snapshot stores effective burden ─────

    /// <summary>
    /// A Hard task split into 2 with original duration 360 min (180 × 2 = 360 ≥ 240)
    /// should produce effective burden "normal" for the snapshot.
    /// </summary>
    [Fact]
    public void SplitTask_Hard_Split2_360MinOriginal_SnapshotStoresNormal()
    {
        // Arrange: Hard (2), splitCount=2, shiftDurationMinutes=180
        // Original duration = 180 × 2 = 360 ≥ 240 → reduction applies
        // Effective = max(Easy, Hard - (2-1)) = max(0, 2-1) = 1 = Normal
        var effectiveBurden = BurdenScalingService.ComputeEffectiveBurden(
            TaskBurdenLevel.Hard, splitCount: 2, shiftDurationMinutes: 180);

        // This is what AssignmentSnapshotService stores in the snapshot
        var snapshotBurdenLevel = effectiveBurden.ToString().ToLower();

        effectiveBurden.Should().Be(TaskBurdenLevel.Normal);
        snapshotBurdenLevel.Should().Be("normal");
    }

    // ── Requirement 5.3: Non-split task snapshot stores original burden ───────

    /// <summary>
    /// A Hard task with split 1 (no split) should store "hard" in the snapshot
    /// regardless of duration.
    /// </summary>
    [Fact]
    public void NonSplitTask_Hard_Split1_SnapshotStoresHard()
    {
        // Arrange: Hard (2), splitCount=1 → no reduction applied
        var effectiveBurden = BurdenScalingService.ComputeEffectiveBurden(
            TaskBurdenLevel.Hard, splitCount: 1, shiftDurationMinutes: 480);

        var snapshotBurdenLevel = effectiveBurden.ToString().ToLower();

        effectiveBurden.Should().Be(TaskBurdenLevel.Hard);
        snapshotBurdenLevel.Should().Be("hard");
    }

    // ── Requirement 5.2 (threshold guard): Short task keeps original burden ──

    /// <summary>
    /// A Hard task split into 2 but with original duration 60 min (30 × 2 = 60 < 240)
    /// should store "hard" in the snapshot because the threshold is not met.
    /// </summary>
    [Fact]
    public void ShortTask_Hard_Split2_60MinOriginal_SnapshotStoresHard()
    {
        // Arrange: Hard (2), splitCount=2, shiftDurationMinutes=30
        // Original duration = 30 × 2 = 60 < 240 → threshold NOT met, no reduction
        var effectiveBurden = BurdenScalingService.ComputeEffectiveBurden(
            TaskBurdenLevel.Hard, splitCount: 2, shiftDurationMinutes: 30);

        var snapshotBurdenLevel = effectiveBurden.ToString().ToLower();

        effectiveBurden.Should().Be(TaskBurdenLevel.Hard);
        snapshotBurdenLevel.Should().Be("hard");
    }

    // ── Requirement 6.1, 6.2: Fairness counter does NOT count split-reduced task as hard ──

    /// <summary>
    /// When a snapshot stores "normal" (because a Hard task was split-reduced),
    /// the fairness counter parsing logic should NOT count it as hard.
    /// This mirrors the logic in UpdateFairnessCountersCommand which parses
    /// the BurdenLevel string from DailySnapshot and checks == TaskBurdenLevel.Hard.
    /// </summary>
    [Fact]
    public void FairnessCounter_SplitReducedTask_DoesNotCountAsHard()
    {
        // Arrange: simulate what the snapshot stores for a split-reduced task
        // Hard task, split 2, 180 min per shift → original 360 min → effective = Normal
        var effectiveBurden = BurdenScalingService.ComputeEffectiveBurden(
            TaskBurdenLevel.Hard, splitCount: 2, shiftDurationMinutes: 180);
        var snapshotBurdenLevelRaw = effectiveBurden.ToString().ToLower(); // "normal"

        // Act: simulate the fairness counter parsing logic from UpdateFairnessCountersCommand
        // This is exactly how the handler parses the burden level from the snapshot
        var parsedBurden = Enum.TryParse<TaskBurdenLevel>(snapshotBurdenLevelRaw, true, out var parsed)
            ? parsed
            : TaskBurdenLevel.Normal;

        // Assert: the parsed burden should be Normal, NOT Hard
        parsedBurden.Should().Be(TaskBurdenLevel.Normal);
        parsedBurden.Should().NotBe(TaskBurdenLevel.Hard,
            "a split-reduced task with effective burden 'normal' must NOT count toward HardTasks7d");

        // Verify the counting logic: only Hard counts as hard
        var countsAsHard = parsedBurden == TaskBurdenLevel.Hard;
        countsAsHard.Should().BeFalse(
            "the fairness counter should not count a split-reduced 'normal' burden as hard");
    }
}
