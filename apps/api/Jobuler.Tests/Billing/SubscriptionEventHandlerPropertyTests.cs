// Feature: lemonsqueezy-billing
// Properties 4–15: Subscription event handler property tests
// Validates: Requirements 3.1, 3.2, 3.4, 3.5, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 8.5

using System.Text.Json;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Billing.Commands;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Common;
using Jobuler.Domain.Groups;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Billing;

public class SubscriptionEventHandlerPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static GroupSubscription CreateTrialingSubscription(Guid spaceId, Guid groupId)
    {
        return GroupSubscription.CreateTrial(spaceId, groupId, trialDays: 14);
    }

    private static GroupSubscription CreateActiveSubscription(
        Guid spaceId, Guid groupId, string lsSubId, string lsCustId,
        DateTime periodStart, DateTime periodEnd)
    {
        var sub = GroupSubscription.CreateTrial(spaceId, groupId, trialDays: 14);
        sub.Activate("pro", lsSubId, lsCustId, periodStart, periodEnd);
        return sub;
    }

    private static string BuildSubscriptionCreatedPayload(
        string subscriptionId, string status, long customerId,
        DateTime? periodStart = null, DateTime? periodEnd = null,
        DateTime? trialEndsAt = null, long variantId = 12345)
    {
        var attributes = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["customer_id"] = customerId,
            ["variant_id"] = variantId,
            ["renews_at"] = (periodEnd ?? DateTime.UtcNow.AddMonths(1)).ToString("O"),
        };

        if (periodStart.HasValue)
            attributes["current_period_start"] = periodStart.Value.ToString("O");
        if (periodEnd.HasValue)
            attributes["current_period_end"] = periodEnd.Value.ToString("O");
        if (trialEndsAt.HasValue)
            attributes["trial_ends_at"] = trialEndsAt.Value.ToString("O");

        var payload = new
        {
            data = new
            {
                id = subscriptionId,
                attributes
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string BuildSubscriptionUpdatedPayload(
        string subscriptionId, string status,
        DateTime? periodStart = null, DateTime? periodEnd = null)
    {
        var attributes = new Dictionary<string, object?>
        {
            ["status"] = status,
        };

        if (periodStart.HasValue)
            attributes["current_period_start"] = periodStart.Value.ToString("O");
        if (periodEnd.HasValue)
            attributes["current_period_end"] = periodEnd.Value.ToString("O");

        var payload = new
        {
            data = new
            {
                id = subscriptionId,
                attributes
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string BuildSubscriptionCancelledPayload(string subscriptionId)
    {
        var payload = new
        {
            data = new
            {
                id = subscriptionId,
                attributes = new { status = "cancelled" }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string BuildPaymentSuccessPayload(
        long subscriptionId, DateTime periodStart, DateTime periodEnd)
    {
        var payload = new
        {
            data = new
            {
                attributes = new
                {
                    subscription_id = subscriptionId,
                    current_period_start = periodStart.ToString("O"),
                    current_period_end = periodEnd.ToString("O"),
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    // ── Generators ───────────────────────────────────────────────────────────

    private static Arbitrary<(DateTime periodStart, DateTime periodEnd)> PeriodDatesArbitrary()
    {
        var gen = from offsetDays in Gen.Choose(1, 365)
                  from durationDays in Gen.Choose(28, 90)
                  let start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(offsetDays)
                  let end = start.AddDays(durationDays)
                  select (start, end);

        return Arb.From(gen);
    }

    private static Arbitrary<(DateTime periodStart, DateTime periodEnd)> FuturePeriodEndArbitrary()
    {
        var gen = from offsetDays in Gen.Choose(1, 60)
                  from futureDays in Gen.Choose(1, 90)
                  let start = DateTime.UtcNow.AddDays(-offsetDays)
                  let end = DateTime.UtcNow.AddDays(futureDays)
                  select (start, end);

        return Arb.From(gen);
    }

    private static Arbitrary<(DateTime periodStart, DateTime periodEnd)> PastPeriodEndArbitrary()
    {
        var gen = from offsetDays in Gen.Choose(30, 365)
                  from pastDays in Gen.Choose(1, 29)
                  let start = DateTime.UtcNow.AddDays(-offsetDays)
                  let end = DateTime.UtcNow.AddDays(-pastDays)
                  select (start, end);

        return Arb.From(gen);
    }

    private static Arbitrary<string> LsSubscriptionIdArbitrary()
    {
        var gen = from num in Gen.Choose(100000, 999999)
                  select $"ls_sub_{num}";
        return Arb.From(gen);
    }

    private static Arbitrary<string> LsCustomerIdArbitrary()
    {
        var gen = from num in Gen.Choose(100000, 999999)
                  select num.ToString();
        return Arb.From(gen);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 4: Subscription creation maps to correct entity state
    // **Validates: Requirements 3.1, 3.2**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property SubscriptionCreated_Active_MapsToCorrectEntityState()
    {
        var gen = from dates in PeriodDatesArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  select (dates.periodStart, dates.periodEnd, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (periodStart, periodEnd, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateTrialingSubscription(spaceId, groupId);
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var payload = BuildSubscriptionCreatedPayload(
                subId, "active", long.Parse(custId), periodStart, periodEnd);
            var metadata = new Dictionary<string, string>
            {
                ["space_id"] = spaceId.ToString(),
                ["group_id"] = groupId.ToString()
            };

            var handler = new HandleSubscriptionCreatedCommandHandler(
                db, Substitute.For<ILogger<HandleSubscriptionCreatedCommandHandler>>());

            await handler.Handle(new HandleSubscriptionCreatedCommand(payload, metadata), CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            (updated.Status == SubscriptionStatus.Active)
                .Label("Status should be Active")
                .And((!string.IsNullOrEmpty(updated.LemonSqueezySubscriptionId))
                .Label("LemonSqueezySubscriptionId should be stored"))
                .And((!string.IsNullOrEmpty(updated.LemonSqueezyCustomerId))
                .Label("LemonSqueezyCustomerId should be stored"))
                .And((updated.CurrentPeriodStart == periodStart)
                .Label("CurrentPeriodStart should match payload"))
                .And((updated.CurrentPeriodEnd == periodEnd)
                .Label("CurrentPeriodEnd should match payload"));
        });
    }

    [Property(MaxTest = 100)]
    public Property SubscriptionCreated_OnTrial_MapsToCorrectEntityState()
    {
        var gen = from trialDays in Gen.Choose(1, 30)
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  let trialEndsAt = DateTime.UtcNow.AddDays(trialDays)
                  select (trialEndsAt, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (trialEndsAt, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateTrialingSubscription(spaceId, groupId);
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var payload = BuildSubscriptionCreatedPayload(
                subId, "on_trial", long.Parse(custId), trialEndsAt: trialEndsAt);
            var metadata = new Dictionary<string, string>
            {
                ["space_id"] = spaceId.ToString(),
                ["group_id"] = groupId.ToString()
            };

            var handler = new HandleSubscriptionCreatedCommandHandler(
                db, Substitute.For<ILogger<HandleSubscriptionCreatedCommandHandler>>());

            await handler.Handle(new HandleSubscriptionCreatedCommand(payload, metadata), CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            (updated.Status == SubscriptionStatus.Trialing)
                .Label("Status should be Trialing")
                .And((!string.IsNullOrEmpty(updated.LemonSqueezySubscriptionId))
                .Label("LemonSqueezySubscriptionId should be stored"))
                .And((updated.TrialEndsAt != null)
                .Label("TrialEndsAt should be set"));
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 5: Already-activated subscription ignores duplicate creation events
    // **Validates: Requirements 3.4**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property AlreadyActivated_IgnoresDuplicateCreationEvents()
    {
        var gen = from dates in PeriodDatesArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  select (dates.periodStart, dates.periodEnd, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (periodStart, periodEnd, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, periodStart, periodEnd);
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            // Capture state before duplicate event
            var originalStatus = subscription.Status;
            var originalLsSubId = subscription.LemonSqueezySubscriptionId;
            var originalPeriodStart = subscription.CurrentPeriodStart;
            var originalPeriodEnd = subscription.CurrentPeriodEnd;

            // Send a duplicate creation event
            var newPayload = BuildSubscriptionCreatedPayload(
                "ls_sub_different", "active", 999999,
                DateTime.UtcNow, DateTime.UtcNow.AddMonths(2));
            var metadata = new Dictionary<string, string>
            {
                ["space_id"] = spaceId.ToString(),
                ["group_id"] = groupId.ToString()
            };

            var handler = new HandleSubscriptionCreatedCommandHandler(
                db, Substitute.For<ILogger<HandleSubscriptionCreatedCommandHandler>>());

            await handler.Handle(new HandleSubscriptionCreatedCommand(newPayload, metadata), CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            // Entity should remain unchanged
            (updated.Status == originalStatus)
                .Label("Status should remain unchanged")
                .And((updated.LemonSqueezySubscriptionId == originalLsSubId)
                .Label("LemonSqueezySubscriptionId should remain unchanged"))
                .And((updated.CurrentPeriodStart == originalPeriodStart)
                .Label("CurrentPeriodStart should remain unchanged"))
                .And((updated.CurrentPeriodEnd == originalPeriodEnd)
                .Label("CurrentPeriodEnd should remain unchanged"));
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 6: Unrecognized creation statuses are skipped
    // **Validates: Requirements 3.5**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property UnrecognizedCreationStatus_IsSkipped()
    {
        var unrecognizedStatuses = Gen.Elements("paused", "pending", "unknown", "suspended", "refunded");

        var gen = from status in unrecognizedStatuses
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  select (status, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (status, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateTrialingSubscription(spaceId, groupId);
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var originalStatus = subscription.Status;
            var originalLsSubId = subscription.LemonSqueezySubscriptionId;

            var payload = BuildSubscriptionCreatedPayload(
                subId, status, long.Parse(custId));
            var metadata = new Dictionary<string, string>
            {
                ["space_id"] = spaceId.ToString(),
                ["group_id"] = groupId.ToString()
            };

            var handler = new HandleSubscriptionCreatedCommandHandler(
                db, Substitute.For<ILogger<HandleSubscriptionCreatedCommandHandler>>());

            await handler.Handle(new HandleSubscriptionCreatedCommand(payload, metadata), CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            (updated.Status == originalStatus)
                .Label("Status should remain unchanged")
                .And((updated.LemonSqueezySubscriptionId == originalLsSubId)
                .Label("LemonSqueezySubscriptionId should remain null"));
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 7: Unrecognized update statuses are skipped
    // **Validates: Requirements 4.3**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property UnrecognizedUpdateStatus_IsSkipped()
    {
        var unrecognizedStatuses = Gen.Elements("paused", "pending", "unknown", "suspended", "refunded");

        var gen = from status in unrecognizedStatuses
                  from dates in PeriodDatesArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  select (status, dates.periodStart, dates.periodEnd, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (status, periodStart, periodEnd, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, periodStart, periodEnd);
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var originalStatus = subscription.Status;
            var originalPeriodStart = subscription.CurrentPeriodStart;
            var originalPeriodEnd = subscription.CurrentPeriodEnd;

            var payload = BuildSubscriptionUpdatedPayload(
                subId, status, periodStart.AddDays(1), periodEnd.AddDays(1));

            var handler = new HandleSubscriptionUpdatedCommandHandler(
                db, Substitute.For<ILogger<HandleSubscriptionUpdatedCommandHandler>>());

            await handler.Handle(
                new HandleSubscriptionUpdatedCommand(payload, new Dictionary<string, string>()),
                CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            (updated.Status == originalStatus)
                .Label("Status should remain unchanged")
                .And((updated.CurrentPeriodStart == originalPeriodStart)
                .Label("CurrentPeriodStart should remain unchanged"))
                .And((updated.CurrentPeriodEnd == originalPeriodEnd)
                .Label("CurrentPeriodEnd should remain unchanged"));
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 8: Period dates are updated when they differ
    // **Validates: Requirements 4.4**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property PeriodDates_UpdatedWhenTheyDiffer()
    {
        var gen = from oldDates in PeriodDatesArbitrary().Generator
                  from newDates in PeriodDatesArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  where oldDates.periodStart != newDates.periodStart || oldDates.periodEnd != newDates.periodEnd
                  select (oldDates, newDates, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (oldDates, newDates, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, oldDates.periodStart, oldDates.periodEnd);
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var payload = BuildSubscriptionUpdatedPayload(
                subId, "active", newDates.periodStart, newDates.periodEnd);

            var handler = new HandleSubscriptionUpdatedCommandHandler(
                db, Substitute.For<ILogger<HandleSubscriptionUpdatedCommandHandler>>());

            await handler.Handle(
                new HandleSubscriptionUpdatedCommand(payload, new Dictionary<string, string>()),
                CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            (updated.CurrentPeriodStart == newDates.periodStart)
                .Label("CurrentPeriodStart should be updated to new value")
                .And((updated.CurrentPeriodEnd == newDates.periodEnd)
                .Label("CurrentPeriodEnd should be updated to new value"));
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 9: Transition to Active reactivates the group
    // **Validates: Requirements 4.5**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property TransitionToActive_ReactivatesGroup()
    {
        var previousStatuses = Gen.Elements(
            SubscriptionStatus.Trialing,
            SubscriptionStatus.PastDue,
            SubscriptionStatus.Canceled,
            SubscriptionStatus.Expired);

        var gen = from prevStatus in previousStatuses
                  from dates in PeriodDatesArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  select (prevStatus, dates.periodStart, dates.periodEnd, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (prevStatus, periodStart, periodEnd, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();

            // Create subscription in the previous status
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, periodStart, periodEnd);
            subscription.UpdateStatus(prevStatus);
            db.GroupSubscriptions.Add(subscription);

            // Create a deactivated group with matching Id
            var group = Group.Create(spaceId, null, "Test Group");
            typeof(Entity).GetProperty("Id")!.SetValue(group, groupId);
            group.Deactivate();
            db.Groups.Add(group);

            await db.SaveChangesAsync();

            var payload = BuildSubscriptionUpdatedPayload(
                subId, "active", periodStart, periodEnd);

            var handler = new HandleSubscriptionUpdatedCommandHandler(
                db, Substitute.For<ILogger<HandleSubscriptionUpdatedCommandHandler>>());

            await handler.Handle(
                new HandleSubscriptionUpdatedCommand(payload, new Dictionary<string, string>()),
                CancellationToken.None);

            var updatedGroup = await db.Groups.FirstAsync(g => g.Id == groupId);

            (updatedGroup.IsActive)
                .Label($"Group should be reactivated after transition from {prevStatus} to Active");
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 10: Cancellation sets status and timestamp
    // **Validates: Requirements 5.1**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property Cancellation_SetsStatusAndTimestamp()
    {
        var cancellableStatuses = Gen.Elements(
            SubscriptionStatus.Trialing,
            SubscriptionStatus.Active,
            SubscriptionStatus.PastDue);

        var gen = from status in cancellableStatuses
                  from dates in FuturePeriodEndArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  select (status, dates.periodStart, dates.periodEnd, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (status, periodStart, periodEnd, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, periodStart, periodEnd);
            subscription.UpdateStatus(status);
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var beforeCancel = DateTime.UtcNow;

            var payload = BuildSubscriptionCancelledPayload(subId);

            var handler = new HandleSubscriptionCancelledCommandHandler(
                db, Substitute.For<ILogger<HandleSubscriptionCancelledCommandHandler>>());

            await handler.Handle(
                new HandleSubscriptionCancelledCommand(payload, new Dictionary<string, string>()),
                CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            (updated.Status == SubscriptionStatus.Canceled)
                .Label("Status should be Canceled")
                .And((updated.CanceledAt != null)
                .Label("CanceledAt should not be null"))
                .And((updated.CanceledAt!.Value >= beforeCancel)
                .Label("CanceledAt should be >= time before cancel"));
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 11: Cancellation deactivates group if and only if period has ended
    // **Validates: Requirements 5.2, 5.3**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property Cancellation_DeactivatesGroup_IfPeriodEnded()
    {
        var gen = from dates in PastPeriodEndArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  select (dates.periodStart, dates.periodEnd, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (periodStart, periodEnd, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, periodStart, periodEnd);
            db.GroupSubscriptions.Add(subscription);

            var group = Group.Create(spaceId, null, "Test Group");
            typeof(Entity).GetProperty("Id")!.SetValue(group, groupId);
            db.Groups.Add(group);
            await db.SaveChangesAsync();

            var payload = BuildSubscriptionCancelledPayload(subId);

            var handler = new HandleSubscriptionCancelledCommandHandler(
                db, Substitute.For<ILogger<HandleSubscriptionCancelledCommandHandler>>());

            await handler.Handle(
                new HandleSubscriptionCancelledCommand(payload, new Dictionary<string, string>()),
                CancellationToken.None);

            var updatedGroup = await db.Groups.FirstAsync(g => g.Id == groupId);

            (!updatedGroup.IsActive)
                .Label("Group should be deactivated when period has ended");
        });
    }

    [Property(MaxTest = 100)]
    public Property Cancellation_LeavesGroupActive_IfPeriodNotEnded()
    {
        var gen = from dates in FuturePeriodEndArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  select (dates.periodStart, dates.periodEnd, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (periodStart, periodEnd, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, periodStart, periodEnd);
            db.GroupSubscriptions.Add(subscription);

            var group = Group.Create(spaceId, null, "Test Group");
            typeof(Entity).GetProperty("Id")!.SetValue(group, groupId);
            db.Groups.Add(group);
            await db.SaveChangesAsync();

            var payload = BuildSubscriptionCancelledPayload(subId);

            var handler = new HandleSubscriptionCancelledCommandHandler(
                db, Substitute.For<ILogger<HandleSubscriptionCancelledCommandHandler>>());

            await handler.Handle(
                new HandleSubscriptionCancelledCommand(payload, new Dictionary<string, string>()),
                CancellationToken.None);

            var updatedGroup = await db.Groups.FirstAsync(g => g.Id == groupId);

            (updatedGroup.IsActive)
                .Label("Group should remain active when period has not ended");
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 12: Already-canceled subscriptions ignore cancellation events
    // **Validates: Requirements 5.4**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property AlreadyCanceled_IgnoresCancellationEvents()
    {
        var terminalStatuses = Gen.Elements(SubscriptionStatus.Canceled, SubscriptionStatus.Expired);

        var gen = from status in terminalStatuses
                  from dates in PeriodDatesArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  select (status, dates.periodStart, dates.periodEnd, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (status, periodStart, periodEnd, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, periodStart, periodEnd);

            // Set to terminal status
            if (status == SubscriptionStatus.Canceled)
            {
                subscription.Cancel();
            }
            else
            {
                subscription.Cancel();
                subscription.Expire();
            }

            var originalCanceledAt = subscription.CanceledAt;
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var payload = BuildSubscriptionCancelledPayload(subId);

            var handler = new HandleSubscriptionCancelledCommandHandler(
                db, Substitute.For<ILogger<HandleSubscriptionCancelledCommandHandler>>());

            await handler.Handle(
                new HandleSubscriptionCancelledCommand(payload, new Dictionary<string, string>()),
                CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            (updated.Status == status)
                .Label($"Status should remain {status}")
                .And((updated.CanceledAt == originalCanceledAt)
                .Label("CanceledAt should remain unchanged"));
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 13: Payment success updates period and conditionally resets peak
    // **Validates: Requirements 6.1, 6.3**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property PaymentSuccess_UpdatesPeriod_ResetsWhenPeriodStartDiffers()
    {
        var gen = from oldDates in PeriodDatesArbitrary().Generator
                  from newDates in PeriodDatesArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  from peakCount in Gen.Choose(1, 50)
                  where oldDates.periodStart != newDates.periodStart
                  select (oldDates, newDates, subId, custId, peakCount);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (oldDates, newDates, subId, custId, peakCount) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, oldDates.periodStart, oldDates.periodEnd);
            // Set a peak member count
            subscription.UpdatePeakMemberCount(peakCount);
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var lsSubIdNumeric = long.Parse(subId.Replace("ls_sub_", ""));
            var payload = BuildPaymentSuccessPayload(lsSubIdNumeric, newDates.periodStart, newDates.periodEnd);

            var handler = new HandlePaymentSuccessCommandHandler(
                db, Substitute.For<ILogger<HandlePaymentSuccessCommandHandler>>());

            await handler.Handle(
                new HandlePaymentSuccessCommand(payload, new Dictionary<string, string>()),
                CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            (updated.CurrentPeriodStart == newDates.periodStart)
                .Label("CurrentPeriodStart should be updated")
                .And((updated.CurrentPeriodEnd == newDates.periodEnd)
                .Label("CurrentPeriodEnd should be updated"))
                .And((updated.PeakMemberCount == 0)
                .Label("PeakMemberCount should be reset to 0 when period start differs"));
        });
    }

    [Property(MaxTest = 100)]
    public Property PaymentSuccess_UpdatesPeriod_PreservesPeakWhenPeriodStartSame()
    {
        var gen = from dates in PeriodDatesArbitrary().Generator
                  from newEndOffset in Gen.Choose(1, 30)
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  from peakCount in Gen.Choose(1, 50)
                  let newEnd = dates.periodEnd.AddDays(newEndOffset)
                  select (dates.periodStart, dates.periodEnd, newEnd, subId, custId, peakCount);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (periodStart, oldEnd, newEnd, subId, custId, peakCount) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, periodStart, oldEnd);
            subscription.UpdatePeakMemberCount(peakCount);
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var lsSubIdNumeric = long.Parse(subId.Replace("ls_sub_", ""));
            // Same period start, different end
            var payload = BuildPaymentSuccessPayload(lsSubIdNumeric, periodStart, newEnd);

            var handler = new HandlePaymentSuccessCommandHandler(
                db, Substitute.For<ILogger<HandlePaymentSuccessCommandHandler>>());

            await handler.Handle(
                new HandlePaymentSuccessCommand(payload, new Dictionary<string, string>()),
                CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            (updated.CurrentPeriodStart == periodStart)
                .Label("CurrentPeriodStart should remain the same")
                .And((updated.CurrentPeriodEnd == newEnd)
                .Label("CurrentPeriodEnd should be updated"))
                .And((updated.PeakMemberCount == peakCount)
                .Label("PeakMemberCount should be preserved when period start is the same"));
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 14: Payment success transitions PastDue to Active
    // **Validates: Requirements 6.2**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property PaymentSuccess_TransitionsPastDueToActive()
    {
        var gen = from dates in PeriodDatesArbitrary().Generator
                  from newDates in PeriodDatesArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  select (dates, newDates, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (oldDates, newDates, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, oldDates.periodStart, oldDates.periodEnd);
            subscription.UpdateStatus(SubscriptionStatus.PastDue);
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var lsSubIdNumeric = long.Parse(subId.Replace("ls_sub_", ""));
            var payload = BuildPaymentSuccessPayload(lsSubIdNumeric, newDates.periodStart, newDates.periodEnd);

            var handler = new HandlePaymentSuccessCommandHandler(
                db, Substitute.For<ILogger<HandlePaymentSuccessCommandHandler>>());

            await handler.Handle(
                new HandlePaymentSuccessCommand(payload, new Dictionary<string, string>()),
                CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            (updated.Status == SubscriptionStatus.Active)
                .Label("Status should transition from PastDue to Active");
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 15: Test charges never modify subscriptions
    // **Validates: Requirements 8.5**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property TestCharges_NeverModifySubscriptions()
    {
        var eventTypes = Gen.Elements(
            "subscription_created", "subscription_updated",
            "subscription_cancelled", "subscription_payment_success");

        var gen = from eventType in eventTypes
                  from dates in PeriodDatesArbitrary().Generator
                  from subId in LsSubscriptionIdArbitrary().Generator
                  from custId in LsCustomerIdArbitrary().Generator
                  select (eventType, dates.periodStart, dates.periodEnd, subId, custId);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (eventType, periodStart, periodEnd, subId, custId) = tuple;
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            await using var db = CreateInMemoryDb();
            var subscription = CreateActiveSubscription(
                spaceId, groupId, subId, custId, periodStart, periodEnd);
            db.GroupSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var originalStatus = subscription.Status;
            var originalPeriodStart = subscription.CurrentPeriodStart;
            var originalPeriodEnd = subscription.CurrentPeriodEnd;
            var originalPeak = subscription.PeakMemberCount;

            // Metadata with test-charge flag
            var metadata = new Dictionary<string, string>
            {
                ["space_id"] = spaceId.ToString(),
                ["group_id"] = groupId.ToString(),
                ["charge_type"] = "test-charge"
            };

            var mediator = Substitute.For<IMediator>();
            var handler = new HandleWebhookCommandHandler(
                db, mediator, Substitute.For<ILogger<HandleWebhookCommandHandler>>());

            var eventId = Guid.NewGuid().ToString();
            await handler.Handle(
                new HandleWebhookCommand(eventId, eventType, "{}", metadata),
                CancellationToken.None);

            var updated = await db.GroupSubscriptions.FirstAsync(s => s.GroupId == groupId);

            // Verify no sub-handler was dispatched
            await mediator.DidNotReceive().Send(Arg.Any<IRequest>(), Arg.Any<CancellationToken>());

            (updated.Status == originalStatus)
                .Label("Status should remain unchanged for test charges")
                .And((updated.CurrentPeriodStart == originalPeriodStart)
                .Label("CurrentPeriodStart should remain unchanged for test charges"))
                .And((updated.CurrentPeriodEnd == originalPeriodEnd)
                .Label("CurrentPeriodEnd should remain unchanged for test charges"))
                .And((updated.PeakMemberCount == originalPeak)
                .Label("PeakMemberCount should remain unchanged for test charges"));
        });
    }
}
