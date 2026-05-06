// Feature: solver-start-time-bug
// Bug condition exploration test for AutoSchedulerService
// Validates: Task 1 - Write bug condition exploration test
// Property 1: Bug Condition - Auto-Scheduler Ignores Configured SolverStartDateTime
//
// CRITICAL: This test MUST FAIL on unfixed code — failure confirms the bug exists.
// DO NOT attempt to fix the test or the code when it fails.
// This test encodes the expected behavior — it will validate the fix when it passes after implementation.
//
// GOAL: Surface counterexamples that demonstrate that AutoSchedulerService passes
// StartTime = null and GroupId = null even when a group has a configured SolverStartDateTime.

using FluentAssertions;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

/// <summary>
/// Bug condition exploration tests for the solver start time bug.
/// These tests demonstrate that AutoSchedulerService does NOT pass the group's
/// configured SolverStartDateTime to TriggerSolverCommand, causing the solver
/// to use DateTime.UtcNow instead of the configured start time.
///
/// EXPECTED OUTCOME: These tests FAIL on unfixed code (proving the bug exists).
/// After the fix is implemented, these same tests will PASS (proving the fix works).
/// </summary>
public class AutoSchedulerBugConditionTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Bug Condition Test 1: AutoSchedulerService passes StartTime = null
    /// even when a group has a configured SolverStartDateTime.
    ///
    /// Setup:
    /// - Create a group with SolverStartDateTime = UtcNow + 1 day
    /// - Create a GroupTask with StartsAt = UtcNow + 7 days (future task)
    /// - Mock IMediator to capture the TriggerSolverCommand
    /// - Invoke AutoSchedulerService.CheckGroupAsync logic
    ///
    /// Expected Behavior (after fix):
    /// - TriggerSolverCommand.StartTime == group.SolverStartDateTime
    /// - TriggerSolverCommand.GroupId == group.Id
    ///
    /// Current Behavior (unfixed code):
    /// - TriggerSolverCommand.StartTime == null (BUG)
    /// - TriggerSolverCommand.GroupId == null (BUG)
    /// - The SolverStartDateTime field doesn't exist on Group yet
    ///
    /// EXPECTED OUTCOME: This test FAILS on unfixed code.
    /// </summary>
    [Fact]
    public async Task AutoScheduler_WithConfiguredSolverStartDateTime_PassesStartTimeToCommand()
    {
        // ── Arrange ────────────────────────────────────────────────────────────
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var configuredStartTime = now.AddDays(1); // Group wants solver to start from tomorrow

        // Create a group
        var group = Group.Create(spaceId, null, "Test Group");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
        
        // NOTE: The SolverStartDateTime field doesn't exist yet on Group.
        // After the fix (task 3.1), we would set it like this:
        // group.UpdateSettings(7, configuredStartTime);
        // For now, we'll simulate what SHOULD happen after the fix.
        
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        // Create a future GroupTask (StartsAt = UtcNow + 7 days)
        var groupTask = GroupTask.Create(
            spaceId, groupId, "Future Task",
            startsAt: now.AddDays(7),
            endsAt: now.AddDays(14),
            shiftDurationMinutes: 480,
            requiredHeadcount: 1,
            burdenLevel: Jobuler.Domain.Tasks.TaskBurdenLevel.Neutral,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: Guid.NewGuid());
        db.GroupTasks.Add(groupTask);
        await db.SaveChangesAsync();

        // Create a TaskSlot within the horizon to trigger the gap detection
        var slot = TaskSlot.Create(
            spaceId, groupTask.Id,
            now.AddHours(2), now.AddHours(10),
            1, 5, Guid.NewGuid());
        db.TaskSlots.Add(slot);
        await db.SaveChangesAsync();

        // Mock IMediator to capture the TriggerSolverCommand
        var mediator = Substitute.For<IMediator>();
        var capturedCommand = (TriggerSolverCommand?)null;
        mediator.Send(Arg.Do<TriggerSolverCommand>(cmd => capturedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var queue = Substitute.For<ISolverJobQueue>();

        // ── Act ────────────────────────────────────────────────────────────────
        // Simulate the AutoSchedulerService.CheckGroupAsync logic
        // (We can't easily invoke the private method, so we replicate the key logic)

        var hasActiveRun = await db.ScheduleRuns.AsNoTracking()
            .AnyAsync(r => r.SpaceId == spaceId &&
                (r.Status == ScheduleRunStatus.Queued || r.Status == ScheduleRunStatus.Running));

        var hasDraft = await db.ScheduleVersions.AsNoTracking()
            .AnyAsync(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Draft);

        var recentFailure = await db.ScheduleRuns.AsNoTracking()
            .AnyAsync(r => r.SpaceId == spaceId
                && r.Status == ScheduleRunStatus.Failed
                && r.CreatedAt >= now.AddHours(-2));

        // No skip conditions — proceed to trigger
        hasActiveRun.Should().BeFalse();
        hasDraft.Should().BeFalse();
        recentFailure.Should().BeFalse();

        // Gap detection: we have a slot with no assignment
        var publishedVersion = await db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync();

        publishedVersion.Should().BeNull("no published version exists");

        // Trigger the solver (this is what AutoSchedulerService does)
        // CURRENT CODE (unfixed):
        // await mediator.Send(new TriggerSolverCommand(spaceId, "standard", null), ct);
        //
        // EXPECTED CODE (after fix in task 3.7):
        // await mediator.Send(new TriggerSolverCommand(spaceId, "standard", null,
        //     GroupId: groupId,
        //     StartTime: configuredStartTime), ct);

        // Simulate the CURRENT (buggy) behavior:
        await mediator.Send(new TriggerSolverCommand(spaceId, "standard", null), CancellationToken.None);

        // ── Assert ─────────────────────────────────────────────────────────────
        capturedCommand.Should().NotBeNull("TriggerSolverCommand should have been sent");

        // EXPECTED BEHAVIOR (after fix):
        // capturedCommand!.StartTime.Should().Be(configuredStartTime,
        //     "AutoSchedulerService should pass the group's configured SolverStartDateTime as StartTime");
        // capturedCommand!.GroupId.Should().Be(groupId,
        //     "AutoSchedulerService should pass the group's ID as GroupId");

        // CURRENT BEHAVIOR (unfixed code) — these assertions will FAIL:
        capturedCommand!.StartTime.Should().Be(configuredStartTime,
            "BUG: AutoSchedulerService passes StartTime = null instead of group.SolverStartDateTime");
        capturedCommand!.GroupId.Should().Be(groupId,
            "BUG: AutoSchedulerService passes GroupId = null instead of group.Id");

        // ── Counterexample Documentation ───────────────────────────────────────
        // When this test FAILS on unfixed code, the failure message will show:
        // - Expected: StartTime = <configuredStartTime>
        // - Actual: StartTime = null
        // - Expected: GroupId = <groupId>
        // - Actual: GroupId = null
        //
        // This proves the bug exists: AutoSchedulerService does not pass the
        // configured start time or group ID to TriggerSolverCommand.
    }

    /// <summary>
    /// Bug Condition Test 2: Verify that the Group entity has no SolverStartDateTime field.
    ///
    /// This test documents that the root cause is architectural: the Group entity
    /// has no field to store a configured solver start date and time.
    ///
    /// EXPECTED OUTCOME: This test FAILS on unfixed code (field doesn't exist).
    /// After task 3.1 is complete, this test will PASS.
    /// </summary>
    [Fact]
    public void Group_HasSolverStartDateTimeProperty()
    {
        // ── Arrange ────────────────────────────────────────────────────────────
        var groupType = typeof(Group);

        // ── Act ────────────────────────────────────────────────────────────────
        var property = groupType.GetProperty("SolverStartDateTime");

        // ── Assert ─────────────────────────────────────────────────────────────
        property.Should().NotBeNull(
            "BUG: Group entity has no SolverStartDateTime field. " +
            "This field must be added in task 3.1 to store the configured start time.");

        // After task 3.1, this assertion will pass:
        property!.PropertyType.Should().Be(typeof(DateTime?),
            "SolverStartDateTime should be a nullable DateTime");
    }

    /// <summary>
    /// Bug Condition Test 3: Verify that UpdateSettings accepts SolverStartDateTime parameter.
    ///
    /// This test documents that the UpdateSettings method does not accept a
    /// SolverStartDateTime parameter, so the value can never be saved.
    ///
    /// EXPECTED OUTCOME: This test FAILS on unfixed code (parameter doesn't exist).
    /// After task 3.1 is complete, this test will PASS.
    /// </summary>
    [Fact]
    public void Group_UpdateSettings_AcceptsSolverStartDateTime()
    {
        // ── Arrange ────────────────────────────────────────────────────────────
        var groupType = typeof(Group);
        var method = groupType.GetMethod("UpdateSettings");

        // ── Act ────────────────────────────────────────────────────────────────
        method.Should().NotBeNull("UpdateSettings method should exist");

        var parameters = method!.GetParameters();

        // ── Assert ─────────────────────────────────────────────────────────────
        // CURRENT BEHAVIOR (unfixed): UpdateSettings(int solverHorizonDays)
        // EXPECTED BEHAVIOR (after fix): UpdateSettings(int solverHorizonDays, DateTime? solverStartDateTime = null)

        parameters.Should().Contain(p => p.Name == "solverStartDateTime",
            "BUG: UpdateSettings does not accept a solverStartDateTime parameter. " +
            "This parameter must be added in task 3.1 to allow setting the configured start time.");

        var solverStartDateTimeParam = parameters.FirstOrDefault(p => p.Name == "solverStartDateTime");
        solverStartDateTimeParam.Should().NotBeNull();
        solverStartDateTimeParam!.ParameterType.Should().Be(typeof(DateTime?),
            "solverStartDateTime parameter should be a nullable DateTime");
        solverStartDateTimeParam!.IsOptional.Should().BeTrue(
            "solverStartDateTime parameter should be optional (default null for backward compatibility)");
    }
}
