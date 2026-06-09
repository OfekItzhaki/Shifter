// Feature: lemonsqueezy-billing
// Property 19: Checkout is rejected for active subscriptions
// **Validates: Requirements 1.6**

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Billing;
using Jobuler.Application.Billing.Commands;
using Jobuler.Application.Common;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Groups;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Billing;

public class CheckoutRejectionPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static IPermissionService CreatePermissivePermissionService()
    {
        var permissions = Substitute.For<IPermissionService>();
        permissions
            .RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return permissions;
    }

    private static ILemonSqueezyClient CreateMockLemonSqueezyClient()
    {
        var client = Substitute.For<ILemonSqueezyClient>();
        client
            .CreateCheckoutAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>())
            .Returns("https://checkout.lemonsqueezy.com/test");
        return client;
    }

    private static IOptions<BillingOptions> CreateBillingOptions()
    {
        return Options.Create(new BillingOptions
        {
            DefaultVariantId = "variant_123",
            TestVariantId = "variant_test_456"
        });
    }

    // ── Generators ───────────────────────────────────────────────────────────

    /// <summary>
    /// Generates subscription statuses that should block checkout.
    /// </summary>
    private static Arbitrary<SubscriptionStatus> BlockingStatusArbitrary()
    {
        var gen = Gen.Constant(SubscriptionStatus.Active);
        return Arb.From(gen);
    }

    // ── Property 19: Checkout is rejected for active subscriptions ─────────────
    // **Validates: Requirements 1.6**

    [Property(MaxTest = 100)]
    public Property Checkout_IsRejected_WhenGroupHasActiveSubscription()
    {
        var gen = from status in BlockingStatusArbitrary().Generator
                  from trialDays in Gen.Choose(1, 30)
                  from periodOffsetDays in Gen.Choose(1, 365)
                  from periodDurationDays in Gen.Choose(1, 90)
                  select (status, trialDays, periodOffsetDays, periodDurationDays);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (status, trialDays, periodOffsetDays, periodDurationDays) = tuple;

            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var dbName = $"CheckoutRejection_{Guid.NewGuid()}";

            using var db = CreateInMemoryDb(dbName);

            // Create a group in the space
            var group = Group.Create(spaceId, null, "Test Group");
            // Use reflection to set the group's Id to our known groupId
            typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
            db.Groups.Add(group);

            // Create a subscription with the blocking status
            var sub = GroupSubscription.CreateTrial(spaceId, groupId, trialDays);

            if (status == SubscriptionStatus.Active)
            {
                var periodStart = DateTime.UtcNow.AddDays(-periodOffsetDays);
                var periodEnd = periodStart.AddDays(periodDurationDays);
                sub.Activate("pro", $"ls_sub_{Guid.NewGuid()}", $"ls_cus_{Guid.NewGuid()}", periodStart, periodEnd);
            }
            db.GroupSubscriptions.Add(sub);
            db.SaveChanges();

            // Act: attempt checkout
            var handler = new CreateCheckoutCommandHandler(
                db,
                CreatePermissivePermissionService(),
                CreateMockLemonSqueezyClient(),
                CreateBillingOptions());

            var command = new CreateCheckoutCommand(spaceId, groupId, userId);

            var act = () => handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

            // Assert: should throw InvalidOperationException
            act.Should().Throw<InvalidOperationException>();
        });
    }

    // ── Complementary: Checkout succeeds when subscription is NOT active/trialing ──

    [Property(MaxTest = 100)]
    public Property Checkout_Succeeds_WhenGroupHasNonBlockingSubscription()
    {
        var nonBlockingStatuses = Gen.Elements(
            SubscriptionStatus.PastDue,
            SubscriptionStatus.Canceled,
            SubscriptionStatus.Expired);

        var gen = from status in nonBlockingStatuses
                  from periodOffsetDays in Gen.Choose(1, 365)
                  from periodDurationDays in Gen.Choose(1, 90)
                  select (status, periodOffsetDays, periodDurationDays);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (status, periodOffsetDays, periodDurationDays) = tuple;

            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var dbName = $"CheckoutSuccess_{Guid.NewGuid()}";

            using var db = CreateInMemoryDb(dbName);

            // Create a group in the space
            var group = Group.Create(spaceId, null, "Test Group");
            typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
            db.Groups.Add(group);

            // Create a subscription with a non-blocking status
            var periodStart = DateTime.UtcNow.AddDays(-periodOffsetDays);
            var periodEnd = periodStart.AddDays(periodDurationDays);
            var sub = GroupSubscription.CreateTrial(spaceId, groupId, 14);
            sub.Activate("pro", $"ls_sub_{Guid.NewGuid()}", $"ls_cus_{Guid.NewGuid()}", periodStart, periodEnd);

            // Transition to the non-blocking status
            if (status == SubscriptionStatus.PastDue)
            {
                sub.UpdateStatus(SubscriptionStatus.PastDue);
            }
            else if (status == SubscriptionStatus.Canceled)
            {
                sub.Cancel();
            }
            else if (status == SubscriptionStatus.Expired)
            {
                sub.Cancel();
                sub.Expire();
            }

            db.GroupSubscriptions.Add(sub);
            db.SaveChanges();

            // Act: attempt checkout
            var handler = new CreateCheckoutCommandHandler(
                db,
                CreatePermissivePermissionService(),
                CreateMockLemonSqueezyClient(),
                CreateBillingOptions());

            var command = new CreateCheckoutCommand(spaceId, groupId, userId);

            var act = () => handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

            // Assert: should NOT throw (checkout should succeed)
            act.Should().NotThrow();
        });
    }
}
