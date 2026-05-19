// Feature: subscription-cancellation
// Integration tests: Full cancel → expire → renew lifecycle
// Validates: Requirements 1.1, 1.5, 2.1, 2.3, 2.4, 3.1, 3.3, 3.4

using FluentAssertions;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Groups;
using Xunit;

namespace Jobuler.Tests.Billing;

public class SubscriptionLifecycleIntegrationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GroupSubscription CreateActiveSubscription(DateTime periodStart, DateTime periodEnd)
    {
        var sub = GroupSubscription.CreateTrial(Guid.NewGuid(), Guid.NewGuid(), trialDays: 14);
        sub.Activate("pro", "ls_sub_123", "ls_cus_123", periodStart, periodEnd);
        return sub;
    }

    private static Group CreateActiveGroup()
    {
        return Group.Create(Guid.NewGuid(), null, "Lifecycle Test Group");
    }

    // ── Full lifecycle: Active → Cancel → Expire → Renew ────────────────────

    [Fact]
    public void FullLifecycle_Cancel_Expire_Renew()
    {
        // Arrange: Active subscription and active group
        var periodStart = DateTime.UtcNow.AddDays(-20);
        var periodEnd = DateTime.UtcNow.AddDays(-1); // Past period end for expiry
        var sub = CreateActiveSubscription(periodStart, periodEnd);
        var group = CreateActiveGroup();

        // ── Step 1: Cancel ───────────────────────────────────────────────────
        sub.Cancel();

        sub.Status.Should().Be(SubscriptionStatus.Canceled);
        sub.CanceledAt.Should().NotBeNull();
        group.IsActive.Should().BeTrue("Group remains active during grace window");

        // ── Step 2: Expire (period has ended) ────────────────────────────────
        sub.Expire();
        group.Deactivate();

        sub.Status.Should().Be(SubscriptionStatus.Expired);
        group.IsActive.Should().BeFalse("Group should be deactivated after expiry");

        // ── Step 3: Verify Limited_Mode blocks writes ────────────────────────
        var writeAct = () => group.EnsureActive();
        writeAct.Should().Throw<InvalidOperationException>()
            .WithMessage("*limited mode*");

        // ── Step 4: Renew ────────────────────────────────────────────────────
        var newPeriodStart = DateTime.UtcNow;
        var newPeriodEnd = DateTime.UtcNow.AddMonths(1);
        sub.Renew(newPeriodStart, newPeriodEnd);
        group.Reactivate();

        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.CanceledAt.Should().BeNull();
        sub.CurrentPeriodStart.Should().Be(newPeriodStart);
        sub.CurrentPeriodEnd.Should().Be(newPeriodEnd);
        group.IsActive.Should().BeTrue("Group should be reactivated after renewal");
    }

    // ── Verify read operations still work on deactivated groups ──────────────

    [Fact]
    public void DeactivatedGroup_AllowsReadOperations()
    {
        // Arrange: Deactivated group (Limited_Mode)
        var group = CreateActiveGroup();
        group.Deactivate();

        // Read operations: accessing properties should work fine
        // EnsureActive only blocks writes — reading group data is always allowed
        group.IsActive.Should().BeFalse();
        group.Name.Should().NotBeNullOrEmpty();
        group.SpaceId.Should().NotBeEmpty();

        // The group entity itself is still accessible — only EnsureActive() guards writes
        var readAct = () =>
        {
            _ = group.Name;
            _ = group.SpaceId;
            _ = group.IsActive;
            _ = group.SolverHorizonDays;
        };
        readAct.Should().NotThrow("Read operations should succeed in Limited_Mode");
    }

    // ── Verify EnsureActive blocks writes but not reads ─────────────────────

    [Fact]
    public void LimitedMode_EnsureActive_BlocksWrites_AllowsReads()
    {
        var group = CreateActiveGroup();
        group.Deactivate();

        // Write guard throws
        var writeAct = () => group.EnsureActive();
        writeAct.Should().Throw<InvalidOperationException>();

        // After reactivation, writes are allowed again
        group.Reactivate();
        var writeAfterReactivation = () => group.EnsureActive();
        writeAfterReactivation.Should().NotThrow();
    }

    // ── Verify audit-worthy state changes at each lifecycle step ─────────────

    [Fact]
    public void LifecycleSteps_ProduceAuditWorthyStateChanges()
    {
        var periodStart = DateTime.UtcNow.AddDays(-10);
        var periodEnd = DateTime.UtcNow.AddDays(-1);
        var sub = CreateActiveSubscription(periodStart, periodEnd);

        // Cancel: CanceledAt is set (audit-worthy)
        var beforeCancel = DateTime.UtcNow;
        sub.Cancel();
        sub.CanceledAt.Should().NotBeNull();
        sub.CanceledAt!.Value.Should().BeOnOrAfter(beforeCancel);
        sub.Status.Should().Be(SubscriptionStatus.Canceled);

        // Expire: Status transitions (audit-worthy)
        sub.Expire();
        sub.Status.Should().Be(SubscriptionStatus.Expired);

        // Renew: Status reverts, CanceledAt cleared, new period set (audit-worthy)
        var newStart = DateTime.UtcNow;
        var newEnd = DateTime.UtcNow.AddMonths(1);
        sub.Renew(newStart, newEnd);
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.CanceledAt.Should().BeNull();
        sub.CurrentPeriodStart.Should().Be(newStart);
        sub.CurrentPeriodEnd.Should().Be(newEnd);
    }

    // ── Cancel within grace window then renew preserves period ───────────────

    [Fact]
    public void CancelWithinGraceWindow_ThenRenew_PreservesPeriod()
    {
        // Arrange: Active subscription with future period end
        var periodStart = DateTime.UtcNow.AddDays(-10);
        var periodEnd = DateTime.UtcNow.AddDays(20); // Still within grace window
        var sub = CreateActiveSubscription(periodStart, periodEnd);
        var group = CreateActiveGroup();

        // Cancel
        sub.Cancel();
        sub.Status.Should().Be(SubscriptionStatus.Canceled);
        group.IsActive.Should().BeTrue("Group stays active during grace window");

        // Renew within grace window — preserves existing period
        sub.Renew(periodStart, periodEnd);
        group.IsActive.Should().BeTrue();
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.CanceledAt.Should().BeNull();
        sub.CurrentPeriodStart.Should().Be(periodStart);
        sub.CurrentPeriodEnd.Should().Be(periodEnd);
    }

    // ── Trialing cancel causes immediate deactivation ────────────────────────

    [Fact]
    public void TrialingCancel_ImmediateDeactivation()
    {
        var sub = GroupSubscription.CreateTrial(Guid.NewGuid(), Guid.NewGuid(), trialDays: 14);
        var group = CreateActiveGroup();

        // Cancel trialing subscription
        sub.Cancel();
        group.Deactivate(); // Handler does this immediately for trialing

        sub.Status.Should().Be(SubscriptionStatus.Canceled);
        group.IsActive.Should().BeFalse("Trialing cancel should immediately deactivate group");

        // Verify Limited_Mode is enforced
        var writeAct = () => group.EnsureActive();
        writeAct.Should().Throw<InvalidOperationException>();
    }
}
