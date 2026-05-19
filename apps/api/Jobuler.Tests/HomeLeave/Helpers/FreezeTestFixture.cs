using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Jobuler.Tests.HomeLeave;

/// <summary>
/// Shared test helpers for freeze-period-discard tests.
/// Used by DeactivateFreezeWithDiscardCommandTests, DeactivateFreezePermissionTests,
/// DeactivateFreezeAuditLogTests, and GetFreezePeriodChangesCountQueryTests.
/// </summary>
public static class FreezeTestFixture
{
    public static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    public static IPermissionService AllowAllPermissions()
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

    public static IPermissionService DenyPermission(string deniedPermission)
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

    /// <summary>
    /// Creates a permission service that allows constraints.manage but denies schedule.rollback.
    /// </summary>
    public static IPermissionService AllowConstraintsManageOnly()
    {
        var svc = Substitute.For<IPermissionService>();
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Permissions.ConstraintsManage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Permissions.ScheduleRollback, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("Permission denied."));
        return svc;
    }

    public static IAuditLogger CreateAuditLogger()
    {
        var audit = Substitute.For<IAuditLogger>();
        audit.LogAsync(
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return audit;
    }

    public static ICumulativeTracker CreateCumulativeTracker()
    {
        var tracker = Substitute.For<ICumulativeTracker>();
        tracker.RecomputeForPersonAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return tracker;
    }

    public static HomeLeaveConfig SeedFrozenConfig(
        AppDbContext db, Guid spaceId, Guid groupId, DateTime? freezeStartedAt = null)
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
            typeof(HomeLeaveConfig)
                .GetProperty(nameof(HomeLeaveConfig.FreezeStartedAt))!
                .SetValue(config, freezeStartedAt.Value);
        }

        db.HomeLeaveConfigs.Add(config);
        return config;
    }
}
