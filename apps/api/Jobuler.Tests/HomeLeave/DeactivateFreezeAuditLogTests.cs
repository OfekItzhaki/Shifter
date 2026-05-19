// Feature: freeze-period-discard
// Unit tests for audit log entries produced by DeactivateFreezeWithDiscardCommandHandler (Task 5.2)
// Validates: Requirements 3.4, 4.3, 4.4, 5.4
//
// NOTE: These tests provide focused coverage for audit log concerns.
// Some scenarios overlap with DeactivateFreezeWithDiscardCommandTests by design
// to ensure these critical behaviors are independently verified.

using System.Text.Json;
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
using Xunit;

namespace Jobuler.Tests.HomeLeave;

/// <summary>
/// Tests that the DeactivateFreezeWithDiscardCommandHandler produces correct audit log entries
/// for discard actions, non-discard deactivations, and denied permission attempts.
/// </summary>
public class DeactivateFreezeAuditLogTests
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

    private static IPermissionService DenyScheduleRollback()
    {
        var svc = Substitute.For<IPermissionService>();
        // Allow constraints.manage
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        // Deny schedule.rollback
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Permissions.ScheduleRollback, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException("Missing schedule.rollback permission")));
        return svc;
    }

    private static IAuditLogger CreateMockAuditLogger()
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

    private static ICumulativeTracker CreateMockCumulativeTracker()
    {
        var tracker = Substitute.For<ICumulativeTracker>();
        tracker.RecomputeForPersonAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return tracker;
    }

    private static async Task<HomeLeaveConfig> SeedFrozenConfig(AppDbContext db, Guid spaceId, Guid groupId)
    {
        var config = HomeLeaveConfig.Create(
            spaceId, groupId,
            minRestHours: 8, eligibilityThresholdHours: 168,
            leaveCapacity: 2, leaveDurationHours: 48);
        config.ActivateEmergencyFreeze(useForScheduling: false);
        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();
        return config;
    }

    private static async Task<ScheduleVersion> SeedPublishedVersion(
        AppDbContext db, Guid spaceId, int versionNumber, DateTime publishedAt)
    {
        var version = ScheduleVersion.CreateDraft(spaceId, versionNumber, null, null, Guid.NewGuid());
        version.Publish(Guid.NewGuid());
        // Use reflection to set PublishedAt to a specific time for testing
        typeof(ScheduleVersion)
            .GetProperty(nameof(ScheduleVersion.PublishedAt))!
            .SetValue(version, publishedAt);
        db.ScheduleVersions.Add(version);
        await db.SaveChangesAsync();
        return version;
    }

    private static async Task SeedOverrideAssignment(
        AppDbContext db, Guid spaceId, Guid versionId, DateTime createdAt)
    {
        var assignment = Assignment.Create(
            spaceId, versionId, Guid.NewGuid(), Guid.NewGuid(),
            AssignmentSource.Override, "Manual override during freeze");
        // Set CreatedAt via reflection to simulate freeze-period creation
        typeof(Jobuler.Domain.Common.Entity)
            .GetProperty(nameof(Jobuler.Domain.Common.Entity.CreatedAt))!
            .SetValue(assignment, createdAt);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Test discard action produces audit entry with all required fields:
    /// action = "discard_freeze_changes", entityType = "schedule_version",
    /// beforeJson contains group_id, freeze_started_at, change_count,
    /// afterJson contains new_version_id, baseline_version_id.
    /// Validates: Requirements 3.4
    /// </summary>
    [Fact]
    public async Task Discard_ProducesAuditEntry_WithAllRequiredFields()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var auditLogger = CreateMockAuditLogger();

        var config = await SeedFrozenConfig(db, spaceId, groupId);
        var freezeStartedAt = config.FreezeStartedAt!.Value;

        // Seed a pre-freeze published version
        var preFreezeVersion = await SeedPublishedVersion(
            db, spaceId, 1, freezeStartedAt.AddDays(-1));

        // Seed a draft version with an override assignment created during freeze
        var draftVersion = ScheduleVersion.CreateDraft(spaceId, 2, null, null, userId);
        db.ScheduleVersions.Add(draftVersion);
        await db.SaveChangesAsync();

        await SeedOverrideAssignment(db, spaceId, draftVersion.Id, freezeStartedAt.AddHours(1));

        // Also seed an assignment in the pre-freeze version (to be copied)
        var baselineAssignment = Assignment.Create(
            spaceId, preFreezeVersion.Id, Guid.NewGuid(), Guid.NewGuid(),
            AssignmentSource.Solver, null);
        db.Assignments.Add(baselineAssignment);
        await db.SaveChangesAsync();

        var handler = new DeactivateFreezeWithDiscardCommandHandler(
            db, AllowAllPermissions(), auditLogger,
            CreateMockCumulativeTracker(), new Helpers.NoOpCacheService());

        // Act
        var result = await handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: true),
            CancellationToken.None);

        // Assert — audit logger was called with "discard_freeze_changes" action
        result.DiscardPerformed.Should().BeTrue();

        await auditLogger.Received(1).LogAsync(
            spaceId,
            userId,
            "discard_freeze_changes",
            entityType: "schedule_version",
            entityId: result.DiscardVersionId,
            beforeJson: Arg.Is<string?>(json =>
                json != null &&
                json.Contains("group_id") &&
                json.Contains("freeze_started_at") &&
                json.Contains("change_count")),
            afterJson: Arg.Is<string?>(json =>
                json != null &&
                json.Contains("new_version_id") &&
                json.Contains("baseline_version_id")),
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Test non-discard deactivation produces audit entry with correct flag:
    /// action = "deactivate_freeze", entityType = "home_leave_config",
    /// afterJson contains discard_performed: false.
    /// Validates: Requirements 4.3, 4.4
    /// </summary>
    [Fact]
    public async Task NonDiscardDeactivation_ProducesAuditEntry_WithCorrectFlag()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var auditLogger = CreateMockAuditLogger();

        var config = await SeedFrozenConfig(db, spaceId, groupId);

        var handler = new DeactivateFreezeWithDiscardCommandHandler(
            db, AllowAllPermissions(), auditLogger,
            CreateMockCumulativeTracker(), new Helpers.NoOpCacheService());

        // Act
        var result = await handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: false),
            CancellationToken.None);

        // Assert — audit logger was called with "deactivate_freeze" action
        result.DiscardPerformed.Should().BeFalse();

        await auditLogger.Received(1).LogAsync(
            spaceId,
            userId,
            "deactivate_freeze",
            entityType: "home_leave_config",
            entityId: config.Id,
            beforeJson: Arg.Is<string?>(json =>
                json != null &&
                json.Contains("group_id") &&
                json.Contains("freeze_started_at")),
            afterJson: Arg.Is<string?>(json =>
                json != null &&
                json.Contains("discard_performed") &&
                json.Contains("false")),
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Test denied permission attempt produces audit entry with actor, space, group,
    /// and action attempted.
    /// Validates: Requirements 5.4
    /// </summary>
    [Fact]
    public async Task DeniedPermissionAttempt_ProducesAuditEntry()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var auditLogger = CreateMockAuditLogger();

        await SeedFrozenConfig(db, spaceId, groupId);

        var handler = new DeactivateFreezeWithDiscardCommandHandler(
            db, DenyScheduleRollback(), auditLogger,
            CreateMockCumulativeTracker(), new Helpers.NoOpCacheService());

        // Act — attempt discard without schedule.rollback permission
        var act = async () => await handler.Handle(
            new DeactivateFreezeWithDiscardCommand(spaceId, groupId, userId, DiscardFreezeChanges: true),
            CancellationToken.None);

        // Assert — should throw UnauthorizedAccessException
        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        // Assert — audit logger was called with "permission_denied" action
        await auditLogger.Received(1).LogAsync(
            spaceId,
            userId,
            "permission_denied",
            entityType: "home_leave_config",
            entityId: null,
            beforeJson: Arg.Is<string?>(json =>
                json != null &&
                json.Contains("group_id") &&
                json.Contains("action_attempted") &&
                json.Contains("discard_freeze_changes") &&
                json.Contains("required_permission") &&
                json.Contains("schedule.rollback")),
            afterJson: null,
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }
}
