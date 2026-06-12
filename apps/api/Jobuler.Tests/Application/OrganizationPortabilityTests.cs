using FluentAssertions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jobuler.Application.Auth.Commands;
using Jobuler.Application.Billing.Queries;
using Jobuler.Application.Common;
using Jobuler.Application.Organizations.Commands;
using Jobuler.Application.Organizations.Queries;
using Jobuler.Application.Spaces.Queries;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Logs;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Organizations;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class OrganizationPortabilityTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Jobuler.Domain.Identity.User CreateUser(Guid id, string email = "owner@example.com")
    {
        var user = Jobuler.Domain.Identity.User.Create(email, "Owner", "hash", "en");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(user, id);
        return user;
    }

    private static RegisterCommandHandler CreateRegisterHandler(AppDbContext db)
    {
        var emailSender = Substitute.For<IEmailSender>();
        emailSender.SendAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var config = Substitute.For<IConfiguration>();
        config["App:FrontendBaseUrl"].Returns("https://test.local");

        return new RegisterCommandHandler(
            db,
            emailSender,
            TestContactLookupProtector.Create(),
            NullLogger<RegisterCommandHandler>.Instance,
            config);
    }

    [Fact]
    public async Task Register_CreatesHiddenOrganizationFromCountryAndTemplate_WhenNameIsBlank()
    {
        var db = CreateDb();
        var handler = CreateRegisterHandler(db);

        var userId = await handler.Handle(
            new RegisterCommand(
                Email: "owner@example.com",
                DisplayName: "Owner",
                Password: "Password1!",
                PreferredLocale: "en",
                CountryCode: "il",
                SetupTemplate: "military_style"),
            CancellationToken.None);

        var organization = await db.Organizations.SingleAsync();
        var space = await db.Spaces.SingleAsync();

        organization.PrimaryOwnerUserId.Should().Be(userId);
        organization.CountryCode.Should().Be("IL");
        organization.SetupTemplate.Should().Be("military_style");
        organization.DisplayName.Should().Be("IL Military Style");
        organization.Status.Should().Be(OrganizationStatus.Active);
        space.OrganizationId.Should().Be(organization.Id);
    }

    [Fact]
    public async Task GetMySpaces_HidesRelocatedDisabledOrganizationWithoutDeletingSpace()
    {
        var db = CreateDb();
        var handler = CreateRegisterHandler(db);
        var userId = await handler.Handle(
            new RegisterCommand(
                Email: "owner@example.com",
                DisplayName: "Owner",
                Password: "Password1!",
                PreferredLocale: "en",
                CountryCode: "US",
                SetupTemplate: "restaurant_hospitality",
                OrganizationName: "Pizza Branch"),
            CancellationToken.None);

        var organization = await db.Organizations.SingleAsync();
        var space = await db.Spaces.SingleAsync();
        organization.MarkRelocated("pizza-private-prod", DateTime.UtcNow);
        await db.SaveChangesAsync();

        var spaces = await new GetMySpacesQueryHandler(db)
            .Handle(new GetMySpacesQuery(userId), CancellationToken.None);

        spaces.Should().BeEmpty();
        (await db.Spaces.FindAsync(space.Id)).Should().NotBeNull();
        organization.PurgeEligibleAt.Should().BeCloseTo(
            organization.DisabledAt!.Value.AddDays(90),
            TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SearchOrganizations_FindsCandidatesBySpaceNameAndCountsWholeOrganization()
    {
        var db = CreateDb();
        var owner = Jobuler.Domain.Identity.User.Create(
            "manager@pizza.example",
            "Pizza Manager",
            "hash",
            "en");
        db.Users.Add(owner);

        var organization = Organization.Create(
            "US Restaurant Hospitality",
            owner.Id,
            "US",
            "restaurant_hospitality",
            "en");
        db.Organizations.Add(organization);

        var space = Space.Create("Pizza Hut Haifa", owner.Id, locale: "en", organizationId: organization.Id);
        db.Spaces.Add(space);
        db.SpaceMemberships.Add(SpaceMembership.Create(space.Id, owner.Id));
        db.Groups.Add(Jobuler.Domain.Groups.Group.Create(space.Id, null, "Kitchen"));
        await db.SaveChangesAsync();

        var results = await new SearchOrganizationsQueryHandler(db)
            .Handle(new SearchOrganizationsQuery(Search: "pizza hut"), CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Id.Should().Be(organization.Id);
        results[0].SpaceCount.Should().Be(1);
        results[0].GroupCount.Should().Be(1);
        results[0].MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task MoveSpaceToOrganization_MovesVerifiedSpaceWithItsWholeGroupTree()
    {
        var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var source = Organization.Create("IL General", ownerId, "IL", "general", "he");
        var target = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut North", ownerId, locale: "he", organizationId: source.Id);
        var parent = Jobuler.Domain.Groups.Group.Create(space.Id, null, "Restaurant");
        var child = Jobuler.Domain.Groups.Group.Create(space.Id, null, "Kitchen");
        child.SetParentGroup(parent.Id);

        db.Organizations.AddRange(source, target);
        db.Spaces.Add(space);
        db.Groups.AddRange(parent, child);
        await db.SaveChangesAsync();

        await new MoveSpaceToOrganizationCommandHandler(db)
            .Handle(new MoveSpaceToOrganizationCommand(space.Id, target.Id), CancellationToken.None);

        var moved = await db.Spaces.FindAsync(space.Id);
        moved!.OrganizationId.Should().Be(target.Id);
        (await db.Groups.CountAsync(g => g.SpaceId == space.Id)).Should().Be(2);
    }

    [Fact]
    public async Task OrganizationLifecycle_CanRelocateRestoreAndMarkPurgePendingAfterRetention()
    {
        var db = CreateDb();
        var organization = Organization.Create("IL Military Style", Guid.NewGuid(), "IL", "military_style", "he");
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        await new MarkOrganizationRelocatedCommandHandler(db)
            .Handle(new MarkOrganizationRelocatedCommand(organization.Id, "dedicated-il", RetentionDays: 1), CancellationToken.None);

        organization.Status.Should().Be(OrganizationStatus.RelocatedDisabled);
        organization.PurgeEligibleAt.Should().NotBeNull();

        await new RestoreRelocatedOrganizationCommandHandler(db)
            .Handle(new RestoreRelocatedOrganizationCommand(organization.Id), CancellationToken.None);
        organization.Status.Should().Be(OrganizationStatus.Active);
        organization.PurgeEligibleAt.Should().BeNull();

        organization.MarkRelocated("dedicated-il", DateTime.UtcNow.AddDays(-2), disabledRetentionDays: 1);
        await db.SaveChangesAsync();

        await new MarkOrganizationPurgePendingCommandHandler(db)
            .Handle(new MarkOrganizationPurgePendingCommand(organization.Id), CancellationToken.None);

        organization.Status.Should().Be(OrganizationStatus.PurgePending);
    }

    [Fact]
    public async Task PurgeOrganization_BlocksActiveOrganization()
    {
        var db = CreateDb();
        var organization = Organization.Create("IL Military Style", Guid.NewGuid(), "IL", "military_style", "he");
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        var act = () => new PurgeOrganizationCommandHandler(db)
            .Handle(new PurgeOrganizationCommand(organization.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*purge pending*");
        (await db.Organizations.FindAsync(organization.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task MarkPurgePending_BlocksRelocatedOrganizationBeforeRetentionWindowEnds()
    {
        var db = CreateDb();
        var organization = Organization.Create("IL Military Style", Guid.NewGuid(), "IL", "military_style", "he");
        organization.MarkRelocated("dedicated-il", DateTime.UtcNow, disabledRetentionDays: 90);
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        var act = () => new MarkOrganizationPurgePendingCommandHandler(db)
            .Handle(new MarkOrganizationPurgePendingCommand(organization.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not eligible*");
        organization.Status.Should().Be(OrganizationStatus.RelocatedDisabled);
    }

    [Fact]
    public async Task PurgeOrganization_RemovesOnlyTheEligibleOrganizationAndItsSpaceScopedData()
    {
        var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        var group = Jobuler.Domain.Groups.Group.Create(space.Id, null, "Kitchen");
        var person = Jobuler.Domain.People.Person.Create(space.Id, "Worker 1");

        var outsideOrganization = Organization.Create("Other Org", ownerId, "IL", "general", "he");
        var outsideSpace = Space.Create("Other Space", ownerId, locale: "he", organizationId: outsideOrganization.Id);
        var outsideGroup = Jobuler.Domain.Groups.Group.Create(outsideSpace.Id, null, "Outside");

        organization.MarkRelocated("pizza-private-prod", DateTime.UtcNow.AddDays(-91), disabledRetentionDays: 90);
        organization.MarkPurgePending(DateTime.UtcNow);

        db.Organizations.AddRange(organization, outsideOrganization);
        db.Spaces.AddRange(space, outsideSpace);
        db.Groups.AddRange(group, outsideGroup);
        db.People.Add(person);
        db.GroupMemberships.Add(Jobuler.Domain.Groups.GroupMembership.Create(space.Id, group.Id, person.Id));
        db.SpaceMemberships.Add(SpaceMembership.Create(space.Id, ownerId));
        db.SpaceSubscriptions.Add(SpaceSubscription.CreateTrial(space.Id, trialDays: 14));
        db.OrganizationSubscriptions.Add(OrganizationSubscription.Create(
            organization.Id,
            OrganizationBillingMode.EnterpriseInvoice,
            "enterprise",
            DateTime.UtcNow.AddMonths(-2),
            DateTime.UtcNow.AddMonths(-1),
            autoRenew: false));
        db.AuditLogs.Add(AuditLog.Create(space.Id, ownerId, "organization.purged", "Organization", organization.Id));
        db.AuditLogs.Add(AuditLog.Create(outsideSpace.Id, ownerId, "outside.kept", "Organization", outsideOrganization.Id));
        await db.SaveChangesAsync();

        var result = await new PurgeOrganizationCommandHandler(db)
            .Handle(new PurgeOrganizationCommand(organization.Id), CancellationToken.None);

        result.OrganizationId.Should().Be(organization.Id);
        result.SpaceCount.Should().Be(1);
        result.RemovedTenantScopedRowCount.Should().BeGreaterThan(0);
        result.RemovedAuditLogCount.Should().Be(1);

        (await db.Organizations.FindAsync(organization.Id)).Should().BeNull();
        (await db.Spaces.FindAsync(space.Id)).Should().BeNull();
        (await db.Groups.FindAsync(group.Id)).Should().BeNull();
        (await db.People.FindAsync(person.Id)).Should().BeNull();
        (await db.OrganizationSubscriptions.AnyAsync(s => s.OrganizationId == organization.Id)).Should().BeFalse();
        (await db.AuditLogs.AnyAsync(l => l.SpaceId == space.Id)).Should().BeFalse();

        (await db.Organizations.FindAsync(outsideOrganization.Id)).Should().NotBeNull();
        (await db.Spaces.FindAsync(outsideSpace.Id)).Should().NotBeNull();
        (await db.Groups.FindAsync(outsideGroup.Id)).Should().NotBeNull();
        (await db.AuditLogs.AnyAsync(l => l.SpaceId == outsideSpace.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task OrganizationSubscription_GrantsAccessToCoveredSpaceWithoutSpaceSubscription()
    {
        var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        db.Organizations.Add(organization);
        db.Spaces.Add(space);
        await db.SaveChangesAsync();

        await new SetOrganizationSubscriptionCommandHandler(db)
            .Handle(new SetOrganizationSubscriptionCommand(
                organization.Id,
                OrganizationBillingMode.EnterpriseInvoice,
                "enterprise",
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow.AddMonths(12),
                AutoRenew: true,
                ProviderSubscriptionId: null,
                ProviderCustomerId: null,
                CoveredSpaceLimit: 10,
                CoveredMemberLimit: 500), CancellationToken.None);

        var access = await new GetSpaceBillingAccessHandler(db)
            .Handle(new GetSpaceBillingAccessQuery(space.Id, Guid.NewGuid()), CancellationToken.None);

        access.Should().BeTrue();
    }

    [Fact]
    public async Task OrganizationSubscription_DoesNotGrantAccessAfterRelocationDisablesOrganization()
    {
        var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        db.Organizations.Add(organization);
        db.Spaces.Add(space);
        db.OrganizationSubscriptions.Add(OrganizationSubscription.Create(
            organization.Id,
            OrganizationBillingMode.EnterpriseInvoice,
            "enterprise",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddMonths(12),
            autoRenew: true));
        organization.MarkRelocated("pizza-private-prod", DateTime.UtcNow);
        await db.SaveChangesAsync();

        var access = await new GetSpaceBillingAccessHandler(db)
            .Handle(new GetSpaceBillingAccessQuery(space.Id, Guid.NewGuid()), CancellationToken.None);

        access.Should().BeFalse();
    }

    [Fact]
    public async Task OrganizationSubscription_CanceledExpiredCoverageDoesNotGrantAccess()
    {
        var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        var subscription = OrganizationSubscription.Create(
            organization.Id,
            OrganizationBillingMode.PrivateOnline,
            "private-online",
            DateTime.UtcNow.AddMonths(-2),
            DateTime.UtcNow.AddMonths(-1),
            autoRenew: false,
            providerSubscriptionId: "sub_123",
            providerCustomerId: "cus_123");
        subscription.Cancel();

        db.Organizations.Add(organization);
        db.Spaces.Add(space);
        db.OrganizationSubscriptions.Add(subscription);
        await db.SaveChangesAsync();

        var access = await new GetSpaceBillingAccessHandler(db)
            .Handle(new GetSpaceBillingAccessQuery(space.Id, Guid.NewGuid()), CancellationToken.None);

        access.Should().BeFalse();
    }

    [Fact]
    public async Task SetOrganizationSubscription_UpdatesExistingCoverage()
    {
        var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        var handler = new SetOrganizationSubscriptionCommandHandler(db);
        await handler.Handle(new SetOrganizationSubscriptionCommand(
            organization.Id,
            OrganizationBillingMode.EnterpriseInvoice,
            "enterprise",
            DateTime.UtcNow.AddDays(-1),
            null,
            AutoRenew: false,
            ProviderSubscriptionId: null,
            ProviderCustomerId: null,
            CoveredSpaceLimit: null,
            CoveredMemberLimit: null), CancellationToken.None);

        await handler.Handle(new SetOrganizationSubscriptionCommand(
            organization.Id,
            OrganizationBillingMode.OfflineLicense,
            "offline-license",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddYears(1),
            AutoRenew: false,
            ProviderSubscriptionId: null,
            ProviderCustomerId: null,
            CoveredSpaceLimit: 3,
            CoveredMemberLimit: 200), CancellationToken.None);

        var subscription = await db.OrganizationSubscriptions.SingleAsync();
        subscription.BillingMode.Should().Be(OrganizationBillingMode.OfflineLicense);
        subscription.TierId.Should().Be("offline-license");
        subscription.CoveredSpaceLimit.Should().Be(3);
        subscription.CoveredMemberLimit.Should().Be(200);
    }

    [Fact]
    public async Task ExportManifest_IncludesAllSpacesAndOperationalCountsForOrganization()
    {
        var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var firstSpace = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        var secondSpace = Space.Create("Pizza Hut Tel Aviv", ownerId, locale: "he", organizationId: organization.Id);
        var outsideOrganization = Organization.Create("Other Org", ownerId, "IL", "general", "he");
        var outsideSpace = Space.Create("Other Space", ownerId, locale: "he", organizationId: outsideOrganization.Id);

        db.Organizations.AddRange(organization, outsideOrganization);
        db.Spaces.AddRange(firstSpace, secondSpace, outsideSpace);
        db.SpaceMemberships.AddRange(
            SpaceMembership.Create(firstSpace.Id, ownerId),
            SpaceMembership.Create(secondSpace.Id, ownerId),
            SpaceMembership.Create(outsideSpace.Id, ownerId));
        db.Groups.AddRange(
            Jobuler.Domain.Groups.Group.Create(firstSpace.Id, null, "Kitchen"),
            Jobuler.Domain.Groups.Group.Create(secondSpace.Id, null, "Floor"),
            Jobuler.Domain.Groups.Group.Create(outsideSpace.Id, null, "Outside"));
        db.People.AddRange(
            Jobuler.Domain.People.Person.Create(firstSpace.Id, "Worker 1"),
            Jobuler.Domain.People.Person.Create(secondSpace.Id, "Worker 2"),
            Jobuler.Domain.People.Person.Create(outsideSpace.Id, "Outside Worker"));
        await db.SaveChangesAsync();

        var manifest = await new GetOrganizationExportManifestQueryHandler(db)
            .Handle(new GetOrganizationExportManifestQuery(organization.Id), CancellationToken.None);

        manifest.OrganizationId.Should().Be(organization.Id);
        manifest.Counts.Spaces.Should().Be(2);
        manifest.Counts.Groups.Should().Be(2);
        manifest.Counts.People.Should().Be(2);
        manifest.Counts.SpaceMemberships.Should().Be(2);
        manifest.Spaces.Select(s => s.SpaceId).Should().BeEquivalentTo([firstSpace.Id, secondSpace.Id]);
        manifest.Spaces.Should().NotContain(s => s.SpaceId == outsideSpace.Id);
    }

    [Fact]
    public async Task ExportManifest_WarnsWhenOrganizationBillingMustBeRecreatedInTargetDeployment()
    {
        var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        db.Organizations.Add(organization);
        db.Spaces.Add(space);
        db.OrganizationSubscriptions.Add(OrganizationSubscription.Create(
            organization.Id,
            OrganizationBillingMode.EnterpriseInvoice,
            "enterprise",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddYears(1),
            autoRenew: true,
            providerSubscriptionId: "sub_sensitive",
            providerCustomerId: "cus_sensitive"));
        await db.SaveChangesAsync();

        var manifest = await new GetOrganizationExportManifestQueryHandler(db)
            .Handle(new GetOrganizationExportManifestQuery(organization.Id), CancellationToken.None);

        manifest.Warnings.Should().Contain(w => w.Contains("recreate billing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExportPackage_IncludesManifestAndScopedDataOnly_AndImportsSelfServiceGraph()
    {
        var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var owner = CreateUser(ownerId);
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        var outsideOrganization = Organization.Create("Other Org", ownerId, "IL", "general", "he");
        var outsideSpace = Space.Create("Other Space", ownerId, locale: "he", organizationId: outsideOrganization.Id);
        var group = Group.Create(space.Id, null, "Kitchen");
        var outsideGroup = Group.Create(outsideSpace.Id, null, "Outside");
        var person = Person.Create(space.Id, "Worker 1");
        var targetPerson = Person.Create(space.Id, "Worker 2");
        var outsidePerson = Person.Create(outsideSpace.Id, "Outside Worker");
        var cycleStart = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var groupTask = GroupTask.Create(
            space.Id,
            group.Id,
            "Kitchen shift",
            cycleStart,
            cycleStart.AddDays(7),
            shiftDurationMinutes: 240,
            requiredHeadcount: 1,
            TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            ownerId);
        var organizationDefaults = OrganizationSelfServiceDefaults.Create(
            organization.Id,
            minShiftsPerCycle: 1,
            maxShiftsPerCycle: 5,
            requestWindowOpenOffsetHours: 168,
            requestWindowCloseOffsetHours: 24,
            cancellationCutoffHours: 24,
            maxAbsencesPerCycle: 2,
            maxLateCancellationsPerCycle: 1,
            lateCancellationWindowHours: 24,
            waitlistOfferMinutes: 60,
            cycleDurationDays: 7,
            allowMemberShiftClaims: true,
            allowWaitlist: true,
            allowShiftChangeRequests: true,
            allowAbsenceReports: true,
            allowShiftSwaps: false);
        var defaults = SpaceSelfServiceDefaults.Create(
            space.Id,
            minShiftsPerCycle: 1,
            maxShiftsPerCycle: 4,
            requestWindowOpenOffsetHours: 168,
            requestWindowCloseOffsetHours: 24,
            cancellationCutoffHours: 24,
            maxAbsencesPerCycle: 2,
            maxLateCancellationsPerCycle: 1,
            lateCancellationWindowHours: 24,
            waitlistOfferMinutes: 60,
            cycleDurationDays: 7,
            allowMemberShiftClaims: true,
            allowWaitlist: true,
            allowShiftChangeRequests: true,
            allowAbsenceReports: true,
            allowShiftSwaps: true);
        var specialDay = SpaceSpecialDay.Create(
            space.Id,
            DateOnly.FromDateTime(cycleStart),
            "Holiday",
            SpaceSpecialDayKind.Holiday,
            homeLeaveWeightMultiplier: 1.5m,
            requiresCoverage: true);
        var config = SelfServiceConfig.Create(space.Id, group.Id);
        var outsideConfig = SelfServiceConfig.Create(outsideSpace.Id, outsideGroup.Id);
        var cycle = SchedulingCycle.Create(
            space.Id,
            group.Id,
            cycleStart,
            cycleStart.AddDays(7),
            cycleStart.AddDays(-7),
            cycleStart.AddDays(-1));
        var template = ShiftTemplate.Create(
            space.Id,
            group.Id,
            groupTask.Id,
            DayOfWeek.Monday,
            new TimeOnly(8, 0),
            new TimeOnly(12, 0),
            requiredHeadcount: 1,
            createdByUserId: ownerId);
        var slot = ShiftSlot.Create(
            space.Id,
            group.Id,
            groupTask.Id,
            template.Id,
            cycle.Id,
            DateOnly.FromDateTime(cycleStart),
            new TimeOnly(8, 0),
            new TimeOnly(12, 0),
            capacity: 1);
        var shiftRequest = ShiftRequest.Create(space.Id, slot.Id, person.Id, group.Id, cycle.Id);
        shiftRequest.Approve(ownerId);
        var targetShiftRequest = ShiftRequest.Create(space.Id, slot.Id, targetPerson.Id, group.Id, cycle.Id);
        targetShiftRequest.Approve(ownerId);
        var attendance = ShiftAttendanceRecord.Create(
            space.Id,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            slot.Id,
            person.Id,
            ShiftAttendanceStatus.Present,
            ownerId,
            "Arrived");
        var absence = ShiftAbsenceReport.Create(
            space.Id,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            slot.Id,
            person.Id,
            "Sick",
            isLate: false,
            reportedAt: cycleStart.AddDays(-2));
        var change = ShiftChangeRequest.Create(
            space.Id,
            group.Id,
            cycle.Id,
            shiftRequest.Id,
            slot.Id,
            requestedShiftSlotId: null,
            person.Id,
            "Need a different slot",
            cycleStart.AddDays(-3));
        var waitlist = WaitlistEntry.Create(space.Id, slot.Id, person.Id, position: 1);
        var swap = SwapRequest.Create(
            space.Id,
            group.Id,
            person.Id,
            targetPerson.Id,
            shiftRequest.Id,
            targetShiftRequest.Id);
        var specialLeave = SpecialLeaveRequest.Create(
            space.Id,
            person.Id,
            cycleStart.AddDays(1),
            cycleStart.AddDays(2),
            "Family event",
            ownerId);
        var notification = Notification.Create(
            space.Id,
            ownerId,
            "self_service.request_approved",
            "Shift approved",
            "Your shift was approved.",
            metadataJson: $$"""{"shiftRequestId":"{{shiftRequest.Id}}"}""");
        var outsideNotification = Notification.Create(
            outsideSpace.Id,
            ownerId,
            "outside",
            "Outside",
            "Outside notification");
        var auditLog = AuditLog.Create(
            space.Id,
            ownerId,
            "self_service.request_approved",
            "ShiftRequest",
            shiftRequest.Id);
        var outsideAuditLog = AuditLog.Create(
            outsideSpace.Id,
            ownerId,
            "outside.kept",
            "Space",
            outsideSpace.Id);

        db.Users.Add(owner);
        db.Organizations.AddRange(organization, outsideOrganization);
        db.Spaces.AddRange(space, outsideSpace);
        db.Groups.AddRange(group, outsideGroup);
        db.People.AddRange(person, targetPerson, outsidePerson);
        db.GroupTasks.Add(groupTask);
        db.OrganizationSelfServiceDefaults.Add(organizationDefaults);
        db.SpaceSelfServiceDefaults.Add(defaults);
        db.SpaceSpecialDays.Add(specialDay);
        db.SelfServiceConfigs.AddRange(config, outsideConfig);
        db.SchedulingCycles.Add(cycle);
        db.ShiftTemplates.Add(template);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.AddRange(shiftRequest, targetShiftRequest);
        db.ShiftAttendanceRecords.Add(attendance);
        db.ShiftAbsenceReports.Add(absence);
        db.ShiftChangeRequests.Add(change);
        db.WaitlistEntries.Add(waitlist);
        db.SwapRequests.Add(swap);
        db.SpecialLeaveRequests.Add(specialLeave);
        db.Notifications.AddRange(notification, outsideNotification);
        db.AuditLogs.AddRange(auditLog, outsideAuditLog);
        await db.SaveChangesAsync();

        var mediator = Substitute.For<MediatR.IMediator>();
        mediator.Send(Arg.Any<GetOrganizationExportManifestQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new GetOrganizationExportManifestQueryHandler(db)
                .Handle(callInfo.Arg<GetOrganizationExportManifestQuery>(), callInfo.Arg<CancellationToken>()));

        var result = await new ExportOrganizationPackageCommandHandler(db, mediator)
            .Handle(new ExportOrganizationPackageCommand(organization.Id), CancellationToken.None);

        result.FileName.Should().Contain(organization.Id.ToString("N"));

        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("manifest").GetProperty("organizationId").GetGuid().Should().Be(organization.Id);
        var manifestCounts = root.GetProperty("manifest").GetProperty("counts");
        manifestCounts.GetProperty("organizationSelfServiceDefaults").GetInt32().Should().Be(1);
        manifestCounts.GetProperty("shiftAttendanceRecords").GetInt32().Should().Be(1);
        manifestCounts.GetProperty("shiftAbsenceReports").GetInt32().Should().Be(1);
        manifestCounts.GetProperty("shiftChangeRequests").GetInt32().Should().Be(1);
        manifestCounts.GetProperty("waitlistEntries").GetInt32().Should().Be(1);
        manifestCounts.GetProperty("swapRequests").GetInt32().Should().Be(1);
        manifestCounts.GetProperty("specialLeaveRequests").GetInt32().Should().Be(1);

        var spaces = root.GetProperty("data").GetProperty("spaces").EnumerateArray().ToList();
        spaces.Should().ContainSingle();
        spaces[0].GetProperty("id").GetGuid().Should().Be(space.Id);

        var groups = root.GetProperty("data").GetProperty("groups").EnumerateArray().ToList();
        groups.Should().ContainSingle();
        groups[0].GetProperty("spaceId").GetGuid().Should().Be(space.Id);

        var data = root.GetProperty("data");
        data.GetProperty("users").EnumerateArray().Should().ContainSingle(user =>
            user.GetProperty("id").GetGuid() == ownerId);
        data.GetProperty("organizationSelfServiceDefaults").EnumerateArray().Should().ContainSingle();
        data.GetProperty("spaceSelfServiceDefaults").EnumerateArray().Should().ContainSingle();
        data.GetProperty("spaceSpecialDays").EnumerateArray().Should().ContainSingle();
        data.GetProperty("selfServiceConfigs").EnumerateArray().Should().ContainSingle();
        data.GetProperty("selfServiceConfigs").EnumerateArray().Single().GetProperty("spaceId").GetGuid().Should().Be(space.Id);
        data.GetProperty("schedulingCycles").EnumerateArray().Should().ContainSingle();
        data.GetProperty("shiftTemplates").EnumerateArray().Should().ContainSingle();
        data.GetProperty("shiftSlots").EnumerateArray().Should().ContainSingle();
        data.GetProperty("shiftRequests").EnumerateArray().Should().HaveCount(2);
        data.GetProperty("shiftAttendanceRecords").EnumerateArray().Should().ContainSingle();
        data.GetProperty("shiftAbsenceReports").EnumerateArray().Should().ContainSingle();
        data.GetProperty("shiftChangeRequests").EnumerateArray().Should().ContainSingle();
        data.GetProperty("waitlistEntries").EnumerateArray().Should().ContainSingle();
        data.GetProperty("swapRequests").EnumerateArray().Should().ContainSingle();
        data.GetProperty("specialLeaveRequests").EnumerateArray().Should().ContainSingle();
        data.GetProperty("notifications").EnumerateArray().Should().ContainSingle();
        data.GetProperty("auditLogs").EnumerateArray().Should().ContainSingle();

        var importValidation = await new ValidateOrganizationImportPackageCommandHandler(CreateDb())
            .Handle(new ValidateOrganizationImportPackageCommand(
                System.Text.Encoding.UTF8.GetString(result.Content)), CancellationToken.None);
        importValidation.IsImportSafe.Should().BeTrue();
        importValidation.Counts.OrganizationSelfServiceDefaults.Should().Be(1);
        importValidation.Counts.ShiftAttendanceRecords.Should().Be(1);
        importValidation.Counts.ShiftAbsenceReports.Should().Be(1);
        importValidation.Counts.ShiftChangeRequests.Should().Be(1);
        importValidation.Counts.WaitlistEntries.Should().Be(1);
        importValidation.Counts.SwapRequests.Should().Be(1);
        importValidation.Counts.SpecialLeaveRequests.Should().Be(1);

        var targetDb = CreateDb();
        var importResult = await new ImportOrganizationPackageCommandHandler(targetDb)
            .Handle(new ImportOrganizationPackageCommand(
                System.Text.Encoding.UTF8.GetString(result.Content),
                ConfirmImport: true), CancellationToken.None);

        importResult.OrganizationId.Should().Be(organization.Id);
        importResult.Counts.Should().Be(importValidation.Counts);

        (await targetDb.OrganizationSelfServiceDefaults.FindAsync(organizationDefaults.Id)).Should().NotBeNull();
        (await targetDb.SpaceSelfServiceDefaults.FindAsync(defaults.Id)).Should().NotBeNull();
        (await targetDb.SpaceSpecialDays.FindAsync(specialDay.Id)).Should().NotBeNull();
        (await targetDb.SelfServiceConfigs.FindAsync(config.Id)).Should().NotBeNull();
        (await targetDb.SchedulingCycles.FindAsync(cycle.Id)).Should().NotBeNull();
        (await targetDb.ShiftTemplates.FindAsync(template.Id)).Should().NotBeNull();
        (await targetDb.ShiftSlots.FindAsync(slot.Id)).Should().NotBeNull();
        (await targetDb.ShiftRequests.FindAsync(shiftRequest.Id)).Should().NotBeNull();
        (await targetDb.ShiftRequests.FindAsync(targetShiftRequest.Id)).Should().NotBeNull();
        (await targetDb.ShiftAttendanceRecords.FindAsync(attendance.Id)).Should().NotBeNull();
        (await targetDb.ShiftAbsenceReports.FindAsync(absence.Id)).Should().NotBeNull();
        (await targetDb.ShiftChangeRequests.FindAsync(change.Id)).Should().NotBeNull();
        (await targetDb.WaitlistEntries.FindAsync(waitlist.Id)).Should().NotBeNull();
        (await targetDb.SwapRequests.FindAsync(swap.Id)).Should().NotBeNull();
        (await targetDb.SpecialLeaveRequests.FindAsync(specialLeave.Id)).Should().NotBeNull();
        (await targetDb.Notifications.FindAsync(notification.Id)).Should().NotBeNull();
        (await targetDb.AuditLogs.FindAsync(auditLog.Id)).Should().NotBeNull();

        (await targetDb.SelfServiceConfigs.AnyAsync(c => c.Id == outsideConfig.Id)).Should().BeFalse();
        (await targetDb.Notifications.AnyAsync(n => n.Id == outsideNotification.Id)).Should().BeFalse();
        (await targetDb.AuditLogs.AnyAsync(a => a.Id == outsideAuditLog.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ValidateImportPackage_ReturnsSafeForEmptyTargetDeployment()
    {
        var sourceDb = CreateDb();
        var ownerId = Guid.NewGuid();
        var owner = CreateUser(ownerId);
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        sourceDb.Users.Add(owner);
        sourceDb.Organizations.Add(organization);
        sourceDb.Spaces.Add(space);
        sourceDb.Groups.Add(Jobuler.Domain.Groups.Group.Create(space.Id, null, "Kitchen"));
        await sourceDb.SaveChangesAsync();

        var package = await ExportPackageAsync(sourceDb, organization.Id);
        var targetDb = CreateDb();

        var result = await new ValidateOrganizationImportPackageCommandHandler(targetDb)
            .Handle(new ValidateOrganizationImportPackageCommand(
                System.Text.Encoding.UTF8.GetString(package.Content)), CancellationToken.None);

        result.IsImportSafe.Should().BeTrue();
        result.OrganizationId.Should().Be(organization.Id);
        result.Counts.Spaces.Should().Be(1);
        result.Counts.Groups.Should().Be(1);
        result.Errors.Should().BeEmpty();
        result.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateImportPackage_DetectsExistingTargetIds()
    {
        var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var owner = CreateUser(ownerId);
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        var specialDay = SpaceSpecialDay.Create(
            space.Id,
            new DateOnly(2026, 9, 21),
            "Yom Kippur",
            SpaceSpecialDayKind.Holiday,
            homeLeaveWeightMultiplier: 3m,
            requiresCoverage: true);
        var notification = Notification.Create(
            space.Id,
            ownerId,
            "self_service.calendar_ready",
            "Calendar ready",
            "Holiday calendar was imported.");
        var auditLog = AuditLog.Create(
            space.Id,
            ownerId,
            "space.special_day.imported",
            "SpaceSpecialDay",
            specialDay.Id);
        db.Users.Add(owner);
        db.Organizations.Add(organization);
        db.Spaces.Add(space);
        db.SpaceSpecialDays.Add(specialDay);
        db.Notifications.Add(notification);
        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync();

        var package = await ExportPackageAsync(db, organization.Id);

        var result = await new ValidateOrganizationImportPackageCommandHandler(db)
            .Handle(new ValidateOrganizationImportPackageCommand(
                System.Text.Encoding.UTF8.GetString(package.Content)), CancellationToken.None);

        result.IsImportSafe.Should().BeFalse();
        result.Conflicts.Should().Contain(c => c.Contains("Organization id already exists"));
        result.Conflicts.Should().Contain(c => c.Contains("space id"));
        result.Conflicts.Should().Contain(c => c.Contains("space special day id"));
        result.Conflicts.Should().Contain(c => c.Contains("notification id"));
        result.Conflicts.Should().Contain(c => c.Contains("audit log id"));
    }

    [Fact]
    public async Task ValidateImportPackage_RejectsRowsOutsideExportedSpaces()
    {
        var sourceDb = CreateDb();
        var ownerId = Guid.NewGuid();
        var owner = CreateUser(ownerId);
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        sourceDb.Users.Add(owner);
        sourceDb.Organizations.Add(organization);
        sourceDb.Spaces.Add(space);
        sourceDb.Groups.Add(Group.Create(space.Id, null, "Kitchen"));
        await sourceDb.SaveChangesAsync();

        var package = await ExportPackageAsync(sourceDb, organization.Id);
        var packageJson = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(package.Content))!;
        var groups = packageJson["data"]!["groups"]!.AsArray();
        groups.Add(new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["spaceId"] = Guid.NewGuid().ToString(),
            ["name"] = "Leaked group"
        });

        var result = await new ValidateOrganizationImportPackageCommandHandler(CreateDb())
            .Handle(new ValidateOrganizationImportPackageCommand(packageJson.ToJsonString()), CancellationToken.None);

        result.IsImportSafe.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("groups contains 1 row(s) outside the exported spaces"));
    }

    [Fact]
    public async Task ValidateImportPackage_RejectsBrokenCoreReferences()
    {
        var sourceDb = CreateDb();
        var ownerId = Guid.NewGuid();
        var owner = CreateUser(ownerId);
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        sourceDb.Users.Add(owner);
        sourceDb.Organizations.Add(organization);
        sourceDb.Spaces.Add(space);
        await sourceDb.SaveChangesAsync();

        var package = await ExportPackageAsync(sourceDb, organization.Id);
        var packageJson = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(package.Content))!;
        packageJson["data"]!["spaces"]!.AsArray()[0]!["ownerUserId"] = Guid.NewGuid().ToString();

        var result = await new ValidateOrganizationImportPackageCommandHandler(CreateDb())
            .Handle(new ValidateOrganizationImportPackageCommand(packageJson.ToJsonString()), CancellationToken.None);

        result.IsImportSafe.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.Contains("spaces contains 1 row(s) whose ownerUserId is not in exported users"));
    }

    [Fact]
    public async Task ValidateImportPackage_RejectsBrokenSelfServiceReferences()
    {
        var sourceDb = CreateDb();
        var ownerId = Guid.NewGuid();
        var owner = CreateUser(ownerId);
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        var group = Group.Create(space.Id, null, "Kitchen");
        var person = Person.Create(space.Id, "Worker 1");
        var cycleStart = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var groupTask = GroupTask.Create(
            space.Id,
            group.Id,
            "Kitchen shift",
            cycleStart,
            cycleStart.AddDays(7),
            shiftDurationMinutes: 240,
            requiredHeadcount: 1,
            TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            ownerId);
        var cycle = SchedulingCycle.Create(
            space.Id,
            group.Id,
            cycleStart,
            cycleStart.AddDays(7),
            cycleStart.AddDays(-7),
            cycleStart.AddDays(-1));
        var template = ShiftTemplate.Create(
            space.Id,
            group.Id,
            groupTask.Id,
            DayOfWeek.Monday,
            new TimeOnly(8, 0),
            new TimeOnly(12, 0),
            requiredHeadcount: 1,
            createdByUserId: ownerId);
        var slot = ShiftSlot.Create(
            space.Id,
            group.Id,
            groupTask.Id,
            template.Id,
            cycle.Id,
            DateOnly.FromDateTime(cycleStart),
            new TimeOnly(8, 0),
            new TimeOnly(12, 0),
            capacity: 1);
        var request = ShiftRequest.Create(space.Id, slot.Id, person.Id, group.Id, cycle.Id);

        sourceDb.Users.Add(owner);
        sourceDb.Organizations.Add(organization);
        sourceDb.Spaces.Add(space);
        sourceDb.Groups.Add(group);
        sourceDb.People.Add(person);
        sourceDb.GroupTasks.Add(groupTask);
        sourceDb.SchedulingCycles.Add(cycle);
        sourceDb.ShiftTemplates.Add(template);
        sourceDb.ShiftSlots.Add(slot);
        sourceDb.ShiftRequests.Add(request);
        await sourceDb.SaveChangesAsync();

        var package = await ExportPackageAsync(sourceDb, organization.Id);
        var packageJson = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(package.Content))!;
        packageJson["data"]!["shiftRequests"]!.AsArray()[0]!["groupId"] = Guid.NewGuid().ToString();

        var result = await new ValidateOrganizationImportPackageCommandHandler(CreateDb())
            .Handle(new ValidateOrganizationImportPackageCommand(packageJson.ToJsonString()), CancellationToken.None);

        result.IsImportSafe.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.Contains("shiftRequests contains 1 row(s) whose groupId is not in exported groups"));
    }

    [Fact]
    public async Task ImportOrganizationPackage_InsertsValidatedPackageIntoEmptyTarget()
    {
        var sourceDb = CreateDb();
        var ownerId = Guid.NewGuid();
        var owner = CreateUser(ownerId);
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        var group = Group.Create(space.Id, null, "Kitchen", createdByUserId: ownerId);
        var person = Person.Create(space.Id, "Worker 1", linkedUserId: ownerId);

        sourceDb.Users.Add(owner);
        sourceDb.Organizations.Add(organization);
        sourceDb.Spaces.Add(space);
        sourceDb.SpaceMemberships.Add(SpaceMembership.Create(space.Id, ownerId));
        sourceDb.Groups.Add(group);
        sourceDb.People.Add(person);
        sourceDb.GroupMemberships.Add(GroupMembership.Create(space.Id, group.Id, person.Id));
        await sourceDb.SaveChangesAsync();

        var package = await ExportPackageAsync(sourceDb, organization.Id);
        var packageJson = System.Text.Encoding.UTF8.GetString(package.Content);
        var targetDb = CreateDb();

        var result = await new ImportOrganizationPackageCommandHandler(targetDb)
            .Handle(new ImportOrganizationPackageCommand(packageJson, ConfirmImport: true), CancellationToken.None);

        result.OrganizationId.Should().Be(organization.Id);
        result.Counts.Spaces.Should().Be(1);
        result.Counts.Groups.Should().Be(1);
        result.Counts.People.Should().Be(1);

        (await targetDb.Organizations.FindAsync(organization.Id)).Should().NotBeNull();
        (await targetDb.Spaces.FindAsync(space.Id)).Should().NotBeNull();
        (await targetDb.Groups.FindAsync(group.Id)).Should().NotBeNull();
        (await targetDb.People.FindAsync(person.Id)).Should().NotBeNull();

        var importedUser = await targetDb.Users.FindAsync(ownerId);
        importedUser.Should().NotBeNull();
        importedUser!.PasswordHash.Should().NotBeNullOrWhiteSpace();
        importedUser.PasswordHash.Should().NotBe(owner.PasswordHash);
    }

    [Fact]
    public async Task ImportOrganizationPackage_RequiresExplicitConfirmation()
    {
        var sourceDb = CreateDb();
        var ownerId = Guid.NewGuid();
        var owner = CreateUser(ownerId);
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        sourceDb.Users.Add(owner);
        sourceDb.Organizations.Add(organization);
        sourceDb.Spaces.Add(space);
        await sourceDb.SaveChangesAsync();

        var package = await ExportPackageAsync(sourceDb, organization.Id);
        var packageJson = System.Text.Encoding.UTF8.GetString(package.Content);
        var targetDb = CreateDb();

        var act = () => new ImportOrganizationPackageCommandHandler(targetDb)
            .Handle(new ImportOrganizationPackageCommand(packageJson, ConfirmImport: false), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*confirmImport*");
        (await targetDb.Organizations.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ImportOrganizationPackage_BlocksUnsafePackageWithoutWriting()
    {
        var sourceDb = CreateDb();
        var ownerId = Guid.NewGuid();
        var owner = CreateUser(ownerId);
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        sourceDb.Users.Add(owner);
        sourceDb.Organizations.Add(organization);
        sourceDb.Spaces.Add(space);
        await sourceDb.SaveChangesAsync();

        var package = await ExportPackageAsync(sourceDb, organization.Id);
        var packageJson = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(package.Content))!;
        packageJson["data"]!["spaces"]!.AsArray()[0]!["ownerUserId"] = Guid.NewGuid().ToString();
        var targetDb = CreateDb();

        var act = () => new ImportOrganizationPackageCommandHandler(targetDb)
            .Handle(new ImportOrganizationPackageCommand(packageJson.ToJsonString(), ConfirmImport: true), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not safe to import*");
        (await targetDb.Organizations.AnyAsync()).Should().BeFalse();
        (await targetDb.Spaces.AnyAsync()).Should().BeFalse();
    }

    private static Task<OrganizationExportPackageResult> ExportPackageAsync(AppDbContext db, Guid organizationId)
    {
        var mediator = Substitute.For<MediatR.IMediator>();
        mediator.Send(Arg.Any<GetOrganizationExportManifestQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new GetOrganizationExportManifestQueryHandler(db)
                .Handle(callInfo.Arg<GetOrganizationExportManifestQuery>(), callInfo.Arg<CancellationToken>()));

        return new ExportOrganizationPackageCommandHandler(db, mediator)
            .Handle(new ExportOrganizationPackageCommand(organizationId), CancellationToken.None);
    }
}
