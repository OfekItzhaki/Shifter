// Feature: schedule-regeneration
// Property 7: Regeneration does not block standard runs
// For any group with an in-progress regeneration run, triggering a standard or emergency
// solver run SHALL succeed without conflict.
// **Validates: Requirements 9.4**

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Identity;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

/// <summary>
/// FsCheck arbitraries for generating random standard or emergency solver run requests
/// used in the regeneration-not-blocking-standard-runs property test.
/// </summary>
public static class StandardRunRequestArbitraries
{
    public static Arbitrary<StandardRunRequest> StandardRunRequest()
    {
        var gen = from triggerMode in Gen.Elements("standard", "emergency")
                  from regenStatus in Gen.Elements(ScheduleRunStatus.Queued, ScheduleRunStatus.Running)
                  select new StandardRunRequest(triggerMode, regenStatus);

        return Arb.From(gen);
    }
}

/// <summary>
/// Input record representing a random standard or emergency run request
/// alongside a regeneration run status (Queued or Running).
/// </summary>
public record StandardRunRequest(
    string TriggerMode,
    ScheduleRunStatus RegenerationRunStatus)
{
    public override string ToString() =>
        $"TriggerMode={TriggerMode}, RegenerationRunStatus={RegenerationRunStatus}";
}

/// <summary>
/// Property-based test verifying that for any group with an in-progress regeneration run
/// (status Queued or Running), triggering a standard or emergency solver run succeeds
/// without throwing a ConflictException. The concurrency guard only blocks concurrent
/// regeneration runs, not standard/emergency runs.
/// </summary>
public class RegenerationNotBlockingStandardRunsPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ISolverJobQueue CreateMockQueue()
    {
        var queue = Substitute.For<ISolverJobQueue>();
        queue.EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return queue;
    }

    /// <summary>
    /// Seeds a space with a user, group, active subscription, published version,
    /// an active future task (required by TriggerSolverCommand's stale-task guard),
    /// and an in-progress regeneration run with the specified status.
    /// Returns (spaceId, groupId, userId, publishedVersionId, regenRunId).
    /// </summary>
    private static async Task<(Guid SpaceId, Guid GroupId, Guid UserId, Guid PublishedVersionId, Guid RegenRunId)>
        SeedScenarioAsync(AppDbContext db, ScheduleRunStatus regenStatus)
    {
        var userId = Guid.NewGuid();
        var user = User.Create("admin@test.com", "Admin", BCrypt.Net.BCrypt.HashPassword("Pass1234!", workFactor: 4));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var spaceId = Guid.NewGuid();
        var space = Space.Create("Test Space", user.Id);
        db.Entry(space).Property("Id").CurrentValue = spaceId;
        db.Spaces.Add(space);
        await db.SaveChangesAsync();

        var groupId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        db.Entry(group).Property("Id").CurrentValue = groupId;
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        // Active subscription (so no billing guard blocks)
        var subscription = GroupSubscription.CreateTrial(spaceId, groupId, trialDays: 30);
        db.GroupSubscriptions.Add(subscription);

        // Published version (baseline for the regeneration run)
        var publishedVersion = ScheduleVersion.CreateDraft(spaceId, 1, null, null, user.Id);
        publishedVersion.Publish(user.Id);
        db.ScheduleVersions.Add(publishedVersion);
        await db.SaveChangesAsync();

        // Active future task (required by TriggerSolverCommand's stale-task guard)
        var futureTask = GroupTask.Create(
            spaceId, groupId, "Future Task",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            shiftDurationMinutes: 240, requiredHeadcount: 1,
            burdenLevel: TaskBurdenLevel.Normal,
            allowsDoubleShift: false, allowsOverlap: false,
            createdByUserId: user.Id);
        db.GroupTasks.Add(futureTask);
        await db.SaveChangesAsync();

        // In-progress regeneration run
        var regenRun = ScheduleRun.Create(
            spaceId, ScheduleRunTrigger.Regeneration,
            publishedVersion.Id, user.Id, groupId);
        db.ScheduleRuns.Add(regenRun);
        await db.SaveChangesAsync();

        if (regenStatus == ScheduleRunStatus.Running)
        {
            regenRun.MarkRunning("regen-hash");
            await db.SaveChangesAsync();
        }
        // If Queued, it's already in Queued status by default

        return (spaceId, groupId, user.Id, publishedVersion.Id, regenRun.Id);
    }

    // ── Property 7: Regeneration does not block standard runs ─────────────────
    // Feature: schedule-regeneration, Property 7
    // **Validates: Requirements 9.4**

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(StandardRunRequestArbitraries) })]
    public bool StandardOrEmergencyRun_SucceedsWhileRegenerationInProgress(StandardRunRequest request)
    {
        return StandardRunNotBlockedTestAsync(request).GetAwaiter().GetResult();
    }

    private async Task<bool> StandardRunNotBlockedTestAsync(StandardRunRequest request)
    {
        // Arrange — seed a group with a published version and an in-progress regeneration run
        var db = CreateDb();
        var (spaceId, groupId, userId, publishedVersionId, regenRunId) =
            await SeedScenarioAsync(db, request.RegenerationRunStatus);

        var queue = CreateMockQueue();
        var handler = new TriggerSolverCommandHandler(db, queue);

        var command = new TriggerSolverCommand(
            spaceId,
            request.TriggerMode,
            userId,
            groupId);

        // Act — trigger a standard or emergency run (should NOT throw ConflictException)
        Guid newRunId;
        try
        {
            newRunId = await handler.Handle(command, CancellationToken.None);
        }
        catch (Exception)
        {
            // Any exception means the standard/emergency run was blocked — property violated
            return false;
        }

        // Assert 1: A new run was created successfully
        var newRun = await db.ScheduleRuns.FindAsync(newRunId);
        if (newRun is null) return false;

        // Assert 2: The new run has the correct trigger type (Standard or Emergency)
        var expectedTrigger = request.TriggerMode == "emergency"
            ? ScheduleRunTrigger.Emergency
            : ScheduleRunTrigger.Standard;
        if (newRun.TriggerType != expectedTrigger) return false;

        // Assert 3: The new run is in Queued status
        if (newRun.Status != ScheduleRunStatus.Queued) return false;

        // Assert 4: The new run belongs to the correct space
        if (newRun.SpaceId != spaceId) return false;

        // Assert 5: The regeneration run is still in its original status (not affected)
        var regenRun = await db.ScheduleRuns.FindAsync(regenRunId);
        if (regenRun is null) return false;
        if (regenRun.Status != request.RegenerationRunStatus) return false;
        if (regenRun.TriggerType != ScheduleRunTrigger.Regeneration) return false;

        // Assert 6: The queue was called (job was dispatched)
        await queue.Received(1).EnqueueAsync(
            Arg.Is<SolverJobMessage>(m => m.RunId == newRunId && m.TriggerMode == request.TriggerMode),
            Arg.Any<CancellationToken>());

        return true;
    }

    // ── Deterministic edge case examples ─────────────────────────────────────

    [Fact]
    public async Task StandardRun_SucceedsWithQueuedRegenerationRun()
    {
        // Arrange
        var db = CreateDb();
        var (spaceId, groupId, userId, publishedVersionId, regenRunId) =
            await SeedScenarioAsync(db, ScheduleRunStatus.Queued);

        var queue = CreateMockQueue();
        var handler = new TriggerSolverCommandHandler(db, queue);

        var command = new TriggerSolverCommand(spaceId, "standard", userId, groupId);

        // Act
        var newRunId = await handler.Handle(command, CancellationToken.None);

        // Assert
        var newRun = await db.ScheduleRuns.FindAsync(newRunId);
        newRun.Should().NotBeNull();
        newRun!.TriggerType.Should().Be(ScheduleRunTrigger.Standard);
        newRun.Status.Should().Be(ScheduleRunStatus.Queued);

        // Regeneration run is unaffected
        var regenRun = await db.ScheduleRuns.FindAsync(regenRunId);
        regenRun!.Status.Should().Be(ScheduleRunStatus.Queued);
        regenRun.TriggerType.Should().Be(ScheduleRunTrigger.Regeneration);
    }

    [Fact]
    public async Task EmergencyRun_SucceedsWithRunningRegenerationRun()
    {
        // Arrange
        var db = CreateDb();
        var (spaceId, groupId, userId, publishedVersionId, regenRunId) =
            await SeedScenarioAsync(db, ScheduleRunStatus.Running);

        var queue = CreateMockQueue();
        var handler = new TriggerSolverCommandHandler(db, queue);

        var command = new TriggerSolverCommand(spaceId, "emergency", userId, groupId);

        // Act
        var newRunId = await handler.Handle(command, CancellationToken.None);

        // Assert
        var newRun = await db.ScheduleRuns.FindAsync(newRunId);
        newRun.Should().NotBeNull();
        newRun!.TriggerType.Should().Be(ScheduleRunTrigger.Emergency);
        newRun.Status.Should().Be(ScheduleRunStatus.Queued);

        // Regeneration run is still running
        var regenRun = await db.ScheduleRuns.FindAsync(regenRunId);
        regenRun!.Status.Should().Be(ScheduleRunStatus.Running);
        regenRun.TriggerType.Should().Be(ScheduleRunTrigger.Regeneration);
    }

    [Fact]
    public async Task MultipleStandardRuns_SucceedWithRegenerationInProgress()
    {
        // Arrange — verify multiple standard runs can be created even with regeneration active
        var db = CreateDb();
        var (spaceId, groupId, userId, publishedVersionId, regenRunId) =
            await SeedScenarioAsync(db, ScheduleRunStatus.Running);

        var queue = CreateMockQueue();
        var handler = new TriggerSolverCommandHandler(db, queue);

        // Act — trigger two standard runs in sequence
        var runId1 = await handler.Handle(
            new TriggerSolverCommand(spaceId, "standard", userId, groupId),
            CancellationToken.None);
        var runId2 = await handler.Handle(
            new TriggerSolverCommand(spaceId, "emergency", userId, groupId),
            CancellationToken.None);

        // Assert — both runs created successfully
        var run1 = await db.ScheduleRuns.FindAsync(runId1);
        var run2 = await db.ScheduleRuns.FindAsync(runId2);
        run1.Should().NotBeNull();
        run2.Should().NotBeNull();
        run1!.TriggerType.Should().Be(ScheduleRunTrigger.Standard);
        run2!.TriggerType.Should().Be(ScheduleRunTrigger.Emergency);

        // Regeneration run is still running
        var regenRun = await db.ScheduleRuns.FindAsync(regenRunId);
        regenRun!.Status.Should().Be(ScheduleRunStatus.Running);
    }
}
