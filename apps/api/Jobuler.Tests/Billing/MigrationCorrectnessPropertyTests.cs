// Feature: space-billing, Property 14: Migration creates correct space subscriptions
// Validates: Requirements 8.1, 8.2, 8.3, 8.4

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Billing;
using Jobuler.Application.Billing.Commands;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Jobuler.Tests.Billing;

public class MigrationCorrectnessPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static ITrialDurationCache CreateMockTrialCache(int trialDays = 14)
    {
        var cache = Substitute.For<ITrialDurationCache>();
        cache.GetTrialDaysAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(trialDays));
        return cache;
    }

    private static MigrateToSpaceBillingCommandHandler CreateHandler(AppDbContext db, ITrialDurationCache cache)
    {
        var logger = NullLogger<MigrateToSpaceBillingCommandHandler>.Instance;
        return new MigrateToSpaceBillingCommandHandler(db, cache, logger);
    }

    /// <summary>
    /// Creates a GroupSubscription with the given status for a space.
    /// Uses reflection to set private properties since the entity uses private setters.
    /// </summary>
    private static GroupSubscription CreateGroupSubscription(
        Guid spaceId,
        Guid groupId,
        SubscriptionStatus status,
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        string? lsSubId = null,
        string? lsCustId = null,
        string tierId = "pro")
    {
        var gs = GroupSubscription.CreateTrial(spaceId, groupId, 14);

        // Transition to the desired status
        switch (status)
        {
            case SubscriptionStatus.Active:
                gs.Activate(
                    tierId,
                    lsSubId ?? $"ls_sub_{Guid.NewGuid():N}",
                    lsCustId ?? $"ls_cust_{Guid.NewGuid():N}",
                    periodStart ?? DateTime.UtcNow.AddDays(-15),
                    periodEnd ?? DateTime.UtcNow.AddDays(15));
                break;
            case SubscriptionStatus.Trialing:
                // Already trialing from CreateTrial
                break;
            case SubscriptionStatus.Canceled:
                gs.Activate(tierId, lsSubId ?? "ls_sub_x", lsCustId ?? "ls_cust_x",
                    periodStart ?? DateTime.UtcNow.AddDays(-15),
                    periodEnd ?? DateTime.UtcNow.AddDays(15));
                gs.Cancel();
                break;
            case SubscriptionStatus.Expired:
                gs.Activate(tierId, lsSubId ?? "ls_sub_x", lsCustId ?? "ls_cust_x",
                    periodStart ?? DateTime.UtcNow.AddDays(-30),
                    periodEnd ?? DateTime.UtcNow.AddDays(-1));
                gs.Cancel();
                gs.Expire();
                break;
            case SubscriptionStatus.Migrated:
                gs.UpdateStatus(SubscriptionStatus.Migrated);
                break;
        }

        return gs;
    }

    // ── Generators ───────────────────────────────────────────────────────────

    /// <summary>
    /// Represents a space scenario for migration testing.
    /// </summary>
    public record SpaceScenario(
        Guid SpaceId,
        List<(SubscriptionStatus Status, DateTime? PeriodStart, DateTime? PeriodEnd, string TierId, string? LsSubId, string? LsCustId)> GroupSubs,
        bool HasExistingSpaceSubscription);

    /// <summary>
    /// Generates a list of 1-5 space scenarios with varying GroupSubscription states.
    /// </summary>
    private static Arbitrary<List<SpaceScenario>> SpaceScenariosArbitrary()
    {
        var statusGen = Gen.Elements(
            SubscriptionStatus.Active,
            SubscriptionStatus.Trialing,
            SubscriptionStatus.Canceled,
            SubscriptionStatus.Expired);

        var periodEndGen = from offsetDays in Gen.Choose(1, 60)
                           select DateTime.UtcNow.AddDays(offsetDays);

        var periodStartGen = from offsetDays in Gen.Choose(1, 30)
                             select DateTime.UtcNow.AddDays(-offsetDays);

        var tierGen = Gen.Elements("pro", "business", "enterprise");

        var groupSubGen = from status in statusGen
                          from periodStart in periodStartGen
                          from periodEnd in periodEndGen
                          from tier in tierGen
                          select (status, (DateTime?)periodStart, (DateTime?)periodEnd, tier, (string?)null, (string?)null);

        var groupSubsGen = from count in Gen.Choose(1, 4)
                           from subs in Gen.ListOf(count, groupSubGen)
                           select subs.ToList();

        var hasExistingGen = Gen.Elements(true, false);

        var scenarioGen = from groupSubs in groupSubsGen
                          from hasExisting in hasExistingGen
                          select new SpaceScenario(Guid.NewGuid(), groupSubs, hasExisting);

        var scenariosGen = from count in Gen.Choose(1, 5)
                           from scenarios in Gen.ListOf(count, scenarioGen)
                           select scenarios.ToList();

        return Arb.From(scenariosGen);
    }

    // ── Property 14a: All GroupSubscriptions are marked as Migrated ──────────
    // **Validates: Requirements 8.1**

    [Property(MaxTest = 100)]
    public Property Migration_MarksAllGroupSubscriptionsAsMigrated()
    {
        return Prop.ForAll(SpaceScenariosArbitrary(), async (scenarios) =>
        {
            using var db = CreateInMemoryDb();
            var cache = CreateMockTrialCache();
            var handler = CreateHandler(db, cache);

            // Seed group subscriptions (exclude scenarios with existing space subs for this property)
            var allGroupSubIds = new List<Guid>();
            foreach (var scenario in scenarios)
            {
                foreach (var (status, periodStart, periodEnd, tier, lsSubId, lsCustId) in scenario.GroupSubs)
                {
                    var groupId = Guid.NewGuid();
                    var gs = CreateGroupSubscription(scenario.SpaceId, groupId, status, periodStart, periodEnd, lsSubId, lsCustId, tier);
                    db.GroupSubscriptions.Add(gs);
                    allGroupSubIds.Add(gs.Id);
                }
            }

            await db.SaveChangesAsync();

            // Act
            await handler.Handle(new MigrateToSpaceBillingCommand(100), CancellationToken.None);

            // Assert: all group subscriptions should be Migrated
            var groupSubs = await db.GroupSubscriptions
                .Where(gs => allGroupSubIds.Contains(gs.Id))
                .ToListAsync();

            groupSubs.Should().AllSatisfy(gs =>
            {
                gs.Status.Should().Be(SubscriptionStatus.Migrated,
                    $"GroupSubscription {gs.Id} for space {gs.SpaceId} should be marked Migrated");
            });
        });
    }

    // ── Property 14b: Spaces with active/trialing group subs get Active SpaceSubscription ──
    // **Validates: Requirements 8.2**

    [Property(MaxTest = 100)]
    public Property Migration_ActiveGroupSubs_CreateActiveSpaceSubscriptionWithLatestPeriod()
    {
        // Generate spaces that have at least one active or trialing group subscription
        var statusGen = Gen.Elements(SubscriptionStatus.Active, SubscriptionStatus.Trialing);

        var periodEndGen = from offsetDays in Gen.Choose(1, 60)
                           select DateTime.UtcNow.AddDays(offsetDays);

        var periodStartGen = from offsetDays in Gen.Choose(1, 30)
                             select DateTime.UtcNow.AddDays(-offsetDays);

        var tierGen = Gen.Elements("pro", "business", "enterprise");

        // Generate 1-3 active/trialing subs per space
        var activeSubGen = from status in statusGen
                           from periodStart in periodStartGen
                           from periodEnd in periodEndGen
                           from tier in tierGen
                           select (status, periodStart, periodEnd, tier);

        var activeSubsGen = from count in Gen.Choose(1, 3)
                            from subs in Gen.ListOf(count, activeSubGen)
                            select subs.ToList();

        var arb = Arb.From(activeSubsGen);

        return Prop.ForAll(arb, async (activeSubs) =>
        {
            using var db = CreateInMemoryDb();
            var cache = CreateMockTrialCache();
            var handler = CreateHandler(db, cache);

            var spaceId = Guid.NewGuid();

            // Seed active/trialing group subscriptions
            foreach (var (status, periodStart, periodEnd, tier) in activeSubs)
            {
                var groupId = Guid.NewGuid();
                var gs = CreateGroupSubscription(spaceId, groupId, status, periodStart, periodEnd, tierId: tier);
                db.GroupSubscriptions.Add(gs);
            }

            await db.SaveChangesAsync();

            // Act
            await handler.Handle(new MigrateToSpaceBillingCommand(100), CancellationToken.None);

            // Assert: SpaceSubscription should exist with Active status
            var spaceSub = await db.SpaceSubscriptions
                .FirstOrDefaultAsync(ss => ss.SpaceId == spaceId);

            spaceSub.Should().NotBeNull("Space with active/trialing group subs should get a SpaceSubscription");
            spaceSub!.Status.Should().Be(SubscriptionStatus.Active,
                "Space with active/trialing group subs should get Active SpaceSubscription");

            // Verify period dates come from the group sub with the latest CurrentPeriodEnd
            var allGroupSubs = await db.GroupSubscriptions
                .Where(gs => gs.SpaceId == spaceId)
                .ToListAsync();

            var latestSub = allGroupSubs
                .Where(gs => gs.CurrentPeriodEnd.HasValue)
                .OrderByDescending(gs => gs.CurrentPeriodEnd)
                .FirstOrDefault();

            if (latestSub != null)
            {
                spaceSub.CurrentPeriodStart.Should().Be(latestSub.CurrentPeriodStart,
                    "SpaceSubscription period start should match the group sub with latest period end");
                spaceSub.CurrentPeriodEnd.Should().Be(latestSub.CurrentPeriodEnd,
                    "SpaceSubscription period end should match the group sub with latest period end");
            }
        });
    }

    // ── Property 14c: Spaces without active/trialing group subs get Trialing SpaceSubscription ──
    // **Validates: Requirements 8.3**

    [Property(MaxTest = 100)]
    public Property Migration_InactiveGroupSubs_CreateTrialingSpaceSubscription()
    {
        // Generate spaces that have only canceled/expired group subscriptions
        var statusGen = Gen.Elements(SubscriptionStatus.Canceled, SubscriptionStatus.Expired);

        var periodEndGen = from offsetDays in Gen.Choose(1, 60)
                           select DateTime.UtcNow.AddDays(-offsetDays); // past period end

        var periodStartGen = from offsetDays in Gen.Choose(30, 60)
                             select DateTime.UtcNow.AddDays(-offsetDays);

        var inactiveSubGen = from status in statusGen
                             from periodStart in periodStartGen
                             from periodEnd in periodEndGen
                             select (status, periodStart, periodEnd);

        var inactiveSubsGen = from count in Gen.Choose(1, 3)
                              from subs in Gen.ListOf(count, inactiveSubGen)
                              select subs.ToList();

        var arb = Arb.From(inactiveSubsGen);

        return Prop.ForAll(arb, async (inactiveSubs) =>
        {
            using var db = CreateInMemoryDb();
            var cache = CreateMockTrialCache();
            var handler = CreateHandler(db, cache);

            var spaceId = Guid.NewGuid();

            // Seed only canceled/expired group subscriptions
            foreach (var (status, periodStart, periodEnd) in inactiveSubs)
            {
                var groupId = Guid.NewGuid();
                var gs = CreateGroupSubscription(spaceId, groupId, status, periodStart, periodEnd);
                db.GroupSubscriptions.Add(gs);
            }

            await db.SaveChangesAsync();

            // Act
            await handler.Handle(new MigrateToSpaceBillingCommand(100), CancellationToken.None);

            // Assert: SpaceSubscription should exist with Trialing status
            var spaceSub = await db.SpaceSubscriptions
                .FirstOrDefaultAsync(ss => ss.SpaceId == spaceId);

            spaceSub.Should().NotBeNull("Space with only inactive group subs should get a Trialing SpaceSubscription");
            spaceSub!.Status.Should().Be(SubscriptionStatus.Trialing,
                "Space with no active/trialing group subs should get Trialing SpaceSubscription");
        });
    }

    // ── Property 14d: Spaces with existing SpaceSubscription are skipped ─────
    // **Validates: Requirements 8.4**

    [Property(MaxTest = 100)]
    public Property Migration_SkipsSpacesWithExistingSpaceSubscription()
    {
        var statusGen = Gen.Elements(
            SubscriptionStatus.Active,
            SubscriptionStatus.Trialing,
            SubscriptionStatus.Canceled,
            SubscriptionStatus.Expired);

        var periodEndGen = from offsetDays in Gen.Choose(1, 60)
                           select DateTime.UtcNow.AddDays(offsetDays);

        var periodStartGen = from offsetDays in Gen.Choose(1, 30)
                             select DateTime.UtcNow.AddDays(-offsetDays);

        var groupSubGen = from status in statusGen
                          from periodStart in periodStartGen
                          from periodEnd in periodEndGen
                          select (status, periodStart, periodEnd);

        var groupSubsGen = from count in Gen.Choose(1, 3)
                           from subs in Gen.ListOf(count, groupSubGen)
                           select subs.ToList();

        var arb = Arb.From(groupSubsGen);

        return Prop.ForAll(arb, async (groupSubData) =>
        {
            using var db = CreateInMemoryDb();
            var cache = CreateMockTrialCache();
            var handler = CreateHandler(db, cache);

            var spaceId = Guid.NewGuid();

            // Pre-create a SpaceSubscription for this space
            var existingSub = SpaceSubscription.CreateTrial(spaceId, 14);
            db.SpaceSubscriptions.Add(existingSub);

            // Seed group subscriptions that would normally trigger migration
            foreach (var (status, periodStart, periodEnd) in groupSubData)
            {
                var groupId = Guid.NewGuid();
                var gs = CreateGroupSubscription(spaceId, groupId, status, periodStart, periodEnd);
                db.GroupSubscriptions.Add(gs);
            }

            await db.SaveChangesAsync();

            // Capture the existing subscription state before migration
            var existingSubId = existingSub.Id;
            var existingStatus = existingSub.Status;
            var existingTrialEndsAt = existingSub.TrialEndsAt;

            // Act
            await handler.Handle(new MigrateToSpaceBillingCommand(100), CancellationToken.None);

            // Assert: only one SpaceSubscription exists for this space (the pre-existing one)
            var spaceSubs = await db.SpaceSubscriptions
                .Where(ss => ss.SpaceId == spaceId)
                .ToListAsync();

            spaceSubs.Should().HaveCount(1,
                "Space with existing SpaceSubscription should not get a second one");
            spaceSubs[0].Id.Should().Be(existingSubId,
                "The existing SpaceSubscription should be unchanged");
            spaceSubs[0].Status.Should().Be(existingStatus,
                "The existing SpaceSubscription status should be unchanged");
            spaceSubs[0].TrialEndsAt.Should().Be(existingTrialEndsAt,
                "The existing SpaceSubscription trial end should be unchanged");
        });
    }
}
