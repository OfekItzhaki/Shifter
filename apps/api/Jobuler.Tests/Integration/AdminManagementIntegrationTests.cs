// Feature: admin-management-and-scheduling
// Integration tests for admin management and scheduling (Task 33)

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Application.Tasks.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Integration;

/// <summary>
/// Integration tests for admin management and scheduling features.
/// Uses in-memory EF Core database (no real PostgreSQL connection required).
/// </summary>
public class AdminManagementIntegrationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IPermissionService AllowAllPermissions()
    {
        var svc = Substitute.For<IPermissionService>();
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        svc.HasPermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return svc;
    }

    private static IAuditLogger NoOpAuditLogger()
    {
        var logger = Substitute.For<IAuditLogger>();
        logger.LogAsync(
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<Guid?>(),
                Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logger;
    }

    private static async Task<(Guid spaceId, Guid groupId)> SeedGroup(AppDbContext db)
    {
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group", null, null);
        typeof(Jobuler.Domain.Common.Entity)
            .GetProperty("Id")!
            .SetValue(group, groupId);
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return (spaceId, groupId);
    }

    // ── Task 33.1: EF model is configured correctly — GroupTasks DbSet exists ──
    // Feature: admin-management-and-scheduling, Integration 33.1: GroupTasks DbSet

    [Fact]
    public async Task Integration_33_1_GroupTasksDbSet_ExistsAndCanBeQueried()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        // Act — create a GroupTask directly and query it back
        var task = GroupTask.Create(
            spaceId, groupId, "Test Task",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(8),
            8, 1, TaskBurdenLevel.Neutral, false, false, Guid.NewGuid());

        db.GroupTasks.Add(task);
        await db.SaveChangesAsync();

        var retrieved = await db.GroupTasks
            .Where(t => t.SpaceId == spaceId)
            .ToListAsync();

        // Assert — DbSet is accessible and returns the correct entity
        retrieved.Should().HaveCount(1);
        retrieved[0].Id.Should().Be(task.Id);
        retrieved[0].Name.Should().Be("Test Task");
        retrieved[0].IsActive.Should().BeTrue();
    }

    // ── Task 33.2: Unique constraint behavior (EF in-memory allows duplicates) ─
    // Feature: admin-management-and-scheduling, Integration 33.2: unique constraint note

    [Fact]
    public async Task Integration_33_2_UniqueConstraint_EFInMemoryAllowsDuplicates_DocumentedBehavior()
    {
        // NOTE: The unique constraint on (space_id, group_id, name) is enforced at the
        // PostgreSQL DB level via a UNIQUE INDEX. EF Core's in-memory provider does NOT
        // enforce unique indexes, so this test documents that behavior.
        //
        // In production, inserting two tasks with the same (spaceId, groupId, name)
        // would throw a DbUpdateException due to the unique constraint violation.

        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var task1 = GroupTask.Create(
            spaceId, groupId, "Duplicate Name",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(8),
            8, 1, TaskBurdenLevel.Neutral, false, false, Guid.NewGuid());

        var task2 = GroupTask.Create(
            spaceId, groupId, "Duplicate Name",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(8),
            8, 1, TaskBurdenLevel.Neutral, false, false, Guid.NewGuid());

        db.GroupTasks.AddRange(task1, task2);

        // In-memory DB allows this (no unique constraint enforcement)
        var act = async () => await db.SaveChangesAsync();
        await act.Should().NotThrowAsync(
            because: "EF in-memory provider does not enforce unique indexes; " +
                     "the real constraint is enforced at the PostgreSQL level");

        // Both records exist in in-memory DB
        var count = await db.GroupTasks.CountAsync(t => t.SpaceId == spaceId);
        count.Should().Be(2);
    }

    // ── Task 33.3: Valid burden levels accepted by validator ──────────────────
    // Feature: admin-management-and-scheduling, Integration 33.3: burden_level CHECK constraint

    [Theory]
    [InlineData("favorable")]
    [InlineData("neutral")]
    [InlineData("disliked")]
    [InlineData("hated")]
    public void Integration_33_3_ValidBurdenLevels_AcceptedByValidator(string burdenLevel)
    {
        // The CHECK constraint on burden_level is enforced at DB level.
        // Here we verify the application-layer validator accepts all valid values.
        var validator = new CreateGroupTaskCommandValidator();
        var startsAt = DateTime.UtcNow;
        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Valid Task", startsAt, startsAt.AddHours(8),
            8, 1, burdenLevel, false, false);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeTrue(
            because: $"'{burdenLevel}' is a valid burden_level value");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("extreme")]
    [InlineData("medium")]
    [InlineData("")]
    public void Integration_33_3_InvalidBurdenLevels_RejectedByValidator(string burdenLevel)
    {
        var validator = new CreateGroupTaskCommandValidator();
        var startsAt = DateTime.UtcNow;
        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Valid Task", startsAt, startsAt.AddHours(8),
            8, 1, burdenLevel, false, false);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse(
            because: $"'{burdenLevel}' is not a valid burden_level value");
    }

    // ── Task 33.4: TriggerSolverCommand creates a ScheduleRun record ──────────
    // Feature: admin-management-and-scheduling, Integration 33.4: trigger solver creates run

    [Fact]
    public async Task Integration_33_4_TriggerSolverCommand_CreatesScheduleRunRecord()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var queue = Substitute.For<ISolverJobQueue>();
        queue.EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new TriggerSolverCommandHandler(db, queue);

        // Act
        var runId = await handler.Handle(
            new TriggerSolverCommand(spaceId, "standard", userId),
            CancellationToken.None);

        // Assert — a ScheduleRun record was created
        var run = await db.ScheduleRuns.FindAsync(runId);
        run.Should().NotBeNull();
        run!.SpaceId.Should().Be(spaceId);
        run.TriggerType.Should().Be(ScheduleRunTrigger.Standard);
        run.Status.Should().Be(ScheduleRunStatus.Queued);
        run.RequestedByUserId.Should().Be(userId);

        // Assert — the job was enqueued
        await queue.Received(1).EnqueueAsync(
            Arg.Is<SolverJobMessage>(m => m.RunId == runId && m.SpaceId == spaceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Integration_33_4_TriggerSolverCommand_EmergencyMode_SetsCorrectTriggerType()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var queue = Substitute.For<ISolverJobQueue>();
        queue.EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new TriggerSolverCommandHandler(db, queue);

        // Act
        var runId = await handler.Handle(
            new TriggerSolverCommand(spaceId, "emergency", userId),
            CancellationToken.None);

        // Assert
        var run = await db.ScheduleRuns.FindAsync(runId);
        run!.TriggerType.Should().Be(ScheduleRunTrigger.Emergency);
    }

    // ── Task 33.5: PublishVersionCommand archives previous published version ───
    // Feature: admin-management-and-scheduling, Integration 33.5: publish archives previous

    [Fact]
    public async Task Integration_33_5_PublishVersionCommand_ArchivesPreviousPublishedVersion()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Create and publish version 1
        var version1 = ScheduleVersion.CreateDraft(spaceId, 1, null, null, userId);
        db.ScheduleVersions.Add(version1);
        await db.SaveChangesAsync();

        var publishHandler = new PublishVersionCommandHandler(db, NoOpAuditLogger());

        await publishHandler.Handle(
            new PublishVersionCommand(spaceId, version1.Id, userId),
            CancellationToken.None);

        // Verify version 1 is Published
        var v1After = await db.ScheduleVersions.FindAsync(version1.Id);
        v1After!.Status.Should().Be(ScheduleVersionStatus.Published);

        // Create version 2 (draft)
        var version2 = ScheduleVersion.CreateDraft(spaceId, 2, null, null, userId);
        db.ScheduleVersions.Add(version2);
        await db.SaveChangesAsync();

        // Act — publish version 2
        await publishHandler.Handle(
            new PublishVersionCommand(spaceId, version2.Id, userId),
            CancellationToken.None);

        // Assert — version 1 is now Archived
        var v1Final = await db.ScheduleVersions.FindAsync(version1.Id);
        v1Final!.Status.Should().Be(ScheduleVersionStatus.Archived);

        // Assert — version 2 is Published
        var v2Final = await db.ScheduleVersions.FindAsync(version2.Id);
        v2Final!.Status.Should().Be(ScheduleVersionStatus.Published);
    }

    [Fact]
    public async Task Integration_33_5_PublishVersionCommand_NoExistingPublished_PublishesCleanly()
    {
        // Arrange — no previously published version
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var version = ScheduleVersion.CreateDraft(spaceId, 1, null, null, userId);
        db.ScheduleVersions.Add(version);
        await db.SaveChangesAsync();

        var handler = new PublishVersionCommandHandler(db, NoOpAuditLogger());

        // Act
        var act = async () => await handler.Handle(
            new PublishVersionCommand(spaceId, version.Id, userId),
            CancellationToken.None);

        // Assert — no exception; version is Published
        await act.Should().NotThrowAsync();

        var published = await db.ScheduleVersions.FindAsync(version.Id);
        published!.Status.Should().Be(ScheduleVersionStatus.Published);
    }
}
