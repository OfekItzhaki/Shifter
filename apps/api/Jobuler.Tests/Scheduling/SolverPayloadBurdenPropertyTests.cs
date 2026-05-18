// Feature: split-burden-scaling
// Property 3: Solver payload preserves original burden
// **Validates: Requirements 4.1**

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

/// <summary>
/// Custom Arbitraries for the solver payload burden property test.
/// FsCheck discovers these via the static Arbitrary-returning properties.
/// </summary>
public static class SolverBurdenArbitraries
{
    public static Arbitrary<TaskBurdenLevel> TaskBurdenLevel() =>
        Gen.Elements(
            Jobuler.Domain.Tasks.TaskBurdenLevel.Easy,
            Jobuler.Domain.Tasks.TaskBurdenLevel.Normal,
            Jobuler.Domain.Tasks.TaskBurdenLevel.Hard)
        .ToArbitrary();

    public static Arbitrary<BurdenSplitInput> BurdenSplitInput()
    {
        var gen = from burden in Gen.Elements(
                      Jobuler.Domain.Tasks.TaskBurdenLevel.Easy,
                      Jobuler.Domain.Tasks.TaskBurdenLevel.Normal,
                      Jobuler.Domain.Tasks.TaskBurdenLevel.Hard)
                  from splitCount in Gen.Choose(1, 10)
                  from shiftDurationMinutes in Gen.Choose(30, 480)
                  select new BurdenSplitInput(burden, splitCount, shiftDurationMinutes);

        return Arb.From(gen);
    }
}

/// <summary>
/// Input record for the FsCheck property test.
/// </summary>
public record BurdenSplitInput(TaskBurdenLevel Burden, int SplitCount, int ShiftDurationMinutes)
{
    public override string ToString() =>
        $"Burden={Burden}, SplitCount={SplitCount}, ShiftDurationMinutes={ShiftDurationMinutes}";
}

/// <summary>
/// Property-based test verifying that the SolverPayloadNormalizer always sends
/// the ORIGINAL burden level in TaskSlotDto entries — never the effective
/// (split-adjusted) burden level.
/// </summary>
public class SolverPayloadBurdenPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(AppDbContext db, Guid spaceId, Guid groupId)> SetupSpaceAndGroupAsync()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        // Create space
        var space = Space.Create("Test Space", Guid.NewGuid());
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(space, spaceId);
        db.Spaces.Add(space);

        // Create group
        var group = Group.Create(spaceId, null, "Test Group", null, null);
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
        db.Groups.Add(group);

        await db.SaveChangesAsync();
        return (db, spaceId, groupId);
    }

    private static SolverPayloadNormalizer CreateNormalizer(AppDbContext db)
    {
        var logger = Substitute.For<ILogger<SolverPayloadNormalizer>>();
        var cumulativeTracker = Substitute.For<ICumulativeTracker>();
        cumulativeTracker.GetForSolverPayloadAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<CumulativeTrackingDto>()));
        return new SolverPayloadNormalizer(db, logger, cumulativeTracker);
    }

    // ── Property 3: Solver payload preserves original burden (FsCheck) ───────
    // Feature: split-burden-scaling, Property 3: Solver payload preserves original burden

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(SolverBurdenArbitraries) })]
    public bool SolverPayload_AlwaysUsesOriginalBurdenLevel(BurdenSplitInput input)
    {
        // Arrange
        var (db, spaceId, groupId) = SetupSpaceAndGroupAsync().GetAwaiter().GetResult();

        var now = DateTime.UtcNow;
        var startsAt = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var endsAt = startsAt.AddDays(7);

        var task = GroupTask.Create(
            spaceId, groupId,
            "Test Task",
            startsAt, endsAt,
            input.ShiftDurationMinutes,
            requiredHeadcount: 1,
            burdenLevel: input.Burden,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: Guid.NewGuid(),
            splitCount: input.SplitCount);

        db.GroupTasks.Add(task);
        db.SaveChangesAsync().GetAwaiter().GetResult();

        var normalizer = CreateNormalizer(db);

        // Act
        var payload = normalizer.BuildAsync(
            spaceId,
            runId: Guid.NewGuid(),
            triggerMode: "standard",
            baselineVersionId: null,
            groupId: groupId,
            startTime: startsAt,
            ct: CancellationToken.None).GetAwaiter().GetResult();

        // Assert: all TaskSlotDto entries for this task must use the ORIGINAL burden level
        var expectedBurden = input.Burden.ToString().ToLower();
        var taskSlots = payload.TaskSlots
            .Where(s => s.TaskTypeId == task.Id.ToString())
            .ToList();

        // If no slots generated (shift too long for horizon), property holds vacuously
        if (taskSlots.Count == 0)
            return true;

        return taskSlots.All(s => s.BurdenLevel == expectedBurden);
    }

    // ── Deterministic examples verifying effective differs but solver uses original ──

    [Theory]
    [InlineData(TaskBurdenLevel.Hard, 2, 240)]   // effective = Normal, solver should still say "hard"
    [InlineData(TaskBurdenLevel.Hard, 3, 120)]   // effective = Easy, solver should still say "hard"
    [InlineData(TaskBurdenLevel.Normal, 2, 180)] // effective = Easy, solver should still say "normal"
    [InlineData(TaskBurdenLevel.Easy, 5, 60)]    // effective = Easy, solver should still say "easy"
    [InlineData(TaskBurdenLevel.Hard, 1, 480)]   // no split, solver says "hard"
    public async Task SolverPayload_UsesOriginalBurden_NotEffective(
        TaskBurdenLevel burden, int splitCount, int shiftDurationMinutes)
    {
        // Arrange
        var (db, spaceId, groupId) = await SetupSpaceAndGroupAsync();

        var now = DateTime.UtcNow;
        var startsAt = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var endsAt = startsAt.AddDays(7);

        var task = GroupTask.Create(
            spaceId, groupId,
            "Burden Test Task",
            startsAt, endsAt,
            shiftDurationMinutes,
            requiredHeadcount: 1,
            burdenLevel: burden,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: Guid.NewGuid(),
            splitCount: splitCount);

        db.GroupTasks.Add(task);
        await db.SaveChangesAsync();

        var normalizer = CreateNormalizer(db);

        // Act
        var payload = await normalizer.BuildAsync(
            spaceId,
            runId: Guid.NewGuid(),
            triggerMode: "standard",
            baselineVersionId: null,
            groupId: groupId,
            startTime: startsAt,
            ct: CancellationToken.None);

        // Assert
        var expectedOriginalBurden = burden.ToString().ToLower();
        var effectiveBurden = BurdenScalingService.ComputeEffectiveBurden(burden, splitCount, shiftDurationMinutes)
            .ToString().ToLower();

        var taskSlots = payload.TaskSlots
            .Where(s => s.TaskTypeId == task.Id.ToString())
            .ToList();

        taskSlots.Should().NotBeEmpty("at least one shift slot should be generated");

        foreach (var slot in taskSlots)
        {
            slot.BurdenLevel.Should().Be(expectedOriginalBurden,
                $"solver payload must use original burden '{expectedOriginalBurden}', not effective '{effectiveBurden}'");
        }
    }
}
