// Feature: freeze-period-discard
// Unit tests for API endpoint permission enforcement (Task 4.3)
// Validates: Requirements 5.1, 5.2, 5.3, 5.4, 6.5

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.HomeLeave.Commands;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Jobuler.Tests.HomeLeave;

public class DeactivateFreezePermissionTests
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

    /// <summary>
    /// Creates a permission service that allows constraints.manage but denies schedule.rollback.
    /// </summary>
    private static IPermissionService AllowConstraintsManageOnly()
    {
        var svc = Substitute.For<IPermissionService>();

        // Allow constraints.manage
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Deny schedule.rollback
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Permissions.ScheduleRollback, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("Permission denied."));

        return svc;
    }

    private static IAuditLogger CreateAuditLogger()
    {
        var audit = Substitute.For<IAuditLogger>();
        audit.LogAsync(
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return audit;
    }

    private static ICumulativeTracker CreateCumulativeTracker()
    {
        var tracker = Substitute.For<ICumulativeTracker>();
        tracker.RecomputeForPersonAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return tracker;
    }

    private static async Task<(AppDbContext db, Guid spaceId, Guid groupId, Guid userId)> SetupWithActiveFreezeAsync()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

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

        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        return (db, spaceId, groupId, userId);
    }

    private static async Task<(AppDbContext db, Guid spaceId, Guid groupId, Guid userId)> SetupWithInactiveFreezeAsync()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

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

        // Freeze is NOT active
        db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        return (db, spaceId, groupId, userId);
    }

    // ── Test: 403 returned when discard requested without schedule.rollback permission ──
    // Validates: Requirements 5.1, 5.3

    [Fact]
    public async Task DiscardRequested_WithoutScheduleRollbackPermission_ThrowsUnauthorized()
    {
        // Arrange
        var (db, spaceId, groupId, userId) = await SetupWithActiveFreezeAsync();
        var permissions = AllowConstraintsManageOnly();
        var audit = CreateAuditLogger();

        var handler = new DeactivateFreezeWithDiscardCommandHandler(
            db, permissions, audit, CreateCumulativeTracker(), new Helpers.NoOpCacheService());

        var command = new DeactivateFreezeWithDiscardCommand(
            spaceId, groupId, userId, DiscardFreezeChanges: true);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert — must throw UnauthorizedAccessException (maps to 403 via middleware)
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Test: Standard deactivation succeeds with only constraints.manage permission ──
    // Validates: Requirements 5.1, 5.2

    [Fact]
    public async Task StandardDeactivation_WithConstraintsManageOnly_Succeeds()
    {
        // Arrange
        var (db, spaceId, groupId, userId) = await SetupWithActiveFreezeAsync();
        var permissions = AllowConstraintsManageOnly();
        var audit = CreateAuditLogger();

        var handler = new DeactivateFreezeWithDiscardCommandHandler(
            db, permissions, audit, CreateCumulativeTracker(), new Helpers.NoOpCacheService());

        var command = new DeactivateFreezeWithDiscardCommand(
            spaceId, groupId, userId, DiscardFreezeChanges: false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — deactivation succeeds without discard
        result.Should().NotBeNull();
        result.DiscardPerformed.Should().BeFalse();
        result.Config.EmergencyFreezeActive.Should().BeFalse();
    }

    // ── Test: Denied attempt recorded in audit log ──
    // Validates: Requirements 5.4

    [Fact]
    public async Task DiscardDenied_RecordsDeniedAttemptInAuditLog()
    {
        // Arrange
        var (db, spaceId, groupId, userId) = await SetupWithActiveFreezeAsync();
        var permissions = AllowConstraintsManageOnly();
        var audit = CreateAuditLogger();

        var handler = new DeactivateFreezeWithDiscardCommandHandler(
            db, permissions, audit, CreateCumulativeTracker(), new Helpers.NoOpCacheService());

        var command = new DeactivateFreezeWithDiscardCommand(
            spaceId, groupId, userId, DiscardFreezeChanges: true);

        // Act — expect exception
        try
        {
            await handler.Handle(command, CancellationToken.None);
        }
        catch (UnauthorizedAccessException)
        {
            // Expected
        }

        // Assert — audit log was called with permission_denied action
        await audit.Received(1).LogAsync(
            spaceId,
            userId,
            "permission_denied",
            entityType: "home_leave_config",
            entityId: null,
            beforeJson: Arg.Is<string?>(s => s != null && s.Contains("discard_freeze_changes") && s.Contains("schedule.rollback")),
            afterJson: null,
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    // ── Test: 400 returned when freeze not active ──
    // Validates: Requirements 6.5

    [Fact]
    public async Task DeactivateFreeze_WhenFreezeNotActive_ThrowsInvalidOperation()
    {
        // Arrange
        var (db, spaceId, groupId, userId) = await SetupWithInactiveFreezeAsync();
        var permissions = AllowAllPermissions();
        var audit = CreateAuditLogger();

        var handler = new DeactivateFreezeWithDiscardCommandHandler(
            db, permissions, audit, CreateCumulativeTracker(), new Helpers.NoOpCacheService());

        var command = new DeactivateFreezeWithDiscardCommand(
            spaceId, groupId, userId, DiscardFreezeChanges: false);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert — must throw InvalidOperationException (maps to 400 via middleware)
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not active*");
    }
}
