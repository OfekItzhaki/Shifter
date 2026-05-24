// Feature: space-billing
// Properties 6, 7, 16: Checkout metadata, active subscription rejection, upgrade guard
// **Validates: Requirements 5.1, 5.2, 9.4, 10.2**

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Billing;
using Jobuler.Application.Billing.Commands;
using Jobuler.Application.Common;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Billing;

public class CheckoutAndUpgradePropertyTests
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

    private static ILemonSqueezyClient CreateCapturingLemonSqueezyClient(
        List<CreateCheckoutRequest> capturedRequests)
    {
        var client = Substitute.For<ILemonSqueezyClient>();
        client
            .CreateCheckoutAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var req = callInfo.Arg<CreateCheckoutRequest>();
                capturedRequests.Add(req);
                return "https://checkout.lemonsqueezy.com/test";
            });
        return client;
    }

    private static ILemonSqueezyClient CreateMockLemonSqueezyClient()
    {
        var client = Substitute.For<ILemonSqueezyClient>();
        client
            .CreateCheckoutAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>())
            .Returns("https://checkout.lemonsqueezy.com/test");
        return client;
    }

    private static IOptions<BillingOptions> CreateBillingOptions(string defaultVariantId = "variant_default")
    {
        return Options.Create(new BillingOptions
        {
            DefaultVariantId = defaultVariantId,
            TestVariantId = "variant_test"
        });
    }

    // ── Generators ───────────────────────────────────────────────────────────

    /// <summary>
    /// Generates random space IDs and optional variant IDs for checkout commands.
    /// </summary>
    private static Arbitrary<(Guid spaceId, Guid userId, string? variantId)> CheckoutInputArbitrary()
    {
        var gen = from spaceId in Arb.Generate<Guid>()
                  from userId in Arb.Generate<Guid>()
                  from useCustomVariant in Arb.Generate<bool>()
                  from variantSuffix in Gen.Choose(1, 9999)
                  let variantId = useCustomVariant ? $"variant_{variantSuffix}" : null
                  select (spaceId, userId, variantId);

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates statuses that are NOT Active (for Property 7 — statuses that allow checkout).
    /// We need to seed the DB with an Active subscription to test rejection.
    /// </summary>
    private static Arbitrary<(Guid spaceId, Guid userId, int trialDays)> ActiveSubscriptionInputArbitrary()
    {
        var gen = from spaceId in Arb.Generate<Guid>()
                  from userId in Arb.Generate<Guid>()
                  from trialDays in Gen.Choose(1, 365)
                  select (spaceId, userId, trialDays);

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates subscription statuses that should reject upgrade (not Active, not Trialing).
    /// </summary>
    private static Arbitrary<SubscriptionStatus> UpgradeBlockingStatusArbitrary()
    {
        var gen = Gen.Elements(
            SubscriptionStatus.Canceled,
            SubscriptionStatus.Expired,
            SubscriptionStatus.PastDue);
        return Arb.From(gen);
    }

    // ── Property 6: Checkout metadata always includes space_id ───────────────
    // **Validates: Requirements 5.1, 9.4**

    [Property(MaxTest = 100)]
    public Property CheckoutMetadata_AlwaysIncludesSpaceId()
    {
        return Prop.ForAll(CheckoutInputArbitrary(), input =>
        {
            var (spaceId, userId, variantId) = input;

            // Skip empty GUIDs (FsCheck can generate them)
            if (spaceId == Guid.Empty || userId == Guid.Empty)
                return true.ToProperty();

            var dbName = $"CheckoutMetadata_{Guid.NewGuid()}";
            using var db = CreateInMemoryDb(dbName);

            // No existing subscription → checkout should proceed
            var capturedRequests = new List<CreateCheckoutRequest>();
            var lsClient = CreateCapturingLemonSqueezyClient(capturedRequests);

            var handler = new CreateSpaceCheckoutCommandHandler(
                db,
                CreatePermissivePermissionService(),
                lsClient,
                CreateBillingOptions());

            var command = new CreateSpaceCheckoutCommand(spaceId, userId, variantId);

            handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

            // Assert: exactly one call was made and metadata contains space_id
            return (capturedRequests.Count == 1)
                .Label("Exactly one checkout request should be made")
                .And(capturedRequests[0].Metadata.ContainsKey("space_id")
                .Label("Metadata should contain 'space_id' key"))
                .And((capturedRequests[0].Metadata["space_id"] == spaceId.ToString())
                .Label("Metadata 'space_id' should match the requesting space's ID"));
        });
    }

    // ── Property 7: Active subscription rejects checkout ─────────────────────
    // **Validates: Requirements 5.2**

    [Property(MaxTest = 100)]
    public Property ActiveSubscription_RejectsCheckout_AndDoesNotCallLemonSqueezy()
    {
        return Prop.ForAll(ActiveSubscriptionInputArbitrary(), input =>
        {
            var (spaceId, userId, trialDays) = input;

            if (spaceId == Guid.Empty || userId == Guid.Empty)
                return true.ToProperty();

            var dbName = $"ActiveRejectsCheckout_{Guid.NewGuid()}";
            using var db = CreateInMemoryDb(dbName);

            // Create an Active space subscription
            var sub = SpaceSubscription.CreateTrial(spaceId, trialDays);
            sub.Activate("pro", $"ls_sub_{Guid.NewGuid()}", $"ls_cus_{Guid.NewGuid()}",
                DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(30));
            db.SpaceSubscriptions.Add(sub);
            db.SaveChanges();

            var lsClient = Substitute.For<ILemonSqueezyClient>();

            var handler = new CreateSpaceCheckoutCommandHandler(
                db,
                CreatePermissivePermissionService(),
                lsClient,
                CreateBillingOptions());

            var command = new CreateSpaceCheckoutCommand(spaceId, userId);

            // Act & Assert: should throw InvalidOperationException
            var threw = false;
            try
            {
                handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            // Verify LemonSqueezy was never called
            lsClient.DidNotReceive()
                .CreateCheckoutAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>());

            return threw
                .Label("Should throw InvalidOperationException for active subscription");
        });
    }

    // ── Property 16: Upgrade guard ───────────────────────────────────────────
    // **Validates: Requirements 10.2**

    [Property(MaxTest = 100)]
    public Property UpgradeGuard_RejectsWhenStatusIsNotActiveOrTrialing()
    {
        var gen = from status in UpgradeBlockingStatusArbitrary().Generator
                  from spaceId in Arb.Generate<Guid>()
                  from userId in Arb.Generate<Guid>()
                  from variantSuffix in Gen.Choose(1, 9999)
                  from periodOffsetDays in Gen.Choose(1, 365)
                  from periodDurationDays in Gen.Choose(1, 90)
                  select (status, spaceId, userId, $"variant_{variantSuffix}", periodOffsetDays, periodDurationDays);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (status, spaceId, userId, variantId, periodOffsetDays, periodDurationDays) = tuple;

            if (spaceId == Guid.Empty || userId == Guid.Empty)
                return true.ToProperty();

            var dbName = $"UpgradeGuard_{Guid.NewGuid()}";
            using var db = CreateInMemoryDb(dbName);

            // Create a subscription and transition it to the blocking status
            var periodStart = DateTime.UtcNow.AddDays(-periodOffsetDays);
            var periodEnd = periodStart.AddDays(periodDurationDays);
            var sub = SpaceSubscription.CreateTrial(spaceId, 14);
            sub.Activate("basic", $"ls_sub_{Guid.NewGuid()}", $"ls_cus_{Guid.NewGuid()}",
                periodStart, periodEnd);

            // Transition to the target blocking status
            if (status == SubscriptionStatus.Canceled)
            {
                sub.Cancel();
            }
            else if (status == SubscriptionStatus.Expired)
            {
                sub.Cancel();
                sub.Expire();
            }
            else if (status == SubscriptionStatus.PastDue)
            {
                // PastDue is not directly reachable via domain methods,
                // so we use reflection to set the status
                typeof(SpaceSubscription)
                    .GetProperty(nameof(SpaceSubscription.Status))!
                    .SetValue(sub, SubscriptionStatus.PastDue);
            }

            db.SpaceSubscriptions.Add(sub);
            db.SaveChanges();

            var lsClient = Substitute.For<ILemonSqueezyClient>();

            var handler = new UpgradeSpacePlanCommandHandler(
                db,
                CreatePermissivePermissionService(),
                lsClient);

            var command = new UpgradeSpacePlanCommand(spaceId, userId, variantId);

            // Act & Assert: should throw InvalidOperationException
            var threw = false;
            try
            {
                handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            // Verify LemonSqueezy was never called
            lsClient.DidNotReceive()
                .CreateCheckoutAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>());

            return threw
                .Label($"Should throw InvalidOperationException for status {status}");
        });
    }
}
