using FluentAssertions;
using System.Text.Json;
using Jobuler.Application.Auth.Commands;
using Jobuler.Application.Billing.Queries;
using Jobuler.Application.Common;
using Jobuler.Application.Organizations.Commands;
using Jobuler.Application.Organizations.Queries;
using Jobuler.Application.Spaces.Queries;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Logs;
using Jobuler.Domain.Organizations;
using Jobuler.Domain.Spaces;
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
    public async Task ExportPackage_IncludesManifestAndScopedDataOnly()
    {
        var db = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        var outsideOrganization = Organization.Create("Other Org", ownerId, "IL", "general", "he");
        var outsideSpace = Space.Create("Other Space", ownerId, locale: "he", organizationId: outsideOrganization.Id);

        db.Organizations.AddRange(organization, outsideOrganization);
        db.Spaces.AddRange(space, outsideSpace);
        db.Groups.AddRange(
            Jobuler.Domain.Groups.Group.Create(space.Id, null, "Kitchen"),
            Jobuler.Domain.Groups.Group.Create(outsideSpace.Id, null, "Outside"));
        db.People.AddRange(
            Jobuler.Domain.People.Person.Create(space.Id, "Worker 1"),
            Jobuler.Domain.People.Person.Create(outsideSpace.Id, "Outside Worker"));
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

        var spaces = root.GetProperty("data").GetProperty("spaces").EnumerateArray().ToList();
        spaces.Should().ContainSingle();
        spaces[0].GetProperty("id").GetGuid().Should().Be(space.Id);

        var groups = root.GetProperty("data").GetProperty("groups").EnumerateArray().ToList();
        groups.Should().ContainSingle();
        groups[0].GetProperty("spaceId").GetGuid().Should().Be(space.Id);
    }

    [Fact]
    public async Task ValidateImportPackage_ReturnsSafeForEmptyTargetDeployment()
    {
        var sourceDb = CreateDb();
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
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
        var organization = Organization.Create("Pizza Hut Israel", ownerId, "IL", "restaurant_hospitality", "he");
        var space = Space.Create("Pizza Hut Haifa", ownerId, locale: "he", organizationId: organization.Id);
        db.Organizations.Add(organization);
        db.Spaces.Add(space);
        await db.SaveChangesAsync();

        var package = await ExportPackageAsync(db, organization.Id);

        var result = await new ValidateOrganizationImportPackageCommandHandler(db)
            .Handle(new ValidateOrganizationImportPackageCommand(
                System.Text.Encoding.UTF8.GetString(package.Content)), CancellationToken.None);

        result.IsImportSafe.Should().BeFalse();
        result.Conflicts.Should().Contain(c => c.Contains("Organization id already exists"));
        result.Conflicts.Should().Contain(c => c.Contains("space id"));
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
