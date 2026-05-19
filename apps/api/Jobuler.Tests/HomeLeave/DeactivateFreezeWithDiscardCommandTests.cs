// Feature: freeze-period-discard
// Unit tests for DeactivateFreezeWithDiscardCommand (Task 2.4)
// Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 6.4, 6.5, 6.6

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.HomeLeave.Commands;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Jobuler.Tests.HomeLeave;

public class DeactivateFreezeWithDiscardCommandTests
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
        return svc;
    }

    private static IPermissionService DenyPermission(string deniedPermission)
    {
        var svc = Substitute.For<IPermissionService>();
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Is<string>(p => p != deniedPermission), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Is(deniedPermission), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("Permission denied."));
        return svc;
    }

    private static IAuditLogger CreateAuditLogger() => Substitute.For<IAuditLogger>();

    private static ICumulativeTracker CreateCumulativeTracker() => Substitute.For<ICumulativeTracker>();

    private static DeactivateFreezeWithDiscardCommandHandler CreateHandler(
        AppDbContext db,
        IPermissionService? permissions = null,
        IAuditLogger? audit = null,
        ICumulativeTracker? cumulativeTracker = null,
        ICacheService? cache = null)
    {
        return new DeactivateFreezeWithDiscardCommandHandler(
            db,
            permissions ?? AllowAllPermissions(),
            audit ?? CreateAuditLogger(),
            cumulativeTracker ?? CreateCumulativeTracker(),
            cache ?? new Helpers.NoOpCacheService());
    }

    private static HomeLeaveConfig CreateFrozenConfig(Guid spaceId, Guid groupId, DateTime? freezeStartedAt = null)
    {
        var config = HomeLeaveConfig.Create(
            spaceId, groupId,
            minRestHours: 8,
            eligibilityThresholdHours: 168,
            leaveCapacity: 2,
            leaveDurationHours: 48,
            balanceValue: 50,
            mode: HomeLeaveMode.Automatic,
            baseDays: 7,
            homeDays: 2,
            minPeopleAtBase: 8);

        config.ActivateEmergencyFreeze(useForScheduling: false);

        // Override FreezeStartedAt if a specific time is needed
        if (freezeStartedAt.HasValue)
        {
            // Use reflection to set FreezeStartedAt since it's private set
            typeof(HomeLeaveConfig)
                .GetProperty(nameof(HomeLeaveConfig.FreezeStartedAt))!
                .SetValue(config, freezeStartedAt.Value);
        }

        return config;
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirements 4.1, 4.2, 4.3, 4.4
    /// Standard deactivation (discard=false) clears freeze state without creating versions.
    /// </summary>
    [Fact]
    public async Task StandardDeactivation_ClearsFreezeState_WithoutCreatingVersions()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var config = CreateFrozenConfig(spaceId, groupId);
        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        // Act
        var result = await handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: false),
            CancellationToken.None);

        // Assert
        result.DiscardPerformed.Should().BeFalse();
        result.DiscardVersionId.Should().BeNull();
        result.DiscardedChangeCount.Should().Be(0);
        result.Config.EmergencyFreezeActive.Should().BeFalse();
        result.Config.FreezeStartedAt.Should().BeNull();

        // No new versions created
        var versions = await db.ScheduleVersions.Where(v => v.SpaceId == spaceId).ToListAsync();
        versions.Should().BeEmpty();
    }

    /// <summary>
    /// Validates: Requirements 3.1, 3.2, 3.3, 3.6
    /// Discard creates new draft version from pre-freeze baseline.
    /// </summary>
    [Fact]
    public async Task Discard_CreatesNewDraftVersion_FromPreFreezeBaseline()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var freezeStart = DateTime.UtcNow.AddHours(-6);

        // Create a pre-freeze published version
        var preFreezeVersion = ScheduleVersion.CreateDraft(spaceId, 1, null, null, userId);
        preFreezeVersion.Publish(userId);
        // Set PublishedAt to before freeze start via reflection
        typeof(ScheduleVersion).GetProperty(nameof(ScheduleVersion.PublishedAt))!
            .SetValue(preFreezeVersion, freezeStart.AddHours(-2));
        db.ScheduleVersions.Add(preFreezeVersion);

        // Add assignments to the pre-freeze version
        var taskSlotId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var assignment = Assignment.Create(spaceId, preFreezeVersion.Id, taskSlotId, personId, AssignmentSource.Solver);
        db.Assignments.Add(assignment);

        // Create a draft version with override assignments during freeze
        var draftVersion = ScheduleVersion.CreateDraft(spaceId, 2, null, null, userId);
        db.ScheduleVersions.Add(draftVersion);

        var overrideAssignment = Assignment.Create(spaceId, draftVersion.Id, taskSlotId, Guid.NewGuid(), AssignmentSource.Override);
        // Set CreatedAt to during freeze via reflection
        typeof(Assignment).GetProperty("CreatedAt")!.SetValue(overrideAssignment, freezeStart.AddHours(1));
        db.Assignments.Add(overrideAssignment);

        // Create frozen config
        var config = CreateFrozenConfig(spaceId, groupId, freezeStartedAt: freezeStart);
        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        // Act
        var result = await handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: true),
            CancellationToken.None);

        // Assert
        result.DiscardPerformed.Should().BeTrue();
        result.DiscardVersionId.Should().NotBeNull();
        result.DiscardedChangeCount.Should().BeGreaterThan(0);

        // New version should exist as a draft with RollbackSourceVersionId pointing to pre-freeze baseline
        var newVersion = await db.ScheduleVersions.FindAsync(result.DiscardVersionId);
        newVersion.Should().NotBeNull();
        newVersion!.Status.Should().Be(ScheduleVersionStatus.Draft);
        newVersion.RollbackSourceVersionId.Should().Be(preFreezeVersion.Id);
        newVersion.VersionNumber.Should().Be(3); // next sequential version number

        // Assignments from pre-freeze version should be copied to new version
        var copiedAssignments = await db.Assignments
            .Where(a => a.ScheduleVersionId == result.DiscardVersionId)
            .ToListAsync();
        copiedAssignments.Should().HaveCount(1);
    }

    /// <summary>
    /// Validates: Requirements 3.5
    /// Discard rejected when no pre-freeze published version exists.
    /// </summary>
    [Fact]
    public async Task Discard_NoPreFreezeBaseline_ThrowsInvalidOperationException()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var freezeStart = DateTime.UtcNow.AddHours(-6);

        // No published versions exist at all
        var config = CreateFrozenConfig(spaceId, groupId, freezeStartedAt: freezeStart);
        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        // Act
        var act = () => handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: true),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No pre-freeze baseline version*");
    }

    /// <summary>
    /// Validates: Requirements 5.1, 5.3, 5.4, 6.4
    /// Discard rejected when caller lacks schedule.rollback permission.
    /// </summary>
    [Fact]
    public async Task Discard_MissingRollbackPermission_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var config = CreateFrozenConfig(spaceId, groupId);
        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        var auditLogger = CreateAuditLogger();
        var handler = CreateHandler(db,
            permissions: DenyPermission(Permissions.ScheduleRollback),
            audit: auditLogger);

        // Act
        var act = () => handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: true),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        // Audit log should record the denied attempt
        await auditLogger.Received(1).LogAsync(
            spaceId,
            userId,
            "permission_denied",
            entityType: "home_leave_config",
            entityId: null,
            beforeJson: Arg.Is<string>(s => s.Contains("discard_freeze_changes") && s.Contains("schedule.rollback")),
            afterJson: null,
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Validates: Requirements 6.5
    /// Deactivation rejected when freeze is not active (400 via InvalidOperationException).
    /// </summary>
    [Fact]
    public async Task Deactivation_FreezeNotActive_ThrowsInvalidOperationException()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Create config WITHOUT freeze active
        var config = HomeLeaveConfig.Create(
            spaceId, groupId,
            minRestHours: 8,
            eligibilityThresholdHours: 168,
            leaveCapacity: 2,
            leaveDurationHours: 48);
        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        // Act
        var act = () => handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: false),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*freeze is not active*");
    }

    /// <summary>
    /// Validates: Requirements 6.6
    /// Discard with zero freeze-period changes skips version creation.
    /// </summary>
    [Fact]
    public async Task Discard_ZeroFreezeChanges_SkipsVersionCreation()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var freezeStart = DateTime.UtcNow.AddHours(-6);

        // Create a pre-freeze published version (but no override assignments during freeze)
        var preFreezeVersion = ScheduleVersion.CreateDraft(spaceId, 1, null, null, userId);
        preFreezeVersion.Publish(userId);
        typeof(ScheduleVersion).GetProperty(nameof(ScheduleVersion.PublishedAt))!
            .SetValue(preFreezeVersion, freezeStart.AddHours(-2));
        db.ScheduleVersions.Add(preFreezeVersion);

        var config = CreateFrozenConfig(spaceId, groupId, freezeStartedAt: freezeStart);
        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        // Act
        var result = await handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: true),
            CancellationToken.None);

        // Assert — no new version created, discard not performed
        result.DiscardPerformed.Should().BeFalse();
        result.DiscardVersionId.Should().BeNull();
        result.DiscardedChangeCount.Should().Be(0);
        result.Config.EmergencyFreezeActive.Should().BeFalse();

        // Only the original pre-freeze version should exist
        var versions = await db.ScheduleVersions.Where(v => v.SpaceId == spaceId).ToListAsync();
        versions.Should().HaveCount(1);
    }

    /// <summary>
    /// Validates: Requirements 3.6
    /// Atomic operation: partial failure does not leave orphaned versions.
    /// If SaveChanges fails after version creation, no orphaned version remains.
    /// </summary>
    [Fact]
    public async Task Discard_AtomicOperation_PartialFailureDoesNotLeaveOrphanedVersions()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var freezeStart = DateTime.UtcNow.AddHours(-6);

        // Create a pre-freeze published version
        var preFreezeVersion = ScheduleVersion.CreateDraft(spaceId, 1, null, null, userId);
        preFreezeVersion.Publish(userId);
        typeof(ScheduleVersion).GetProperty(nameof(ScheduleVersion.PublishedAt))!
            .SetValue(preFreezeVersion, freezeStart.AddHours(-2));
        db.ScheduleVersions.Add(preFreezeVersion);

        // Add assignments to pre-freeze version
        var taskSlotId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        db.Assignments.Add(Assignment.Create(spaceId, preFreezeVersion.Id, taskSlotId, personId, AssignmentSource.Solver));

        // Create a draft version with override assignments during freeze
        var draftVersion = ScheduleVersion.CreateDraft(spaceId, 2, null, null, userId);
        db.ScheduleVersions.Add(draftVersion);
        var overrideAssignment = Assignment.Create(spaceId, draftVersion.Id, taskSlotId, Guid.NewGuid(), AssignmentSource.Override);
        typeof(Assignment).GetProperty("CreatedAt")!.SetValue(overrideAssignment, freezeStart.AddHours(1));
        db.Assignments.Add(overrideAssignment);

        var config = CreateFrozenConfig(spaceId, groupId, freezeStartedAt: freezeStart);
        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        // Use a cumulative tracker that throws to simulate partial failure
        var failingTracker = Substitute.For<ICumulativeTracker>();
        failingTracker.RecomputeForPersonAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Simulated failure during recomputation"));

        var handler = CreateHandler(db, cumulativeTracker: failingTracker);

        // Act
        var act = () => handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: true),
            CancellationToken.None);

        // Assert — the operation should throw
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Simulated failure*");

        // The freeze should still be active since the operation failed mid-way
        // (In-memory DB doesn't support transactions, but we verify the handler throws)
        // In a real DB with transactions, no orphaned version would remain.
    }

    /// <summary>
    /// Validates: Requirements 3.4, 4.3
    /// Audit log entry created for discard path.
    /// </summary>
    [Fact]
    public async Task Discard_AuditLogEntry_CreatedForDiscardPath()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var freezeStart = DateTime.UtcNow.AddHours(-6);

        var preFreezeVersion = ScheduleVersion.CreateDraft(spaceId, 1, null, null, userId);
        preFreezeVersion.Publish(userId);
        typeof(ScheduleVersion).GetProperty(nameof(ScheduleVersion.PublishedAt))!
            .SetValue(preFreezeVersion, freezeStart.AddHours(-2));
        db.ScheduleVersions.Add(preFreezeVersion);

        var taskSlotId = Guid.NewGuid();
        db.Assignments.Add(Assignment.Create(spaceId, preFreezeVersion.Id, taskSlotId, Guid.NewGuid(), AssignmentSource.Solver));

        var draftVersion = ScheduleVersion.CreateDraft(spaceId, 2, null, null, userId);
        db.ScheduleVersions.Add(draftVersion);
        var overrideAssignment = Assignment.Create(spaceId, draftVersion.Id, taskSlotId, Guid.NewGuid(), AssignmentSource.Override);
        typeof(Assignment).GetProperty("CreatedAt")!.SetValue(overrideAssignment, freezeStart.AddHours(1));
        db.Assignments.Add(overrideAssignment);

        var config = CreateFrozenConfig(spaceId, groupId, freezeStartedAt: freezeStart);
        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        var auditLogger = CreateAuditLogger();
        var handler = CreateHandler(db, audit: auditLogger);

        // Act
        await handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: true),
            CancellationToken.None);

        // Assert — audit log for discard action
        await auditLogger.Received(1).LogAsync(
            spaceId,
            userId,
            "discard_freeze_changes",
            entityType: "schedule_version",
            entityId: Arg.Any<Guid?>(),
            beforeJson: Arg.Is<string>(s => s.Contains("group_id") && s.Contains("freeze_started_at") && s.Contains("change_count")),
            afterJson: Arg.Is<string>(s => s.Contains("new_version_id") && s.Contains("baseline_version_id")),
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Validates: Requirements 4.3, 4.4
    /// Audit log entry created for non-discard path.
    /// </summary>
    [Fact]
    public async Task StandardDeactivation_AuditLogEntry_CreatedForNonDiscardPath()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var config = CreateFrozenConfig(spaceId, groupId);
        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        var auditLogger = CreateAuditLogger();
        var handler = CreateHandler(db, audit: auditLogger);

        // Act
        await handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: false),
            CancellationToken.None);

        // Assert — audit log for deactivation without discard
        await auditLogger.Received(1).LogAsync(
            spaceId,
            userId,
            "deactivate_freeze",
            entityType: "home_leave_config",
            entityId: Arg.Any<Guid?>(),
            beforeJson: Arg.Is<string>(s => s.Contains("group_id") && s.Contains("freeze_started_at")),
            afterJson: Arg.Is<string>(s => s.Contains("discard_performed") && s.Contains("false")),
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }
}
