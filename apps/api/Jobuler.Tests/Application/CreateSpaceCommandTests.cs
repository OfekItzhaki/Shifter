using FluentAssertions;
using Jobuler.Application.Billing;
using Jobuler.Application.Spaces.Commands;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class CreateSpaceCommandTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (CreateSpaceCommandHandler handler, ITrialDurationCache trialCache, IStatisticsPeriodService statisticsService) CreateHandler(AppDbContext db)
    {
        var trialCache = Substitute.For<ITrialDurationCache>();
        trialCache.GetTrialDaysAsync(Arg.Any<CancellationToken>()).Returns(14);
        var statisticsService = Substitute.For<IStatisticsPeriodService>();
        var handler = new CreateSpaceCommandHandler(db, trialCache, statisticsService);
        return (handler, trialCache, statisticsService);
    }

    [Fact]
    public async Task Handle_CreatesSpaceWithOwnerPermissions()
    {
        var db = CreateDb();
        var (handler, _, _) = CreateHandler(db);
        var userId = Guid.NewGuid();

        var spaceId = await handler.Handle(
            new CreateSpaceCommand("Test Space", null, "he", userId),
            CancellationToken.None);

        var space = await db.Spaces.FindAsync(spaceId);
        space.Should().NotBeNull();
        space!.OwnerUserId.Should().Be(userId);

        var permissions = await db.SpacePermissionGrants
            .Where(g => g.SpaceId == spaceId && g.UserId == userId)
            .ToListAsync();

        permissions.Should().HaveCountGreaterThan(5);
    }

    [Fact]
    public async Task Handle_CreatesMembership()
    {
        var db = CreateDb();
        var (handler, _, _) = CreateHandler(db);
        var userId = Guid.NewGuid();

        var spaceId = await handler.Handle(
            new CreateSpaceCommand("Test Space", null, "he", userId),
            CancellationToken.None);

        var membership = await db.SpaceMemberships
            .FirstOrDefaultAsync(m => m.SpaceId == spaceId && m.UserId == userId);

        membership.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_CreatesTrialSubscription()
    {
        var db = CreateDb();
        var (handler, _, _) = CreateHandler(db);
        var userId = Guid.NewGuid();

        var spaceId = await handler.Handle(
            new CreateSpaceCommand("Test Space", null, "he", userId),
            CancellationToken.None);

        var subscription = await db.SpaceSubscriptions
            .FirstOrDefaultAsync(s => s.SpaceId == spaceId);

        subscription.Should().NotBeNull();
        subscription!.Status.Should().Be(SubscriptionStatus.Trialing);
        subscription.TierId.Should().Be("trial");
    }

    [Fact]
    public async Task Handle_TriggersOnTrialStartedAsync()
    {
        var db = CreateDb();
        var (handler, _, statisticsService) = CreateHandler(db);
        var userId = Guid.NewGuid();

        var spaceId = await handler.Handle(
            new CreateSpaceCommand("Test Space", null, "he", userId),
            CancellationToken.None);

        await statisticsService.Received(1).OnTrialStartedAsync(
            spaceId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UsesTrialDaysFromCache()
    {
        var db = CreateDb();
        var trialCache = Substitute.For<ITrialDurationCache>();
        trialCache.GetTrialDaysAsync(Arg.Any<CancellationToken>()).Returns(30);
        var statisticsService = Substitute.For<IStatisticsPeriodService>();
        var handler = new CreateSpaceCommandHandler(db, trialCache, statisticsService);
        var userId = Guid.NewGuid();

        var spaceId = await handler.Handle(
            new CreateSpaceCommand("Test Space", null, "he", userId),
            CancellationToken.None);

        var subscription = await db.SpaceSubscriptions
            .FirstOrDefaultAsync(s => s.SpaceId == spaceId);

        subscription.Should().NotBeNull();
        var expectedEnd = subscription!.TrialStartsAt.AddDays(30);
        subscription.TrialEndsAt.Should().BeCloseTo(expectedEnd, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_GrantsBillingManagePermission()
    {
        var db = CreateDb();
        var (handler, _, _) = CreateHandler(db);
        var userId = Guid.NewGuid();

        var spaceId = await handler.Handle(
            new CreateSpaceCommand("Test Space", null, "he", userId),
            CancellationToken.None);

        var billingPermission = await db.SpacePermissionGrants
            .FirstOrDefaultAsync(g => g.SpaceId == spaceId && g.UserId == userId
                && g.PermissionKey == "billing.manage");

        billingPermission.Should().NotBeNull();
    }
}
