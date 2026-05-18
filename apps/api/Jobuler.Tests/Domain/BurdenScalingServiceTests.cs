// Feature: split-burden-scaling
// Unit tests for BurdenScalingService

using FluentAssertions;
using Jobuler.Domain.Tasks;
using Xunit;

namespace Jobuler.Tests.Domain;

public class BurdenScalingServiceTests
{
    // ── Requirement 3.2: Hard + split 2 (original duration ≥ 240) → Normal ──

    [Fact]
    public void ComputeEffectiveBurden_Hard_Split2_LongDuration_ReturnsNormal()
    {
        // Hard (2) - (2-1) = 1 = Normal
        // shiftDurationMinutes=120, splitCount=2 → originalDuration = 240
        var result = BurdenScalingService.ComputeEffectiveBurden(
            TaskBurdenLevel.Hard, splitCount: 2, shiftDurationMinutes: 120);

        result.Should().Be(TaskBurdenLevel.Normal);
    }

    // ── Requirement 3.3: Hard + split 3 (original duration ≥ 240) → Easy ────

    [Fact]
    public void ComputeEffectiveBurden_Hard_Split3_LongDuration_ReturnsEasy()
    {
        // Hard (2) - (3-1) = 0 = Easy
        // shiftDurationMinutes=80, splitCount=3 → originalDuration = 240
        var result = BurdenScalingService.ComputeEffectiveBurden(
            TaskBurdenLevel.Hard, splitCount: 3, shiftDurationMinutes: 80);

        result.Should().Be(TaskBurdenLevel.Easy);
    }

    // ── Requirement 3.4: Normal + split 2 (original duration ≥ 240) → Easy ──

    [Fact]
    public void ComputeEffectiveBurden_Normal_Split2_LongDuration_ReturnsEasy()
    {
        // Normal (1) - (2-1) = 0 = Easy
        // shiftDurationMinutes=120, splitCount=2 → originalDuration = 240
        var result = BurdenScalingService.ComputeEffectiveBurden(
            TaskBurdenLevel.Normal, splitCount: 2, shiftDurationMinutes: 120);

        result.Should().Be(TaskBurdenLevel.Easy);
    }

    // ── Requirement 3.5: Easy + split 5 (original duration ≥ 240) → Easy (floor) ──

    [Fact]
    public void ComputeEffectiveBurden_Easy_Split5_LongDuration_ReturnsEasy_Floor()
    {
        // Easy (0) - (5-1) = -4, clamped to 0 = Easy
        // shiftDurationMinutes=48, splitCount=5 → originalDuration = 240
        var result = BurdenScalingService.ComputeEffectiveBurden(
            TaskBurdenLevel.Easy, splitCount: 5, shiftDurationMinutes: 48);

        result.Should().Be(TaskBurdenLevel.Easy);
    }

    // ── Requirement 2.1/2.2: Hard + split 2 but originalDuration = 120 → Hard (threshold not met) ──

    [Fact]
    public void ComputeEffectiveBurden_Hard_Split2_ShortDuration_ReturnsOriginal()
    {
        // shiftDurationMinutes=60, splitCount=2 → originalDuration = 120 < 240
        var result = BurdenScalingService.ComputeEffectiveBurden(
            TaskBurdenLevel.Hard, splitCount: 2, shiftDurationMinutes: 60);

        result.Should().Be(TaskBurdenLevel.Hard);
    }

    // ── Requirement 3.1: SplitCount = 1 → no change regardless of burden level ──

    [Theory]
    [InlineData(TaskBurdenLevel.Easy)]
    [InlineData(TaskBurdenLevel.Normal)]
    [InlineData(TaskBurdenLevel.Hard)]
    public void ComputeEffectiveBurden_SplitCount1_ReturnsOriginalBurden(TaskBurdenLevel burden)
    {
        var result = BurdenScalingService.ComputeEffectiveBurden(
            burden, splitCount: 1, shiftDurationMinutes: 480);

        result.Should().Be(burden);
    }
}
