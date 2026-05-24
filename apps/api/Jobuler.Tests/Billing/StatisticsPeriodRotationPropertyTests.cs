// Feature: space-billing, Property 13: Lifecycle events rotate statistics periods
// Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Billing;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobuler.Tests.Billing;

public class StatisticsPeriodRotationPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static StatisticsPeriodService CreateService(AppDbContext db)
    {
        var logger = NullLogger<StatisticsPeriodService>.Instance;
        return new StatisticsPeriodService(db, logger);
    }

    /// <summary>
    /// Seeds N active groups for a space and creates one active SubscriptionPeriod per group.
    /// Returns the spaceId and list of groupIds.
    /// </summary>
    private static async Task<(Guid spaceId, List<Guid> groupIds)> SeedGroupsWithActivePeriodsAsync(
        AppDbContext db, int groupCount, DateTime periodStartsAt)
    {
        var spaceId = Guid.NewGuid();

        var groupIds = new List<Guid>();
        for (var i = 0; i < groupCount; i++)
        {
            var group = Group.Create(spaceId, null, $"Group-{i + 1}");
            db.Groups.Add(group);
            groupIds.Add(group.Id);
        }

        await db.SaveChangesAsync();

        // Create active SubscriptionPeriod for each group
        foreach (var groupId in groupIds)
        {
            var period = SubscriptionPeriod.Create(spaceId, groupId);
            db.SubscriptionPeriods.Add(period);
            // Override StartsAt to use the seeded start date
            db.Entry(period).Property(p => p.StartsAt).CurrentValue = periodStartsAt;
        }

        await db.SaveChangesAsync();

        return (spaceId, groupIds);
    }

    /// <summary>
    /// Seeds N active groups for a space WITHOUT creating active periods (for trial start scenario).
    /// </summary>
    private static async Task<(Guid spaceId, List<Guid> groupIds)> SeedGroupsWithoutPeriodsAsync(
        AppDbContext db, int groupCount)
    {
        var spaceId = Guid.NewGuid();

        var groupIds = new List<Guid>();
        for (var i = 0; i < groupCount; i++)
        {
            var group = Group.Create(spaceId, null, $"Group-{i + 1}");
            db.Groups.Add(group);
            groupIds.Add(group.Id);
        }

        await db.SaveChangesAsync();

        return (spaceId, groupIds);
    }

    // ── Generators ───────────────────────────────────────────────────────────

    private static Arbitrary<int> GroupCountArbitrary()
    {
        return Arb.From(Gen.Choose(1, 10));
    }

    private static Arbitrary<DateTime> BoundaryDateArbitrary()
    {
        var gen = from offsetHours in Gen.Choose(1, 8760) // up to 1 year in hours
                  select DateTime.UtcNow.AddHours(-offsetHours);
        return Arb.From(gen);
    }

    // ── Property 13a: OnTrialStartedAsync opens N new periods ────────────────
    // **Validates: Requirements 7.1**

    [Property(MaxTest = 100)]
    public Property OnTrialStarted_OpensNewPeriodsForAllGroups()
    {
        return Prop.ForAll(GroupCountArbitrary(), BoundaryDateArbitrary(), async (groupCount, boundary) =>
        {
            using var db = CreateInMemoryDb();
            var service = CreateService(db);

            // Seed groups without existing periods (trial start is the first event)
            var (spaceId, groupIds) = await SeedGroupsWithoutPeriodsAsync(db, groupCount);

            // Act
            await service.OnTrialStartedAsync(spaceId, boundary, CancellationToken.None);

            // Assert: exactly N new active periods exist
            var activePeriods = await db.SubscriptionPeriods
                .Where(sp => sp.SpaceId == spaceId && sp.Status == "active")
                .ToListAsync();

            activePeriods.Should().HaveCount(groupCount,
                $"OnTrialStarted should open exactly {groupCount} new periods");

            // All new periods should have StartsAt == boundary
            activePeriods.Should().AllSatisfy(p =>
            {
                p.StartsAt.Should().Be(boundary);
            });

            // Each group should have exactly one active period
            foreach (var groupId in groupIds)
            {
                activePeriods.Count(p => p.GroupId == groupId).Should().Be(1,
                    $"Group {groupId} should have exactly one active period");
            }
        });
    }

    // ── Property 13b: OnTrialExpiredAsync closes N active periods ─────────────
    // **Validates: Requirements 7.2**

    [Property(MaxTest = 100)]
    public Property OnTrialExpired_ClosesAllActivePeriodsForAllGroups()
    {
        return Prop.ForAll(GroupCountArbitrary(), BoundaryDateArbitrary(), async (groupCount, boundary) =>
        {
            using var db = CreateInMemoryDb();
            var service = CreateService(db);

            var periodStart = boundary.AddDays(-14); // periods started 14 days before boundary
            var (spaceId, groupIds) = await SeedGroupsWithActivePeriodsAsync(db, groupCount, periodStart);

            // Act
            await service.OnTrialExpiredAsync(spaceId, boundary, CancellationToken.None);

            // Assert: no active periods remain
            var activePeriods = await db.SubscriptionPeriods
                .Where(sp => sp.SpaceId == spaceId && sp.Status == "active")
                .ToListAsync();

            activePeriods.Should().BeEmpty("OnTrialExpired should close all active periods");

            // Assert: N closed periods exist with EndsAt == boundary
            var closedPeriods = await db.SubscriptionPeriods
                .Where(sp => sp.SpaceId == spaceId && sp.Status == "closed")
                .ToListAsync();

            closedPeriods.Should().HaveCount(groupCount,
                $"OnTrialExpired should close exactly {groupCount} periods");

            closedPeriods.Should().AllSatisfy(p =>
            {
                p.EndsAt.Should().Be(boundary);
            });
        });
    }

    // ── Property 13c: OnSubscriptionActivatedAsync closes N and opens N ──────
    // **Validates: Requirements 7.3**

    [Property(MaxTest = 100)]
    public Property OnSubscriptionActivated_ClosesActivePeriodsAndOpensNewOnes()
    {
        return Prop.ForAll(GroupCountArbitrary(), BoundaryDateArbitrary(), async (groupCount, boundary) =>
        {
            using var db = CreateInMemoryDb();
            var service = CreateService(db);

            var periodStart = boundary.AddDays(-14);
            var (spaceId, groupIds) = await SeedGroupsWithActivePeriodsAsync(db, groupCount, periodStart);

            // Act
            await service.OnSubscriptionActivatedAsync(spaceId, boundary, CancellationToken.None);

            // Assert: N closed periods with EndsAt == boundary
            var closedPeriods = await db.SubscriptionPeriods
                .Where(sp => sp.SpaceId == spaceId && sp.Status == "closed")
                .ToListAsync();

            closedPeriods.Should().HaveCount(groupCount,
                $"OnSubscriptionActivated should close exactly {groupCount} periods");

            closedPeriods.Should().AllSatisfy(p =>
            {
                p.EndsAt.Should().Be(boundary);
            });

            // Assert: N new active periods with StartsAt == boundary
            var activePeriods = await db.SubscriptionPeriods
                .Where(sp => sp.SpaceId == spaceId && sp.Status == "active")
                .ToListAsync();

            activePeriods.Should().HaveCount(groupCount,
                $"OnSubscriptionActivated should open exactly {groupCount} new periods");

            activePeriods.Should().AllSatisfy(p =>
            {
                p.StartsAt.Should().Be(boundary);
            });

            // Each group should have exactly one active period
            foreach (var groupId in groupIds)
            {
                activePeriods.Count(p => p.GroupId == groupId).Should().Be(1,
                    $"Group {groupId} should have exactly one active period after activation");
            }
        });
    }

    // ── Property 13d: OnSubscriptionExpiredAsync closes N active periods ─────
    // **Validates: Requirements 7.4**

    [Property(MaxTest = 100)]
    public Property OnSubscriptionExpired_ClosesAllActivePeriodsForAllGroups()
    {
        return Prop.ForAll(GroupCountArbitrary(), BoundaryDateArbitrary(), async (groupCount, boundary) =>
        {
            using var db = CreateInMemoryDb();
            var service = CreateService(db);

            var periodStart = boundary.AddDays(-30);
            var (spaceId, groupIds) = await SeedGroupsWithActivePeriodsAsync(db, groupCount, periodStart);

            // Act
            await service.OnSubscriptionExpiredAsync(spaceId, boundary, CancellationToken.None);

            // Assert: no active periods remain
            var activePeriods = await db.SubscriptionPeriods
                .Where(sp => sp.SpaceId == spaceId && sp.Status == "active")
                .ToListAsync();

            activePeriods.Should().BeEmpty("OnSubscriptionExpired should close all active periods");

            // Assert: N closed periods exist with EndsAt == boundary
            var closedPeriods = await db.SubscriptionPeriods
                .Where(sp => sp.SpaceId == spaceId && sp.Status == "closed")
                .ToListAsync();

            closedPeriods.Should().HaveCount(groupCount,
                $"OnSubscriptionExpired should close exactly {groupCount} periods");

            closedPeriods.Should().AllSatisfy(p =>
            {
                p.EndsAt.Should().Be(boundary);
            });
        });
    }

    // ── Property 13e: OnPeriodRenewedAsync closes N and opens N ──────────────
    // **Validates: Requirements 7.5**

    [Property(MaxTest = 100)]
    public Property OnPeriodRenewed_ClosesActivePeriodsAndOpensNewOnes()
    {
        return Prop.ForAll(GroupCountArbitrary(), BoundaryDateArbitrary(), async (groupCount, boundary) =>
        {
            using var db = CreateInMemoryDb();
            var service = CreateService(db);

            var periodStart = boundary.AddDays(-30);
            var (spaceId, groupIds) = await SeedGroupsWithActivePeriodsAsync(db, groupCount, periodStart);

            // Act
            await service.OnPeriodRenewedAsync(spaceId, boundary, CancellationToken.None);

            // Assert: N closed periods with EndsAt == boundary
            var closedPeriods = await db.SubscriptionPeriods
                .Where(sp => sp.SpaceId == spaceId && sp.Status == "closed")
                .ToListAsync();

            closedPeriods.Should().HaveCount(groupCount,
                $"OnPeriodRenewed should close exactly {groupCount} periods");

            closedPeriods.Should().AllSatisfy(p =>
            {
                p.EndsAt.Should().Be(boundary);
            });

            // Assert: N new active periods with StartsAt == boundary
            var activePeriods = await db.SubscriptionPeriods
                .Where(sp => sp.SpaceId == spaceId && sp.Status == "active")
                .ToListAsync();

            activePeriods.Should().HaveCount(groupCount,
                $"OnPeriodRenewed should open exactly {groupCount} new periods");

            activePeriods.Should().AllSatisfy(p =>
            {
                p.StartsAt.Should().Be(boundary);
            });

            // Each group should have exactly one active period
            foreach (var groupId in groupIds)
            {
                activePeriods.Count(p => p.GroupId == groupId).Should().Be(1,
                    $"Group {groupId} should have exactly one active period after renewal");
            }
        });
    }
}
