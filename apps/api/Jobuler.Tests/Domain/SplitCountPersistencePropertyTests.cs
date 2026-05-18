// Feature: split-burden-scaling, Property 2: Split count persistence round-trip
// Property-based tests for GroupTask SplitCount and ShiftDurationMinutes persistence

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Domain.Tasks;
using Xunit;

namespace Jobuler.Tests.Domain;

/// <summary>
/// **Validates: Requirements 1.1, 1.3**
///
/// Property 2: For any valid splitCount ∈ [1, 10] and shiftDurationMinutes ∈ [1, 1440],
/// creating or updating a GroupTask with those values and reading back the entity
/// SHALL yield the same SplitCount and ShiftDurationMinutes values.
/// </summary>
public class SplitCountPersistencePropertyTests
{
    /// <summary>
    /// Custom Arbitrary that generates splitCount in [1, 10] and shiftDurationMinutes in [1, 1440].
    /// </summary>
    public static Arbitrary<(int splitCount, int shiftDurationMinutes)> SplitCountAndDurationArb()
    {
        var gen = from splitCount in Gen.Choose(1, 10)
                  from shiftDurationMinutes in Gen.Choose(1, 1440)
                  select (splitCount, shiftDurationMinutes);
        return gen.ToArbitrary();
    }

    // ── Property 2a: Create round-trip ───────────────────────────────────────

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(SplitCountPersistencePropertyTests) })]
    public void Create_PersistsSplitCountAndShiftDuration((int splitCount, int shiftDurationMinutes) input)
    {
        // Arrange & Act
        var task = GroupTask.Create(
            spaceId: Guid.NewGuid(),
            groupId: Guid.NewGuid(),
            name: "Test Task",
            startsAt: DateTime.UtcNow,
            endsAt: DateTime.UtcNow.AddHours(12),
            shiftDurationMinutes: input.shiftDurationMinutes,
            requiredHeadcount: 1,
            burdenLevel: TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: Guid.NewGuid(),
            splitCount: input.splitCount);

        // Assert
        task.SplitCount.Should().Be(input.splitCount);
        task.ShiftDurationMinutes.Should().Be(input.shiftDurationMinutes);
    }

    // ── Property 2b: Update round-trip ───────────────────────────────────────

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(SplitCountPersistencePropertyTests) })]
    public void Update_PersistsSplitCountAndShiftDuration((int splitCount, int shiftDurationMinutes) input)
    {
        // Arrange — create with default values first
        var task = GroupTask.Create(
            spaceId: Guid.NewGuid(),
            groupId: Guid.NewGuid(),
            name: "Test Task",
            startsAt: DateTime.UtcNow,
            endsAt: DateTime.UtcNow.AddHours(12),
            shiftDurationMinutes: 240,
            requiredHeadcount: 1,
            burdenLevel: TaskBurdenLevel.Hard,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: Guid.NewGuid(),
            splitCount: 1);

        // Act — update with random values
        task.Update(
            name: "Updated Task",
            startsAt: DateTime.UtcNow,
            endsAt: DateTime.UtcNow.AddHours(12),
            shiftDurationMinutes: input.shiftDurationMinutes,
            requiredHeadcount: 2,
            burdenLevel: TaskBurdenLevel.Easy,
            allowsDoubleShift: true,
            allowsOverlap: true,
            updatedByUserId: Guid.NewGuid(),
            splitCount: input.splitCount);

        // Assert
        task.SplitCount.Should().Be(input.splitCount);
        task.ShiftDurationMinutes.Should().Be(input.shiftDurationMinutes);
    }
}
