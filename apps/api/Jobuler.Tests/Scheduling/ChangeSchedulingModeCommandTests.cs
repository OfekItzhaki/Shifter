using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Spaces.Queries;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Organizations;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class ChangeSchedulingModeCommandTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IPermissionService AllowAll()
    {
        var svc = Substitute.For<IPermissionService>();
        svc.RequirePermissionAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return svc;
    }

    private static IPermissionService DenyAll()
    {
        var svc = Substitute.For<IPermissionService>();
        svc.RequirePermissionAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException("Permission denied.")));
        return svc;
    }

    [Fact]
    public async Task Handle_ChangesToSelfService_WhenNoActiveRequests_Succeeds()
    {
        // Arrange
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll());
        var command = new ChangeSchedulingModeCommand(spaceId, group.Id, Guid.NewGuid(), SchedulingMode.SelfService);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updated = await db.Groups.FindAsync(group.Id);
        updated!.SchedulingMode.Should().Be(SchedulingMode.SelfService);

        var config = await db.SelfServiceConfigs.SingleAsync(c => c.GroupId == group.Id);
        config.SpaceId.Should().Be(spaceId);
        config.MinShiftsPerCycle.Should().Be(0);
        config.MaxShiftsPerCycle.Should().Be(7);
        config.MaxAbsencesPerCycle.Should().Be(3);
        config.MaxLateCancellationsPerCycle.Should().Be(2);
        config.AllowMemberShiftClaims.Should().BeTrue();
        config.AllowWaitlist.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ChangesToSelfService_CreatesConfigFromConfiguredDefaults()
    {
        // Arrange
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var defaults = new SelfServiceDefaultPolicyOptions
        {
            MinShiftsPerCycle = 1,
            MaxShiftsPerCycle = 5,
            RequestWindowOpenOffsetHours = 240,
            RequestWindowCloseOffsetHours = 48,
            CancellationCutoffHours = 36,
            MaxAbsencesPerCycle = 4,
            MaxLateCancellationsPerCycle = 1,
            LateCancellationWindowHours = 18,
            WaitlistOfferMinutes = 45,
            CycleDurationDays = 14,
            AllowMemberShiftClaims = true,
            AllowWaitlist = false,
            AllowShiftChangeRequests = true,
            AllowAbsenceReports = true,
            AllowShiftSwaps = false
        };

        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll(), Options.Create(defaults));
        var command = new ChangeSchedulingModeCommand(spaceId, group.Id, Guid.NewGuid(), SchedulingMode.SelfService);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var config = await db.SelfServiceConfigs.SingleAsync(c => c.GroupId == group.Id);
        config.MinShiftsPerCycle.Should().Be(1);
        config.MaxShiftsPerCycle.Should().Be(5);
        config.RequestWindowOpenOffsetHours.Should().Be(240);
        config.RequestWindowCloseOffsetHours.Should().Be(48);
        config.CancellationCutoffHours.Should().Be(36);
        config.MaxAbsencesPerCycle.Should().Be(4);
        config.MaxLateCancellationsPerCycle.Should().Be(1);
        config.LateCancellationWindowHours.Should().Be(18);
        config.WaitlistOfferMinutes.Should().Be(45);
        config.CycleDurationDays.Should().Be(14);
        config.AllowWaitlist.Should().BeFalse();
        config.AllowShiftSwaps.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ChangesToSelfService_UsesSpaceDefaultsBeforeInstallDefaults()
    {
        // Arrange
        using var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Client Org", ownerId, "IL", "security", "he");
        var space = Space.Create("Client Space", ownerId, organizationId: organization.Id);
        var spaceId = space.Id;
        var group = Group.Create(spaceId, null, "Test Group");
        var organizationDefaults = OrganizationSelfServiceDefaults.Create(
            organization.Id,
            minShiftsPerCycle: 3,
            maxShiftsPerCycle: 8,
            requestWindowOpenOffsetHours: 216,
            requestWindowCloseOffsetHours: 30,
            cancellationCutoffHours: 28,
            maxAbsencesPerCycle: 2,
            maxLateCancellationsPerCycle: 1,
            lateCancellationWindowHours: 16,
            waitlistOfferMinutes: 75,
            cycleDurationDays: 12,
            allowMemberShiftClaims: true,
            allowWaitlist: true,
            allowShiftChangeRequests: false,
            allowAbsenceReports: true,
            allowShiftSwaps: false);
        var spaceDefaults = SpaceSelfServiceDefaults.Create(
            spaceId,
            minShiftsPerCycle: 2,
            maxShiftsPerCycle: 6,
            requestWindowOpenOffsetHours: 192,
            requestWindowCloseOffsetHours: 36,
            cancellationCutoffHours: 30,
            maxAbsencesPerCycle: 1,
            maxLateCancellationsPerCycle: 0,
            lateCancellationWindowHours: 12,
            waitlistOfferMinutes: 90,
            cycleDurationDays: 10,
            allowMemberShiftClaims: true,
            allowWaitlist: false,
            allowShiftChangeRequests: true,
            allowAbsenceReports: false,
            allowShiftSwaps: true);

        db.Organizations.Add(organization);
        db.Spaces.Add(space);
        db.Groups.Add(group);
        db.OrganizationSelfServiceDefaults.Add(organizationDefaults);
        db.SpaceSelfServiceDefaults.Add(spaceDefaults);
        await db.SaveChangesAsync();

        var installDefaults = new SelfServiceDefaultPolicyOptions
        {
            MinShiftsPerCycle = 0,
            MaxShiftsPerCycle = 3,
            RequestWindowOpenOffsetHours = 120,
            RequestWindowCloseOffsetHours = 24,
            CancellationCutoffHours = 24,
            MaxAbsencesPerCycle = 9,
            MaxLateCancellationsPerCycle = 9,
            LateCancellationWindowHours = 24,
            WaitlistOfferMinutes = 30,
            CycleDurationDays = 7,
            AllowMemberShiftClaims = false,
            AllowWaitlist = true,
            AllowShiftChangeRequests = false,
            AllowAbsenceReports = true,
            AllowShiftSwaps = false
        };

        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll(), Options.Create(installDefaults));
        var command = new ChangeSchedulingModeCommand(spaceId, group.Id, Guid.NewGuid(), SchedulingMode.SelfService);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var config = await db.SelfServiceConfigs.SingleAsync(c => c.GroupId == group.Id);
        config.MinShiftsPerCycle.Should().Be(2);
        config.MaxShiftsPerCycle.Should().Be(6);
        config.RequestWindowOpenOffsetHours.Should().Be(192);
        config.RequestWindowCloseOffsetHours.Should().Be(36);
        config.CancellationCutoffHours.Should().Be(30);
        config.MaxAbsencesPerCycle.Should().Be(1);
        config.MaxLateCancellationsPerCycle.Should().Be(0);
        config.LateCancellationWindowHours.Should().Be(12);
        config.WaitlistOfferMinutes.Should().Be(90);
        config.CycleDurationDays.Should().Be(10);
        config.AllowWaitlist.Should().BeFalse();
        config.AllowAbsenceReports.Should().BeFalse();
        config.AllowShiftSwaps.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ChangesToSelfService_UsesOrganizationDefaultsBeforeInstallDefaults()
    {
        // Arrange
        using var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Client Org", ownerId, "IL", "security", "he");
        var space = Space.Create("Client Space", ownerId, organizationId: organization.Id);
        var group = Group.Create(space.Id, null, "Test Group");
        var organizationDefaults = OrganizationSelfServiceDefaults.Create(
            organization.Id,
            minShiftsPerCycle: 3,
            maxShiftsPerCycle: 8,
            requestWindowOpenOffsetHours: 216,
            requestWindowCloseOffsetHours: 30,
            cancellationCutoffHours: 28,
            maxAbsencesPerCycle: 2,
            maxLateCancellationsPerCycle: 1,
            lateCancellationWindowHours: 16,
            waitlistOfferMinutes: 75,
            cycleDurationDays: 12,
            allowMemberShiftClaims: true,
            allowWaitlist: true,
            allowShiftChangeRequests: false,
            allowAbsenceReports: true,
            allowShiftSwaps: false);

        db.Organizations.Add(organization);
        db.Spaces.Add(space);
        db.Groups.Add(group);
        db.OrganizationSelfServiceDefaults.Add(organizationDefaults);
        await db.SaveChangesAsync();

        var installDefaults = new SelfServiceDefaultPolicyOptions
        {
            MinShiftsPerCycle = 0,
            MaxShiftsPerCycle = 3,
            RequestWindowOpenOffsetHours = 120,
            RequestWindowCloseOffsetHours = 24,
            CancellationCutoffHours = 24,
            MaxAbsencesPerCycle = 9,
            MaxLateCancellationsPerCycle = 9,
            LateCancellationWindowHours = 24,
            WaitlistOfferMinutes = 30,
            CycleDurationDays = 7,
            AllowMemberShiftClaims = false,
            AllowWaitlist = false,
            AllowShiftChangeRequests = true,
            AllowAbsenceReports = false,
            AllowShiftSwaps = true
        };

        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll(), Options.Create(installDefaults));
        var command = new ChangeSchedulingModeCommand(space.Id, group.Id, Guid.NewGuid(), SchedulingMode.SelfService);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var config = await db.SelfServiceConfigs.SingleAsync(c => c.GroupId == group.Id);
        config.MinShiftsPerCycle.Should().Be(3);
        config.MaxShiftsPerCycle.Should().Be(8);
        config.RequestWindowOpenOffsetHours.Should().Be(216);
        config.RequestWindowCloseOffsetHours.Should().Be(30);
        config.CancellationCutoffHours.Should().Be(28);
        config.MaxAbsencesPerCycle.Should().Be(2);
        config.MaxLateCancellationsPerCycle.Should().Be(1);
        config.LateCancellationWindowHours.Should().Be(16);
        config.WaitlistOfferMinutes.Should().Be(75);
        config.CycleDurationDays.Should().Be(12);
        config.AllowWaitlist.Should().BeTrue();
        config.AllowShiftChangeRequests.Should().BeFalse();
        config.AllowShiftSwaps.Should().BeFalse();
    }

    [Fact]
    public async Task GetSpaceSelfServiceDefaults_ReturnsOrganizationDefaults_WhenSpaceDefaultsMissing()
    {
        // Arrange
        using var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Client Org", ownerId, "IL", "security", "he");
        var space = Space.Create("Client Space", ownerId, organizationId: organization.Id);
        var organizationDefaults = OrganizationSelfServiceDefaults.Create(
            organization.Id,
            minShiftsPerCycle: 2,
            maxShiftsPerCycle: 9,
            requestWindowOpenOffsetHours: 200,
            requestWindowCloseOffsetHours: 40,
            cancellationCutoffHours: 30,
            maxAbsencesPerCycle: 4,
            maxLateCancellationsPerCycle: 1,
            lateCancellationWindowHours: 18,
            waitlistOfferMinutes: 80,
            cycleDurationDays: 14,
            allowMemberShiftClaims: true,
            allowWaitlist: false,
            allowShiftChangeRequests: true,
            allowAbsenceReports: true,
            allowShiftSwaps: false);

        db.Organizations.Add(organization);
        db.Spaces.Add(space);
        db.OrganizationSelfServiceDefaults.Add(organizationDefaults);
        await db.SaveChangesAsync();

        var handler = new GetSpaceSelfServiceDefaultsQueryHandler(
            db,
            Options.Create(new SelfServiceDefaultPolicyOptions()));

        // Act
        var result = await handler.Handle(new GetSpaceSelfServiceDefaultsQuery(space.Id), CancellationToken.None);

        // Assert
        result.Source.Should().Be("organization");
        result.MinShiftsPerCycle.Should().Be(2);
        result.MaxShiftsPerCycle.Should().Be(9);
        result.RequestWindowOpenOffsetHours.Should().Be(200);
        result.RequestWindowCloseOffsetHours.Should().Be(40);
        result.AllowWaitlist.Should().BeFalse();
        result.AllowShiftSwaps.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ChangesToSelfService_WhenConfigAlreadyExists_DoesNotOverwritePolicy()
    {
        // Arrange
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        var existingConfig = SelfServiceConfig.Create(
            spaceId,
            group.Id,
            minShiftsPerCycle: 2,
            maxShiftsPerCycle: 4,
            requestWindowOpenOffsetHours: 96,
            requestWindowCloseOffsetHours: 12,
            cancellationCutoffHours: 18,
            maxLateCancellationsPerCycle: 1,
            lateCancellationWindowHours: 12,
            waitlistOfferMinutes: 30,
            cycleDurationDays: 14);
        existingConfig.SetAbsenceReportLimit(1);
        existingConfig.SetWorkflowPermissions(
            allowMemberShiftClaims: true,
            allowWaitlist: false,
            allowShiftChangeRequests: true,
            allowAbsenceReports: true,
            allowShiftSwaps: false);

        db.Groups.Add(group);
        db.SelfServiceConfigs.Add(existingConfig);
        await db.SaveChangesAsync();

        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll());
        var command = new ChangeSchedulingModeCommand(spaceId, group.Id, Guid.NewGuid(), SchedulingMode.SelfService);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var configs = await db.SelfServiceConfigs.Where(c => c.GroupId == group.Id).ToListAsync();
        configs.Should().ContainSingle();
        configs[0].MinShiftsPerCycle.Should().Be(2);
        configs[0].MaxShiftsPerCycle.Should().Be(4);
        configs[0].MaxAbsencesPerCycle.Should().Be(1);
        configs[0].AllowWaitlist.Should().BeFalse();
        configs[0].AllowShiftSwaps.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ChangesToAutoGenerated_WhenNoActiveRequests_Succeeds()
    {
        // Arrange
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        group.SetSchedulingMode(SchedulingMode.SelfService);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll());
        var command = new ChangeSchedulingModeCommand(spaceId, group.Id, Guid.NewGuid(), SchedulingMode.AutoGenerated);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updated = await db.Groups.FindAsync(group.Id);
        updated!.SchedulingMode.Should().Be(SchedulingMode.AutoGenerated);
    }

    [Fact]
    public async Task Handle_SameMode_IsNoOp()
    {
        // Arrange
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll());
        var command = new ChangeSchedulingModeCommand(spaceId, group.Id, Guid.NewGuid(), SchedulingMode.AutoGenerated);

        // Act — should not throw
        await handler.Handle(command, CancellationToken.None);

        // Assert — still AutoGenerated
        var updated = await db.Groups.FindAsync(group.Id);
        updated!.SchedulingMode.Should().Be(SchedulingMode.AutoGenerated);
    }

    [Fact]
    public async Task Handle_WithPendingRequests_ThrowsInvalidOperation()
    {
        // Arrange
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        group.SetSchedulingMode(SchedulingMode.SelfService);
        db.Groups.Add(group);

        var shiftRequest = ShiftRequest.Create(
            spaceId, Guid.NewGuid(), Guid.NewGuid(), group.Id, Guid.NewGuid());
        // Status is Pending by default
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll());
        var command = new ChangeSchedulingModeCommand(spaceId, group.Id, Guid.NewGuid(), SchedulingMode.AutoGenerated);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unresolved requests*");
    }

    [Fact]
    public async Task Handle_WithApprovedRequests_ThrowsInvalidOperation()
    {
        // Arrange
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        group.SetSchedulingMode(SchedulingMode.SelfService);
        db.Groups.Add(group);

        var shiftRequest = ShiftRequest.Create(
            spaceId, Guid.NewGuid(), Guid.NewGuid(), group.Id, Guid.NewGuid());
        shiftRequest.Approve();
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll());
        var command = new ChangeSchedulingModeCommand(spaceId, group.Id, Guid.NewGuid(), SchedulingMode.AutoGenerated);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unresolved requests*");
    }

    [Fact]
    public async Task Handle_WithOnlyCancelledRequests_Succeeds()
    {
        // Arrange
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        group.SetSchedulingMode(SchedulingMode.SelfService);
        db.Groups.Add(group);

        var shiftRequest = ShiftRequest.Create(
            spaceId, Guid.NewGuid(), Guid.NewGuid(), group.Id, Guid.NewGuid());
        shiftRequest.Approve();
        shiftRequest.Cancel("No longer needed");
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll());
        var command = new ChangeSchedulingModeCommand(spaceId, group.Id, Guid.NewGuid(), SchedulingMode.AutoGenerated);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updated = await db.Groups.FindAsync(group.Id);
        updated!.SchedulingMode.Should().Be(SchedulingMode.AutoGenerated);
    }

    [Fact]
    public async Task Handle_GroupNotFound_ThrowsKeyNotFound()
    {
        // Arrange
        using var db = CreateDb();
        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll());
        var command = new ChangeSchedulingModeCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SchedulingMode.SelfService);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WithoutPermission_ThrowsUnauthorized()
    {
        // Arrange
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var handler = new ChangeSchedulingModeCommandHandler(db, DenyAll());
        var command = new ChangeSchedulingModeCommand(spaceId, group.Id, Guid.NewGuid(), SchedulingMode.SelfService);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_InactiveGroup_ThrowsInvalidOperation()
    {
        // Arrange
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        group.Deactivate();
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var handler = new ChangeSchedulingModeCommandHandler(db, AllowAll());
        var command = new ChangeSchedulingModeCommand(spaceId, group.Id, Guid.NewGuid(), SchedulingMode.SelfService);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*limited mode*");
    }
}
