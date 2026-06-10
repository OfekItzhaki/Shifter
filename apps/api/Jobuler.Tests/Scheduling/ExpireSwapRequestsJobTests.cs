using FluentAssertions;
using Jobuler.Application.Notifications;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class ExpireSwapRequestsJobTests
{
    [Fact]
    public async Task RunOnceAsync_ExpiresPendingSwapsAndNotifiesInitiators()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var pushSender = Substitute.For<IPushNotificationSender>();
        var services = new ServiceCollection()
            .AddScoped(_ => new AppDbContext(options))
            .AddSingleton(pushSender)
            .BuildServiceProvider();

        Guid spaceId;
        Guid groupId;
        Guid expiredSwapId;
        Guid activeSwapId;
        Guid acceptedSwapId;
        Guid initiatorUserId;

        await using (var db = new AppDbContext(options))
        {
            var seeded = SeedSwapData(db);
            spaceId = seeded.SpaceId;
            groupId = seeded.GroupId;
            expiredSwapId = seeded.ExpiredSwapId;
            activeSwapId = seeded.ActiveSwapId;
            acceptedSwapId = seeded.AcceptedSwapId;
            initiatorUserId = seeded.InitiatorUserId;
        }

        var job = new ExpireSwapRequestsJob(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExpireSwapRequestsJob>.Instance);

        await job.RunOnceAsync();

        await using var assertionDb = new AppDbContext(options);
        var expiredSwap = await assertionDb.SwapRequests.SingleAsync(s => s.Id == expiredSwapId);
        expiredSwap.Status.Should().Be(SwapRequestStatus.Expired);

        var activeSwap = await assertionDb.SwapRequests.SingleAsync(s => s.Id == activeSwapId);
        activeSwap.Status.Should().Be(SwapRequestStatus.Pending);

        var acceptedSwap = await assertionDb.SwapRequests.SingleAsync(s => s.Id == acceptedSwapId);
        acceptedSwap.Status.Should().Be(SwapRequestStatus.Accepted);

        var notification = await assertionDb.Notifications
            .SingleAsync(n => n.EventType == "self_service.swap_expired");
        notification.SpaceId.Should().Be(spaceId);
        notification.UserId.Should().Be(initiatorUserId);
        notification.MetadataJson.Should().Contain(expiredSwapId.ToString());
        notification.MetadataJson.Should().Contain(groupId.ToString());

        await pushSender.Received(1)
            .SendPushToUserAsync(initiatorUserId, spaceId, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    private static SeedResult SeedSwapData(AppDbContext db)
    {
        var spaceId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Operations", createdByUserId: ownerUserId);
        group.SetSchedulingMode(SchedulingMode.SelfService);
        db.Groups.Add(group);

        var cycle = SchedulingCycle.Create(
            spaceId,
            group.Id,
            startsAt: DateTime.UtcNow.AddDays(1),
            endsAt: DateTime.UtcNow.AddDays(8),
            requestWindowOpensAt: DateTime.UtcNow.AddDays(-2),
            requestWindowClosesAt: DateTime.UtcNow.AddHours(1));
        db.SchedulingCycles.Add(cycle);

        var task = GroupTask.Create(
            spaceId,
            group.Id,
            "Guard",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            shiftDurationMinutes: 480,
            requiredHeadcount: 1,
            burdenLevel: TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: ownerUserId);
        db.GroupTasks.Add(task);

        var initiatorUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var initiator = Person.Create(spaceId, "Initiator", linkedUserId: initiatorUserId);
        var target = Person.Create(spaceId, "Target", linkedUserId: targetUserId);
        db.People.AddRange(initiator, target);

        var expiredSwap = CreateSwap(db, spaceId, group.Id, cycle.Id, task.Id, initiator.Id, target.Id);
        db.Entry(expiredSwap).Property(s => s.ExpiresAt).CurrentValue = DateTime.UtcNow.AddMinutes(-5);

        var activeSwap = CreateSwap(db, spaceId, group.Id, cycle.Id, task.Id, initiator.Id, target.Id);
        db.Entry(activeSwap).Property(s => s.ExpiresAt).CurrentValue = DateTime.UtcNow.AddMinutes(30);

        var acceptedSwap = CreateSwap(db, spaceId, group.Id, cycle.Id, task.Id, initiator.Id, target.Id);
        db.Entry(acceptedSwap).Property(s => s.ExpiresAt).CurrentValue = DateTime.UtcNow.AddMinutes(-5);
        acceptedSwap.Accept();

        db.SaveChanges();

        return new SeedResult(
            spaceId,
            group.Id,
            expiredSwap.Id,
            activeSwap.Id,
            acceptedSwap.Id,
            initiatorUserId);
    }

    private static SwapRequest CreateSwap(
        AppDbContext db,
        Guid spaceId,
        Guid groupId,
        Guid cycleId,
        Guid taskId,
        Guid initiatorPersonId,
        Guid targetPersonId)
    {
        var initiatorSlot = CreateSlot(spaceId, groupId, taskId, cycleId, daysFromNow: 2);
        var targetSlot = CreateSlot(spaceId, groupId, taskId, cycleId, daysFromNow: 3);
        var initiatorRequest = ShiftRequest.Create(spaceId, initiatorSlot.Id, initiatorPersonId, groupId, cycleId);
        initiatorRequest.Approve();
        var targetRequest = ShiftRequest.Create(spaceId, targetSlot.Id, targetPersonId, groupId, cycleId);
        targetRequest.Approve();
        var swap = SwapRequest.Create(
            spaceId,
            groupId,
            initiatorPersonId,
            targetPersonId,
            initiatorRequest.Id,
            targetRequest.Id);

        db.ShiftSlots.AddRange(initiatorSlot, targetSlot);
        db.ShiftRequests.AddRange(initiatorRequest, targetRequest);
        db.SwapRequests.Add(swap);
        return swap;
    }

    private static ShiftSlot CreateSlot(
        Guid spaceId,
        Guid groupId,
        Guid taskId,
        Guid cycleId,
        int daysFromNow) =>
        ShiftSlot.Create(
            spaceId,
            groupId,
            taskId,
            shiftTemplateId: Guid.NewGuid(),
            schedulingCycleId: cycleId,
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow)),
            startTime: new TimeOnly(8, 0),
            endTime: new TimeOnly(16, 0),
            capacity: 1);

    private sealed record SeedResult(
        Guid SpaceId,
        Guid GroupId,
        Guid ExpiredSwapId,
        Guid ActiveSwapId,
        Guid AcceptedSwapId,
        Guid InitiatorUserId);
}
