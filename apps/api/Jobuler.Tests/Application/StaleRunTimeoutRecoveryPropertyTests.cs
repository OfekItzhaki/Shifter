// Feature: schedule-regeneration
// Property 6: Stale run timeout recovery
// For any regeneration run in "Running" status longer than (solver_timeout + grace_period),
// the system SHALL treat it as failed and allow new regeneration requests.
// **Validates: Requirements 9.3**

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Identity;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class StaleRunTimeoutRecoveryPropertyTests
{
    // ── Constants matching handler defaults ────────────────────────────────────
    private const int SolverTimeoutSeconds = 30;
    private const int StaleGracePeriodMinutes = 5;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration BuildConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Solver:TimeoutSeconds"] = SolverTimeoutSeconds.ToString(),
                ["Solver:StaleGracePeriodMinutes"] = StaleGracePeriodMinutes.ToString()
            })
            .Build();
        return config;
    }

    private static ISolverJobQueue CreateMockQueue()
    {
        var queue = Substitute.For<ISolverJobQueue>();
        queue.EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return queue;
    }

    private static ITimezoneResolver CreateMockTimezoneResolver()
    {
        var resolver = Substitute.For<ITimezoneResolver>();
        resolver.Resolve(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new TimezoneResolution("Asia/Jerusalem", 120));
        return resolver;
    }

    /// <summary>
    /// Seeds a space with an owner user, an active subscription, and a published version.
    /// Returns (spaceId, groupId, userId, publishedVersionId).
    /// </summary>
    private static async Task<(Guid SpaceId, Guid GroupId, Guid UserId, Guid PublishedVersionId)> SeedScenario(
        AppDbContext db)
    {
        var userId = Guid.NewGuid();
        var user = User.Create("admin@test.com", "Admin", BCrypt.Net.BCrypt.HashPassword("Pass1234!", workFactor: 4));
        // Override the Id via EF entry
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var spaceId = Guid.NewGuid();
        var space = Space.Create("Test Space", user.Id);
        db.Entry(space).Property("Id").CurrentValue = spaceId;
        db.Spaces.Add(space);
        await db.SaveChangesAsync();

        var groupId = Guid.NewGuid();

        // Active subscription
        var subscription = GroupSubscription.CreateTrial(spaceId, groupId, trialDays: 30);
        db.GroupSubscriptions.Add(subscription);

        // Published version
        var version = ScheduleVersion.CreateDraft(spaceId, 1, null, null, user.Id);
        version.Publish(user.Id);
        db.ScheduleVersions.Add(version);
        await db.SaveChangesAsync();

        return (spaceId, groupId, user.Id, version.Id);
    }

    /// <summary>
    /// Seeds a stale regeneration run that has been running longer than the timeout + grace period.
    /// </summary>
    private static async Task<Guid> SeedStaleRun(
        AppDbContext db, Guid spaceId, Guid groupId, Guid publishedVersionId, int extraSecondsOverThreshold)
    {
        var run = ScheduleRun.Create(spaceId, ScheduleRunTrigger.Regeneration, publishedVersionId, null, groupId);
        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync();

        // Mark it as running with a StartedAt that is stale
        var staleStartedAt = DateTime.UtcNow
            .AddSeconds(-(SolverTimeoutSeconds + 1))
            .AddMinutes(-StaleGracePeriodMinutes)
            .AddSeconds(-extraSecondsOverThreshold);

        run.MarkRunning("test-hash");
        // Override StartedAt via EF entry to simulate a stale run
        db.Entry(run).Property(nameof(ScheduleRun.StartedAt)).CurrentValue = staleStartedAt;
        await db.SaveChangesAsync();

        return run.Id;
    }

    // ── FsCheck Generators ────────────────────────────────────────────────────

    /// <summary>
    /// Generates a positive integer representing extra seconds beyond the stale threshold.
    /// Range: 1 to 3600 (1 second to 1 hour beyond threshold).
    /// </summary>
    private static Arbitrary<int> ExtraSecondsArbitrary() =>
        Arb.From(Gen.Choose(1, 3600));

    // ── Property 6: Stale run timeout recovery ────────────────────────────────
    // For any regeneration run in "Running" status longer than (solver_timeout + grace_period),
    // the system SHALL treat it as failed and allow new regeneration requests.
    // **Validates: Requirements 9.3**

    [Property(MaxTest = 100)]
    public Property StaleRunIsMarkedFailedAndNewRegenerationSucceeds()
    {
        return Prop.ForAll(ExtraSecondsArbitrary(), extraSeconds =>
        {
            // Run the async test synchronously for FsCheck
            return StaleRunRecoveryTestAsync(extraSeconds).GetAwaiter().GetResult();
        });
    }

    private async Task<bool> StaleRunRecoveryTestAsync(int extraSecondsOverThreshold)
    {
        // Arrange
        var db = CreateDb();
        var (spaceId, groupId, userId, publishedVersionId) = await SeedScenario(db);
        var staleRunId = await SeedStaleRun(db, spaceId, groupId, publishedVersionId, extraSecondsOverThreshold);

        var handler = new TriggerRegenerationCommandHandler(
            db,
            CreateMockQueue(),
            CreateMockTimezoneResolver(),
            BuildConfiguration());

        var command = new TriggerRegenerationCommand(spaceId, groupId, userId);

        // Act — trigger a new regeneration (should succeed because the stale run gets marked failed)
        var newRunId = await handler.Handle(command, CancellationToken.None);

        // Assert 1: The stale run is now marked as Failed
        var staleRun = await db.ScheduleRuns.FindAsync(staleRunId);
        if (staleRun is null) return false;
        if (staleRun.Status != ScheduleRunStatus.Failed) return false;
        if (string.IsNullOrEmpty(staleRun.ErrorSummary)) return false;

        // Assert 2: A new run was created successfully
        var newRun = await db.ScheduleRuns.FindAsync(newRunId);
        if (newRun is null) return false;
        if (newRun.Status != ScheduleRunStatus.Queued) return false;
        if (newRun.TriggerType != ScheduleRunTrigger.Regeneration) return false;
        if (newRun.GroupId != groupId) return false;
        if (newRun.SpaceId != spaceId) return false;

        // Assert 3: The new run is distinct from the stale run
        if (newRunId == staleRunId) return false;

        return true;
    }

    // ── Additional property: Non-stale runs are NOT marked failed ─────────────
    // Ensures the system only marks runs as failed when they exceed the threshold.

    [Fact]
    public async Task NonStaleRunningRun_BlocksNewRegeneration_WithConflict()
    {
        // Arrange — seed a run that is Running but NOT stale (started 10 seconds ago)
        var db = CreateDb();
        var (spaceId, groupId, userId, publishedVersionId) = await SeedScenario(db);

        var recentRun = ScheduleRun.Create(spaceId, ScheduleRunTrigger.Regeneration, publishedVersionId, null, groupId);
        db.ScheduleRuns.Add(recentRun);
        await db.SaveChangesAsync();

        recentRun.MarkRunning("test-hash");
        // StartedAt is set to now by MarkRunning — it's fresh, not stale
        await db.SaveChangesAsync();

        var handler = new TriggerRegenerationCommandHandler(
            db,
            CreateMockQueue(),
            CreateMockTimezoneResolver(),
            BuildConfiguration());

        var command = new TriggerRegenerationCommand(spaceId, groupId, userId);

        // Act & Assert — should throw ConflictException because the run is NOT stale
        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();

        // The recent run should still be Running (not marked failed)
        var unchanged = await db.ScheduleRuns.FindAsync(recentRun.Id);
        unchanged!.Status.Should().Be(ScheduleRunStatus.Running);
    }

    [Fact]
    public async Task StaleRunAtExactThreshold_IsMarkedFailed()
    {
        // Edge case: run started exactly at the threshold boundary (1 second over)
        var db = CreateDb();
        var (spaceId, groupId, userId, publishedVersionId) = await SeedScenario(db);
        var staleRunId = await SeedStaleRun(db, spaceId, groupId, publishedVersionId, extraSecondsOverThreshold: 1);

        var handler = new TriggerRegenerationCommandHandler(
            db,
            CreateMockQueue(),
            CreateMockTimezoneResolver(),
            BuildConfiguration());

        var command = new TriggerRegenerationCommand(spaceId, groupId, userId);

        // Act
        var newRunId = await handler.Handle(command, CancellationToken.None);

        // Assert
        var staleRun = await db.ScheduleRuns.FindAsync(staleRunId);
        staleRun!.Status.Should().Be(ScheduleRunStatus.Failed);
        staleRun.ErrorSummary.Should().NotBeNullOrEmpty();

        var newRun = await db.ScheduleRuns.FindAsync(newRunId);
        newRun!.Status.Should().Be(ScheduleRunStatus.Queued);
        newRun.TriggerType.Should().Be(ScheduleRunTrigger.Regeneration);
    }
}
