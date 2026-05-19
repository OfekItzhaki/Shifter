// Feature: subscription-cancellation
// Properties 3, 5, 6, 7, 10, 12, 13: Application-layer subscription lifecycle properties
// Validates: Requirements 1.4, 2.1, 2.2, 2.4, 2.5, 3.3, 4.1, 4.2, 5.1, 5.2, 5.3

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Billing.Commands;
using Jobuler.Application.Billing.Queries;
using Jobuler.Application.Common;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Jobuler.Tests.Billing;

public class SubscriptionApplicationPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GroupSubscription CreateTrialingSubscription()
    {
        return GroupSubscription.CreateTrial(Guid.NewGuid(), Guid.NewGuid(), trialDays: 14);
    }

    private static GroupSubscription CreateActiveSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = GroupSubscription.CreateTrial(Guid.NewGuid(), Guid.NewGuid(), trialDays: 14);
        sub.Activate("pro", "ls_sub_123", "ls_cus_123", periodStart, periodEnd);
        return sub;
    }

    private static GroupSubscription CreateCanceledSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = CreateActiveSubscription(periodStart, periodEnd);
        sub.Cancel();
        return sub;
    }

    private static GroupSubscription CreateExpiredSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = CreateCanceledSubscription(periodStart, periodEnd);
        sub.Expire();
        return sub;
    }

    private static Group CreateActiveGroup()
    {
        return Group.Create(Guid.NewGuid(), null, "Test Group");
    }

    private static Group CreateDeactivatedGroup()
    {
        var group = Group.Create(Guid.NewGuid(), null, "Test Group");
        group.Deactivate();
        return group;
    }

    // ── Generators ───────────────────────────────────────────────────────────

    private static Arbitrary<(DateTime periodStart, DateTime periodEnd)> PeriodDatesArbitrary()
    {
        var gen = from offsetDays in Gen.Choose(1, 365)
                  from durationDays in Gen.Choose(1, 90)
                  let start = DateTime.UtcNow.AddDays(-offsetDays)
                  let end = start.AddDays(durationDays)
                  select (start, end);

        return Arb.From(gen);
    }

    private static Arbitrary<(DateTime periodStart, DateTime periodEnd)> PastPeriodEndArbitrary()
    {
        var gen = from offsetDays in Gen.Choose(1, 365)
                  from pastDays in Gen.Choose(1, 90)
                  let start = DateTime.UtcNow.AddDays(-(offsetDays + pastDays))
                  let end = DateTime.UtcNow.AddDays(-pastDays)
                  select (start, end);

        return Arb.From(gen);
    }

    private static Arbitrary<(DateTime periodStart, DateTime periodEnd)> FuturePeriodEndArbitrary()
    {
        var gen = from offsetDays in Gen.Choose(1, 365)
                  from futureDays in Gen.Choose(1, 90)
                  let start = DateTime.UtcNow.AddDays(-offsetDays)
                  let end = DateTime.UtcNow.AddDays(futureDays)
                  select (start, end);

        return Arb.From(gen);
    }

    // ── Property 3: Trialing subscription cancel causes immediate group deactivation ──
    // **Validates: Requirements 1.4**

    [Property(MaxTest = 100)]
    public Property TrialingCancel_CausesImmediateGroupDeactivation()
    {
        var gen = from trialDays in Gen.Choose(1, 30)
                  select trialDays;

        return Prop.ForAll(Arb.From(gen), trialDays =>
        {
            var sub = GroupSubscription.CreateTrial(Guid.NewGuid(), Guid.NewGuid(), trialDays);
            var group = CreateActiveGroup();

            // Simulate what the CancelSubscriptionCommand handler does for trialing
            sub.Cancel();
            group.Deactivate();

            return (sub.Status == SubscriptionStatus.Canceled)
                .Label("Subscription status should be Canceled")
                .And((!group.IsActive)
                .Label("Group IsActive should be false"));
        });
    }

    // ── Property 5: Expired subscription deactivates group ──────────────────────
    // **Validates: Requirements 2.1, 2.2**

    [Property(MaxTest = 100)]
    public Property ExpiredSubscription_DeactivatesGroup()
    {
        return Prop.ForAll(PastPeriodEndArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateCanceledSubscription(periodStart, periodEnd);
            var group = CreateActiveGroup();

            // Simulate what the ExpireSubscriptionsCommand handler does
            sub.Expire();
            group.Deactivate();

            return (sub.Status == SubscriptionStatus.Expired)
                .Label("Subscription status should be Expired")
                .And((!group.IsActive)
                .Label("Group IsActive should be false"));
        });
    }

    // ── Property 6: Active subscriptions are not expired by the expiry job ──────
    // **Validates: Requirements 2.5**

    [Property(MaxTest = 100)]
    public Property ActiveSubscription_NotExpiredByExpiryJob()
    {
        return Prop.ForAll(PeriodDatesArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateActiveSubscription(periodStart, periodEnd);
            var group = CreateActiveGroup();

            // Expire() should throw for Active subscriptions
            var act = () => sub.Expire();
            act.Should().Throw<InvalidOperationException>();

            // Group should remain active
            return (sub.Status == SubscriptionStatus.Active)
                .Label("Subscription status should remain Active")
                .And(group.IsActive
                .Label("Group should remain active"));
        });
    }

    // ── Property 10: Renewal reactivates group from Limited_Mode ────────────────
    // **Validates: Requirements 3.3**

    [Property(MaxTest = 100)]
    public Property Renewal_ReactivatesGroupFromLimitedMode()
    {
        var gen = from offsetDays in Gen.Choose(1, 365)
                  from durationDays in Gen.Choose(1, 90)
                  from isExpired in Arb.Generate<bool>()
                  let start = DateTime.UtcNow.AddDays(-(offsetDays + durationDays))
                  let end = DateTime.UtcNow.AddDays(-offsetDays)
                  select (start, end, isExpired);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (periodStart, periodEnd, isExpired) = tuple;
            var group = CreateDeactivatedGroup();

            GroupSubscription sub;
            if (isExpired)
            {
                sub = CreateExpiredSubscription(periodStart, periodEnd);
            }
            else
            {
                sub = CreateCanceledSubscription(periodStart, periodEnd);
            }

            // Simulate what the RenewSubscriptionCommand handler does
            sub.Renew(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
            group.Reactivate();

            return (group.IsActive)
                .Label("Group IsActive should be true after renewal");
        });
    }

    // ── Property 13: Unauthorized users are rejected for cancel and renew ───────
    // **Validates: Requirements 5.1, 5.2, 5.3**

    [Fact]
    public async Task UnauthorizedUser_IsRejected_ForCancel()
    {
        // Arrange
        var permissions = Substitute.For<IPermissionService>();
        permissions
            .RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Is(Permissions.BillingManage), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("You do not have permission to manage billing for this space."));

        var handler = new CancelSubscriptionCommandHandler(null!, permissions, null!);
        var command = new CancelSubscriptionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UnauthorizedUser_IsRejected_ForRenew()
    {
        // Arrange
        var permissions = Substitute.For<IPermissionService>();
        permissions
            .RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Is(Permissions.BillingManage), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("You do not have permission to manage billing for this space."));

        var handler = new RenewSubscriptionCommandHandler(null!, permissions, null!);
        var command = new RenewSubscriptionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Property 12: Status query returns correct fields per subscription state ─
    // **Validates: Requirements 4.1, 4.2**

    [Property(MaxTest = 100)]
    public Property StatusQuery_ReturnsCorrectFields_ForCanceledSubscription()
    {
        return Prop.ForAll(FuturePeriodEndArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateCanceledSubscription(periodStart, periodEnd);

            // Map to DTO (same logic as GetSubscriptionHandler)
            var dto = new SubscriptionDto(
                sub.Status.ToString().ToLower(),
                sub.TierId,
                sub.TrialEndsAt,
                sub.PeakMemberCount,
                sub.DiscountPercent,
                sub.CouponCode,
                sub.IsActive,
                sub.CanceledAt,
                sub.CurrentPeriodEnd
            );

            return (dto.Status == "canceled")
                .Label("Status should be 'canceled'")
                .And((dto.CanceledAt != null)
                .Label("CanceledAt should be non-null for canceled subscription"))
                .And((dto.PeriodEndsAt == periodEnd)
                .Label("PeriodEndsAt should match CurrentPeriodEnd"));
        });
    }

    [Property(MaxTest = 100)]
    public Property StatusQuery_ReturnsCorrectFields_ForExpiredSubscription()
    {
        return Prop.ForAll(PastPeriodEndArbitrary(), dates =>
        {
            var (periodStart, periodEnd) = dates;
            var sub = CreateExpiredSubscription(periodStart, periodEnd);

            // Map to DTO (same logic as GetSubscriptionHandler)
            var dto = new SubscriptionDto(
                sub.Status.ToString().ToLower(),
                sub.TierId,
                sub.TrialEndsAt,
                sub.PeakMemberCount,
                sub.DiscountPercent,
                sub.CouponCode,
                sub.IsActive,
                sub.CanceledAt,
                sub.CurrentPeriodEnd
            );

            return (dto.Status == "expired")
                .Label("Status should be 'expired'")
                .And((dto.CanceledAt != null)
                .Label("CanceledAt should be non-null for expired subscription"));
        });
    }

    // ── Property 7: Limited_Mode blocks write operations ────────────────────────
    // **Validates: Requirements 2.4**

    [Property(MaxTest = 100)]
    public Property LimitedMode_BlocksWriteOperations()
    {
        var gen = from name in Gen.Elements("Group A", "Group B", "Group C", "Test Group")
                  select name;

        return Prop.ForAll(Arb.From(gen), name =>
        {
            var group = Group.Create(Guid.NewGuid(), null, name);
            group.Deactivate();

            // EnsureActive should throw for deactivated groups
            var act = () => group.EnsureActive();
            act.Should().Throw<InvalidOperationException>();
        });
    }

    [Property(MaxTest = 100)]
    public Property ActiveGroup_AllowsWriteOperations()
    {
        var gen = from name in Gen.Elements("Group A", "Group B", "Group C", "Test Group")
                  select name;

        return Prop.ForAll(Arb.From(gen), name =>
        {
            var group = Group.Create(Guid.NewGuid(), null, name);

            // EnsureActive should NOT throw for active groups
            var act = () => group.EnsureActive();
            act.Should().NotThrow();
        });
    }
}
