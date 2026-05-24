// Feature: schedule-regeneration
// Property 9: Subscription gating
// For any group whose trial has expired and has no active subscription, a regeneration request
// SHALL be rejected with 402. For any group with active subscription or within trial, the request SHALL proceed.
// **Validates: Requirements 10.2, 10.3**

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Identity;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

/// <summary>
/// Represents the subscription states we want to generate for property testing.
/// </summary>
public enum SubscriptionScenario
{
    /// <summary>Active paid subscription (Status = Active)</summary>
    ActiveSubscription,
    /// <summary>Trial that has NOT expired yet (Status = Trialing, TrialEndsAt in the future)</summary>
    WithinTrial,
    /// <summary>Trial that HAS expired (Status = Trialing, TrialEndsAt in the past)</summary>
    ExpiredTrial,
    /// <summary>Subscription was canceled (Status = Canceled)</summary>
    CanceledSubscription,
    /// <summary>Subscription expired after cancellation (Status = Expired)</summary>
    ExpiredSubscription
}

public class SubscriptionGatingPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration BuildConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Solver:TimeoutSeconds"] = "30",
                ["Solver:StaleGracePeriodMinutes"] = "5"
            })
            .Build();
        return config;
    }

    private static ISolverJobQueue CreateMockQueue()
    {
        var queue = Substitute.For<ISolverJobQueue>();
        queue.EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return queue;
    }

    private static ITimezoneResolver CreateMockTimezoneResolver()
    {
        var resolver = Substitute.For<ITimezoneResolver>();
        resolver.Resolve(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new TimezoneResolution("Asia/Jerusalem", 120));
        return resolver;
    }

    /// <summary>
    /// Seeds a space with an owner user and a published version.
    /// Does NOT seed a subscription — the caller controls subscription state.
    /// </summary>
    private static async Task<(Guid SpaceId, Guid GroupId, Guid UserId)> SeedBaseScenario(AppDbContext db)
    {
        var user = User.Create("admin@test.com", "Admin", BCrypt.Net.BCrypt.HashPassword("Pass1234!", workFactor: 4));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var spaceId = Guid.NewGuid();
        var space = Space.Create("Test Space", user.Id);
        db.Entry(space).Property("Id").CurrentValue = spaceId;
        db.Spaces.Add(space);
        await db.SaveChangesAsync();

        var groupId = Guid.NewGuid();

        // Published version (required for the handler to proceed past subscription check)
        var version = ScheduleVersion.CreateDraft(spaceId, 1, null, null, user.Id);
        version.Publish(user.Id);
        db.ScheduleVersions.Add(version);
        await db.SaveChangesAsync();

        return (spaceId, groupId, user.Id);
    }

    /// <summary>
    /// Seeds a subscription with the given scenario state.
    /// </summary>
    private static async Task SeedSubscription(
        AppDbContext db, Guid spaceId, Guid groupId, SubscriptionScenario scenario, int trialDaysOffset)
    {
        var subscription = GroupSubscription.CreateTrial(spaceId, groupId, trialDays: 14);

        switch (scenario)
        {
            case SubscriptionScenario.ActiveSubscription:
                subscription.Activate(
                    "pro",
                    $"ls_sub_{Guid.NewGuid():N}",
                    $"ls_cust_{Guid.NewGuid():N}",
                    DateTime.UtcNow.AddDays(-30),
                    DateTime.UtcNow.AddDays(30));
                break;

            case SubscriptionScenario.WithinTrial:
                // Trial ends in the future (1 to 30 days from now)
                var futureTrialEnd = DateTime.UtcNow.AddDays(Math.Max(1, Math.Abs(trialDaysOffset) % 30 + 1));
                db.Entry(subscription).Property(nameof(GroupSubscription.TrialEndsAt)).CurrentValue = futureTrialEnd;
                break;

            case SubscriptionScenario.ExpiredTrial:
                // Trial ended in the past (1 to 365 days ago)
                var pastTrialEnd = DateTime.UtcNow.AddDays(-(Math.Abs(trialDaysOffset) % 365 + 1));
                db.Entry(subscription).Property(nameof(GroupSubscription.TrialEndsAt)).CurrentValue = pastTrialEnd;
                break;

            case SubscriptionScenario.CanceledSubscription:
                subscription.Activate(
                    "pro",
                    $"ls_sub_{Guid.NewGuid():N}",
                    $"ls_cust_{Guid.NewGuid():N}",
                    DateTime.UtcNow.AddDays(-60),
                    DateTime.UtcNow.AddDays(-1));
                subscription.Cancel();
                break;

            case SubscriptionScenario.ExpiredSubscription:
                subscription.Activate(
                    "pro",
                    $"ls_sub_{Guid.NewGuid():N}",
                    $"ls_cust_{Guid.NewGuid():N}",
                    DateTime.UtcNow.AddDays(-90),
                    DateTime.UtcNow.AddDays(-30));
                subscription.Cancel();
                subscription.Expire();
                break;
        }

        db.GroupSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Determines whether a given scenario should result in the handler rejecting with 402.
    /// </summary>
    private static bool ShouldReject(SubscriptionScenario scenario)
    {
        // The handler rejects when subscription exists AND !IsActive.
        // IsActive = Status == Active || (Status == Trialing && !IsTrialExpired)
        return scenario switch
        {
            SubscriptionScenario.ActiveSubscription => false,   // IsActive = true
            SubscriptionScenario.WithinTrial => false,          // IsActive = true (trial not expired)
            SubscriptionScenario.ExpiredTrial => true,          // IsActive = false (trial expired)
            SubscriptionScenario.CanceledSubscription => true,  // IsActive = false (status = Canceled)
            SubscriptionScenario.ExpiredSubscription => true,   // IsActive = false (status = Expired)
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }

    // ── FsCheck Generators ────────────────────────────────────────────────────

    /// <summary>
    /// Generates subscription scenarios that should be REJECTED (402).
    /// </summary>
    private static Arbitrary<(SubscriptionScenario Scenario, int DaysOffset)> RejectedScenariosArbitrary()
    {
        var scenarios = new[] { SubscriptionScenario.ExpiredTrial, SubscriptionScenario.CanceledSubscription, SubscriptionScenario.ExpiredSubscription };
        var gen = from scenario in Gen.Elements(scenarios)
                  from daysOffset in Gen.Choose(1, 365)
                  select (scenario, daysOffset);
        return Arb.From(gen);
    }

    /// <summary>
    /// Generates subscription scenarios that should PROCEED (active or within trial).
    /// </summary>
    private static Arbitrary<(SubscriptionScenario Scenario, int DaysOffset)> AllowedScenariosArbitrary()
    {
        var scenarios = new[] { SubscriptionScenario.ActiveSubscription, SubscriptionScenario.WithinTrial };
        var gen = from scenario in Gen.Elements(scenarios)
                  from daysOffset in Gen.Choose(1, 30)
                  select (scenario, daysOffset);
        return Arb.From(gen);
    }

    // ── Property 9a: Expired/inactive subscription → 402 rejection ────────────
    // For any group whose trial has expired and has no active subscription,
    // a regeneration request SHALL be rejected with 402.
    // **Validates: Requirements 10.2**

    [Property(MaxTest = 100)]
    public Property InactiveSubscription_RejectsWithPaymentRequired()
    {
        return Prop.ForAll(RejectedScenariosArbitrary(), input =>
        {
            return InactiveSubscriptionTestAsync(input.Scenario, input.DaysOffset).GetAwaiter().GetResult();
        });
    }

    private async Task<bool> InactiveSubscriptionTestAsync(SubscriptionScenario scenario, int daysOffset)
    {
        // Arrange
        var db = CreateDb();
        var (spaceId, groupId, userId) = await SeedBaseScenario(db);
        await SeedSubscription(db, spaceId, groupId, scenario, daysOffset);

        var handler = new TriggerRegenerationCommandHandler(
            db,
            CreateMockQueue(),
            CreateMockTimezoneResolver(),
            BuildConfiguration());

        var command = new TriggerRegenerationCommand(spaceId, groupId, userId);

        // Act & Assert — should throw PaymentRequiredException
        try
        {
            await handler.Handle(command, CancellationToken.None);
            return false; // Should have thrown
        }
        catch (PaymentRequiredException)
        {
            // Verify no ScheduleRun was created
            var runCount = await db.ScheduleRuns
                .CountAsync(r => r.SpaceId == spaceId && r.GroupId == groupId);
            return runCount == 0;
        }
        catch
        {
            return false; // Wrong exception type
        }
    }

    // ── Property 9b: Active subscription or within trial → request proceeds ───
    // For any group with active subscription or within trial, the request SHALL proceed.
    // **Validates: Requirements 10.3**

    [Property(MaxTest = 100)]
    public Property ActiveSubscription_AllowsRegenerationToProceed()
    {
        return Prop.ForAll(AllowedScenariosArbitrary(), input =>
        {
            return ActiveSubscriptionTestAsync(input.Scenario, input.DaysOffset).GetAwaiter().GetResult();
        });
    }

    private async Task<bool> ActiveSubscriptionTestAsync(SubscriptionScenario scenario, int daysOffset)
    {
        // Arrange
        var db = CreateDb();
        var (spaceId, groupId, userId) = await SeedBaseScenario(db);
        await SeedSubscription(db, spaceId, groupId, scenario, daysOffset);

        var handler = new TriggerRegenerationCommandHandler(
            db,
            CreateMockQueue(),
            CreateMockTimezoneResolver(),
            BuildConfiguration());

        var command = new TriggerRegenerationCommand(spaceId, groupId, userId);

        // Act — should succeed (create a run)
        try
        {
            var runId = await handler.Handle(command, CancellationToken.None);

            // Assert: A ScheduleRun was created
            var run = await db.ScheduleRuns.FindAsync(runId);
            if (run is null) return false;
            if (run.Status != ScheduleRunStatus.Queued) return false;
            if (run.TriggerType != ScheduleRunTrigger.Regeneration) return false;
            if (run.GroupId != groupId) return false;
            if (run.SpaceId != spaceId) return false;

            return true;
        }
        catch (PaymentRequiredException)
        {
            return false; // Should NOT have thrown for active subscriptions
        }
    }
}
