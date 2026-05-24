// Feature: space-billing
// Properties 2, 8: Subscription creation idempotency and webhook idempotency
// **Validates: Requirements 1.6, 5.3**

using System.Text.Json;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Billing;
using Jobuler.Application.Billing.Commands;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Billing;

public class WebhookHandlingPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IStatisticsPeriodService CreateNoOpStatisticsPeriodService()
    {
        var service = Substitute.For<IStatisticsPeriodService>();
        service.OnSubscriptionActivatedAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return service;
    }

    private static string BuildSubscriptionCreatedPayload(
        string lsSubscriptionId, long customerId, long variantId,
        DateTime periodStart, DateTime periodEnd)
    {
        return JsonSerializer.Serialize(new
        {
            data = new
            {
                id = lsSubscriptionId,
                attributes = new
                {
                    customer_id = customerId,
                    variant_id = variantId,
                    status = "active",
                    current_period_start = periodStart.ToString("o"),
                    current_period_end = periodEnd.ToString("o")
                }
            }
        });
    }

    // ── Generators ───────────────────────────────────────────────────────────

    /// <summary>
    /// Generates valid inputs for subscription creation: spaceId, trialDays, tier info, period dates.
    /// </summary>
    private static Arbitrary<(Guid spaceId, int trialDays, long customerId, long variantId, int periodOffsetDays, int periodDurationDays)>
        SubscriptionCreationInputArbitrary()
    {
        var gen = from spaceId in Arb.Generate<Guid>().Where(g => g != Guid.Empty)
                  from trialDays in Gen.Choose(1, 365)
                  from customerId in Gen.Choose(1000, 999999).Select(x => (long)x)
                  from variantId in Gen.Choose(100, 9999).Select(x => (long)x)
                  from periodOffsetDays in Gen.Choose(0, 30)
                  from periodDurationDays in Gen.Choose(7, 90)
                  select (spaceId, trialDays, customerId, variantId, periodOffsetDays, periodDurationDays);

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates valid webhook event inputs: eventId, eventType, spaceId.
    /// </summary>
    private static Arbitrary<(string eventId, string eventType, Guid spaceId, int trialDays)>
        WebhookEventInputArbitrary()
    {
        var eventTypeGen = Gen.Elements(
            "subscription_created",
            "subscription_updated",
            "subscription_cancelled",
            "subscription_payment_success");

        var gen = from eventId in Arb.Generate<Guid>().Select(g => g.ToString())
                  from eventType in eventTypeGen
                  from spaceId in Arb.Generate<Guid>().Where(g => g != Guid.Empty)
                  from trialDays in Gen.Choose(1, 365)
                  select (eventId, eventType, spaceId, trialDays);

        return Arb.From(gen);
    }

    // ── Property 2: Subscription creation idempotency ────────────────────────
    // For any space that already has a SpaceSubscription, calling the subscription
    // creation logic again SHALL NOT create a second subscription and SHALL leave
    // the existing record's fields unchanged.
    // **Validates: Requirements 1.6**

    [Property(MaxTest = 100)]
    public Property SubscriptionCreationIdempotency_DuplicateDoesNotCreateSecondSubscription()
    {
        return Prop.ForAll(SubscriptionCreationInputArbitrary(), async input =>
        {
            var (spaceId, trialDays, customerId, variantId, periodOffsetDays, periodDurationDays) = input;

            var dbName = $"SubCreationIdemp_{Guid.NewGuid()}";
            using var db = CreateInMemoryDb(dbName);
            var statisticsPeriods = CreateNoOpStatisticsPeriodService();
            var logger = NullLogger<HandleSpaceSubscriptionCreatedCommandHandler>.Instance;

            // ── Arrange: Create a SpaceSubscription in trialing state ─────────
            var sub = SpaceSubscription.CreateTrial(spaceId, trialDays);
            db.SpaceSubscriptions.Add(sub);
            await db.SaveChangesAsync();

            // ── Act: First subscription_created webhook activates it ──────────
            var periodStart = DateTime.UtcNow.AddDays(-periodOffsetDays);
            var periodEnd = periodStart.AddDays(periodDurationDays);
            var lsSubId = $"ls_sub_{Guid.NewGuid()}";
            var payload = BuildSubscriptionCreatedPayload(lsSubId, customerId, variantId, periodStart, periodEnd);
            var metadata = new Dictionary<string, string> { ["space_id"] = spaceId.ToString() };

            var handler = new HandleSpaceSubscriptionCreatedCommandHandler(db, statisticsPeriods, logger);
            var command = new HandleSpaceSubscriptionCreatedCommand(payload, metadata);

            await handler.Handle(command, CancellationToken.None);

            // Capture state after first activation
            var afterFirst = await db.SpaceSubscriptions.FirstAsync(s => s.SpaceId == spaceId);
            var statusAfterFirst = afterFirst.Status;
            var tierAfterFirst = afterFirst.TierId;
            var lsIdAfterFirst = afterFirst.LemonSqueezySubscriptionId;
            var periodStartAfterFirst = afterFirst.CurrentPeriodStart;
            var periodEndAfterFirst = afterFirst.CurrentPeriodEnd;

            statusAfterFirst.Should().Be(SubscriptionStatus.Active);

            // ── Act: Second subscription_created webhook (duplicate) ──────────
            // Use a different payload to prove the second call is truly a no-op
            var differentLsSubId = $"ls_sub_{Guid.NewGuid()}";
            var differentPayload = BuildSubscriptionCreatedPayload(
                differentLsSubId, customerId + 1, variantId + 1,
                periodStart.AddDays(1), periodEnd.AddDays(1));

            await handler.Handle(
                new HandleSpaceSubscriptionCreatedCommand(differentPayload, metadata),
                CancellationToken.None);

            // ── Assert: Only one subscription exists ──────────────────────────
            var subscriptionCount = await db.SpaceSubscriptions.CountAsync(s => s.SpaceId == spaceId);
            subscriptionCount.Should().Be(1, "duplicate creation should not create a second subscription");

            // ── Assert: State is unchanged after second call ──────────────────
            var afterSecond = await db.SpaceSubscriptions.FirstAsync(s => s.SpaceId == spaceId);
            afterSecond.Status.Should().Be(statusAfterFirst, "status should be unchanged after duplicate");
            afterSecond.TierId.Should().Be(tierAfterFirst, "tier should be unchanged after duplicate");
            afterSecond.LemonSqueezySubscriptionId.Should().Be(lsIdAfterFirst, "LS subscription ID should be unchanged after duplicate");
            afterSecond.CurrentPeriodStart.Should().Be(periodStartAfterFirst, "period start should be unchanged after duplicate");
            afterSecond.CurrentPeriodEnd.Should().Be(periodEndAfterFirst, "period end should be unchanged after duplicate");
        });
    }

    // ── Property 8: Webhook idempotency ──────────────────────────────────────
    // For any webhook event ID that has already been processed (exists in
    // WebhookEventLog), processing the same event ID again SHALL NOT modify
    // the SpaceSubscription state and SHALL return early.
    // **Validates: Requirements 5.3**

    [Property(MaxTest = 100)]
    public Property WebhookIdempotency_DuplicateEventIdDoesNotModifyState()
    {
        return Prop.ForAll(WebhookEventInputArbitrary(), async input =>
        {
            var (eventId, eventType, spaceId, trialDays) = input;

            var dbName = $"WebhookIdemp_{Guid.NewGuid()}";
            using var db = CreateInMemoryDb(dbName);
            var mediator = Substitute.For<IMediator>();
            var logger = NullLogger<HandleWebhookCommandHandler>.Instance;

            // ── Arrange: Create a SpaceSubscription ───────────────────────────
            var sub = SpaceSubscription.CreateTrial(spaceId, trialDays);
            db.SpaceSubscriptions.Add(sub);
            await db.SaveChangesAsync();

            // Capture initial state
            var initialStatus = sub.Status;
            var initialTier = sub.TierId;
            var initialLsSubId = sub.LemonSqueezySubscriptionId;
            var initialPeriodStart = sub.CurrentPeriodStart;
            var initialPeriodEnd = sub.CurrentPeriodEnd;

            var handler = new HandleWebhookCommandHandler(db, mediator, logger);

            // Space-level metadata (space_id without group_id)
            var metadata = new Dictionary<string, string> { ["space_id"] = spaceId.ToString() };

            // ── Act: First processing ─────────────────────────────────────────
            var command = new HandleWebhookCommand(eventId, eventType, "{}", metadata);
            await handler.Handle(command, CancellationToken.None);

            // Verify event was logged
            var eventLogged = await db.WebhookEventLogs.AnyAsync(e => e.EventId == eventId);
            eventLogged.Should().BeTrue("first processing should log the event");

            // Clear mediator call tracking
            mediator.ClearReceivedCalls();

            // ── Act: Second processing with same event ID ─────────────────────
            await handler.Handle(command, CancellationToken.None);

            // ── Assert: No sub-handlers were dispatched on second call ────────
            await mediator.DidNotReceive().Send(
                Arg.Any<HandleSpaceSubscriptionCreatedCommand>(), Arg.Any<CancellationToken>());
            await mediator.DidNotReceive().Send(
                Arg.Any<HandleSpaceSubscriptionUpdatedCommand>(), Arg.Any<CancellationToken>());
            await mediator.DidNotReceive().Send(
                Arg.Any<HandleSpaceSubscriptionCancelledCommand>(), Arg.Any<CancellationToken>());

            // ── Assert: Only one event log entry exists ───────────────────────
            var logCount = await db.WebhookEventLogs.CountAsync(e => e.EventId == eventId);
            logCount.Should().Be(1, "duplicate event should not create a second log entry");

            // ── Assert: SpaceSubscription state is unchanged ──────────────────
            db.ChangeTracker.Clear();
            var afterDuplicate = await db.SpaceSubscriptions.FirstAsync(s => s.SpaceId == spaceId);
            afterDuplicate.Status.Should().Be(initialStatus, "status should be unchanged after duplicate webhook");
            afterDuplicate.TierId.Should().Be(initialTier, "tier should be unchanged after duplicate webhook");
            afterDuplicate.LemonSqueezySubscriptionId.Should().Be(initialLsSubId, "LS sub ID should be unchanged after duplicate webhook");
            afterDuplicate.CurrentPeriodStart.Should().Be(initialPeriodStart, "period start should be unchanged after duplicate webhook");
            afterDuplicate.CurrentPeriodEnd.Should().Be(initialPeriodEnd, "period end should be unchanged after duplicate webhook");
        });
    }
}
