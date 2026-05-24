// Feature: schedule-regeneration
// Property 5: Concurrent regeneration rejection
// For any group that has a regeneration run with status "Queued" or "Running",
// a new regeneration request SHALL be rejected with 409 and no new ScheduleRun created.
// **Validates: Requirements 9.1**

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

public class ConcurrentRegenerationRejectionPropertyTests
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
    /// Seeds an in-progress regeneration run with the given status (Queued or Running).
    /// For Running status, sets StartedAt to a recent time (not stale).
    /// </summary>
    private static async Task<Guid> SeedInProgressRun(
        AppDbContext db, Guid spaceId, Guid groupId, Guid publishedVersionId, ScheduleRunStatus status)
    {
        var run = ScheduleRun.Create(spaceId, ScheduleRunTrigger.Regeneration, publishedVersionId, null, groupId);
        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync();

        if (status == ScheduleRunStatus.Running)
        {
            run.MarkRunning("test-hash");
            // StartedAt is set to now by MarkRunning — it's fresh, not stale
            await db.SaveChangesAsync();
        }
        // If status is Queued, the run is already in Queued status by default

        return run.Id;
    }

    // ── FsCheck Generators ────────────────────────────────────────────────────

    /// <summary>
    /// Generates either Queued or Running status for the existing in-progress run.
    /// </summary>
    private static Arbitrary<ScheduleRunStatus> InProgressStatusArbitrary() =>
        Arb.From(Gen.Elements(ScheduleRunStatus.Queued, ScheduleRunStatus.Running));

    // ── Property 5: Concurrent regeneration rejection ─────────────────────────
    // For any group that has a regeneration run with status "Queued" or "Running",
    // a new regeneration request SHALL be rejected with 409 and no new ScheduleRun created.
    // **Validates: Requirements 9.1**

    [Property(MaxTest = 100)]
    public Property ConcurrentRegenerationIsRejectedWith409AndNoNewRunCreated()
    {
        return Prop.ForAll(InProgressStatusArbitrary(), status =>
        {
            return ConcurrentRejectionTestAsync(status).GetAwaiter().GetResult();
        });
    }

    private async Task<bool> ConcurrentRejectionTestAsync(ScheduleRunStatus existingRunStatus)
    {
        // Arrange
        var db = CreateDb();
        var (spaceId, groupId, userId, publishedVersionId) = await SeedScenario(db);
        var existingRunId = await SeedInProgressRun(db, spaceId, groupId, publishedVersionId, existingRunStatus);

        // Count runs before the attempt
        var runCountBefore = await db.ScheduleRuns.CountAsync();

        var mockQueue = CreateMockQueue();
        var handler = new TriggerRegenerationCommandHandler(
            db,
            mockQueue,
            CreateMockTimezoneResolver(),
            BuildConfiguration());

        var command = new TriggerRegenerationCommand(spaceId, groupId, userId);

        // Act — attempt a new regeneration (should be rejected)
        ConflictException? caughtException = null;
        try
        {
            await handler.Handle(command, CancellationToken.None);
        }
        catch (ConflictException ex)
        {
            caughtException = ex;
        }

        // Assert 1: ConflictException was thrown
        if (caughtException is null) return false;

        // Assert 2: No new ScheduleRun was created
        var runCountAfter = await db.ScheduleRuns.CountAsync();
        if (runCountAfter != runCountBefore) return false;

        // Assert 3: The existing run is still in its original status (unchanged)
        var existingRun = await db.ScheduleRuns.FindAsync(existingRunId);
        if (existingRun is null) return false;
        if (existingRun.Status != existingRunStatus) return false;

        // Assert 4: The queue was never called (no job dispatched)
        await mockQueue.DidNotReceive()
            .EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>());

        return true;
    }
}
