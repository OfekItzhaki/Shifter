using FluentAssertions;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Scheduling.SelfService.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class WaitlistServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task JoinWaitlistAsync_RejectsSlotWithAvailableCapacity()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var person = Person.Create(spaceId, "Member", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);

        db.People.Add(person);
        db.ShiftSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.JoinWaitlistAsync(person.Id, slot.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("This shift still has available capacity. Request the shift directly instead.");
        (await db.WaitlistEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task JoinWaitlistAsync_RejectsMemberWithActiveRequestForSlot()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var assignedPerson = Person.Create(spaceId, "Assigned", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);
        var shiftRequest = ShiftRequest.Create(spaceId, slot.Id, assignedPerson.Id, groupId, cycleId);
        shiftRequest.Approve();
        slot.IncrementFillCount();

        db.People.Add(assignedPerson);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.JoinWaitlistAsync(assignedPerson.Id, slot.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("You already have an active request for this slot.");
        (await db.WaitlistEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task JoinWaitlistAsync_RejectsWhenRequestWindowIsClosed()
    {
        using var db = CreateDb();
        var (spaceId, groupId, _, taskId) = SeedBaseData(db);
        var utcNow = DateTime.UtcNow;
        var closedCycle = SchedulingCycle.Create(
            spaceId,
            groupId,
            startsAt: utcNow.AddDays(2),
            endsAt: utcNow.AddDays(9),
            requestWindowOpensAt: utcNow.AddDays(-3),
            requestWindowClosesAt: utcNow.AddDays(-1));
        var assignedPerson = Person.Create(spaceId, "Assigned", linkedUserId: Guid.NewGuid());
        var waitingPerson = Person.Create(spaceId, "Waiting", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, closedCycle.Id);
        var shiftRequest = ShiftRequest.Create(spaceId, slot.Id, assignedPerson.Id, groupId, closedCycle.Id);
        shiftRequest.Approve();
        slot.IncrementFillCount();

        db.SchedulingCycles.Add(closedCycle);
        db.People.AddRange(assignedPerson, waitingPerson);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.JoinWaitlistAsync(waitingPerson.Id, slot.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("The request window has closed.");
        (await db.WaitlistEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task JoinWaitlistAsync_AllowsDifferentMemberWhenSlotIsFull()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var assignedPerson = Person.Create(spaceId, "Assigned", linkedUserId: Guid.NewGuid());
        var waitingPerson = Person.Create(spaceId, "Waiting", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);
        var shiftRequest = ShiftRequest.Create(spaceId, slot.Id, assignedPerson.Id, groupId, cycleId);
        shiftRequest.Approve();
        slot.IncrementFillCount();

        db.People.AddRange(assignedPerson, waitingPerson);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.JoinWaitlistAsync(waitingPerson.Id, slot.Id);

        result.Success.Should().BeTrue();
        result.Position.Should().Be(1);
        var entry = await db.WaitlistEntries.SingleAsync();
        entry.PersonId.Should().Be(waitingPerson.Id);
        entry.Status.Should().Be(WaitlistEntryStatus.Waiting);
    }

    [Fact]
    public async Task AcceptWaitlistOffer_ApprovesShiftAndAcceptsEntry()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var offeredPerson = Person.Create(spaceId, "Offered", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);
        var entry = WaitlistEntry.Create(spaceId, slot.Id, offeredPerson.Id, position: 1);
        entry.Offer(DateTime.UtcNow.AddMinutes(30));

        db.People.Add(offeredPerson);
        db.ShiftSlots.Add(slot);
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var waitlistService = Substitute.For<IWaitlistService>();
        var pushSender = Substitute.For<IPushNotificationSender>();
        var handler = new AcceptWaitlistOfferCommandHandler(
            db,
            waitlistService,
            pushSender,
            NullLogger<AcceptWaitlistOfferCommandHandler>.Instance);

        var result = await handler.Handle(
            new AcceptWaitlistOfferCommand(spaceId, offeredPerson.Id, slot.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ShiftRequestId.Should().NotBeNull();

        var updatedEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == entry.Id);
        updatedEntry.Status.Should().Be(WaitlistEntryStatus.Accepted);

        var shiftRequest = await db.ShiftRequests.SingleAsync(r => r.Id == result.ShiftRequestId);
        shiftRequest.PersonId.Should().Be(offeredPerson.Id);
        shiftRequest.ShiftSlotId.Should().Be(slot.Id);
        shiftRequest.GroupId.Should().Be(groupId);
        shiftRequest.SchedulingCycleId.Should().Be(cycleId);
        shiftRequest.Status.Should().Be(ShiftRequestStatus.Approved);

        var updatedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == slot.Id);
        updatedSlot.CurrentFillCount.Should().Be(1);

        await waitlistService.DidNotReceiveWithAnyArgs()
            .ProcessSlotReleasedAsync(default, default);
        await pushSender.Received(1)
            .SendPushToUserAsync(offeredPerson.LinkedUserId!.Value, spaceId, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveWaitlistAsync_DeclinesActiveOfferAndOffersNextWaitingMember()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var offeredPerson = Person.Create(spaceId, "Offered", linkedUserId: Guid.NewGuid());
        var waitingPerson = Person.Create(spaceId, "Waiting", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);
        var offeredEntry = WaitlistEntry.Create(spaceId, slot.Id, offeredPerson.Id, position: 1);
        offeredEntry.Offer(DateTime.UtcNow.AddMinutes(30));
        var waitingEntry = WaitlistEntry.Create(spaceId, slot.Id, waitingPerson.Id, position: 2);

        db.People.AddRange(offeredPerson, waitingPerson);
        db.ShiftSlots.Add(slot);
        db.WaitlistEntries.AddRange(offeredEntry, waitingEntry);
        await db.SaveChangesAsync();

        var pushSender = Substitute.For<IPushNotificationSender>();
        var service = CreateService(db, pushSender);

        await service.LeaveWaitlistAsync(offeredPerson.Id, slot.Id);

        var updatedOfferedEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == offeredEntry.Id);
        updatedOfferedEntry.Status.Should().Be(WaitlistEntryStatus.Declined);

        var updatedWaitingEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == waitingEntry.Id);
        updatedWaitingEntry.Status.Should().Be(WaitlistEntryStatus.Offered);
        updatedWaitingEntry.OfferedAt.Should().NotBeNull();
        updatedWaitingEntry.ExpiresAt.Should().NotBeNull();
        updatedWaitingEntry.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        await pushSender.Received(1)
            .SendPushToUserAsync(waitingPerson.LinkedUserId!.Value, spaceId, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessExpiredOffersAsync_ExpiresOfferAndOffersNextWaitingMember()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var expiredPerson = Person.Create(spaceId, "Expired", linkedUserId: Guid.NewGuid());
        var waitingPerson = Person.Create(spaceId, "Waiting", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);
        var expiredEntry = WaitlistEntry.Create(spaceId, slot.Id, expiredPerson.Id, position: 1);
        expiredEntry.Offer(DateTime.UtcNow.AddMinutes(-5));
        var waitingEntry = WaitlistEntry.Create(spaceId, slot.Id, waitingPerson.Id, position: 2);

        db.People.AddRange(expiredPerson, waitingPerson);
        db.ShiftSlots.Add(slot);
        db.WaitlistEntries.AddRange(expiredEntry, waitingEntry);
        await db.SaveChangesAsync();

        var pushSender = Substitute.For<IPushNotificationSender>();
        var service = CreateService(db, pushSender);

        await service.ProcessExpiredOffersAsync();

        var updatedExpiredEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == expiredEntry.Id);
        updatedExpiredEntry.Status.Should().Be(WaitlistEntryStatus.Expired);

        var updatedWaitingEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == waitingEntry.Id);
        updatedWaitingEntry.Status.Should().Be(WaitlistEntryStatus.Offered);
        updatedWaitingEntry.OfferedAt.Should().NotBeNull();
        updatedWaitingEntry.ExpiresAt.Should().NotBeNull();
        updatedWaitingEntry.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var expiredNotification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.waitlist_offer_expired");
        expiredNotification.UserId.Should().Be(expiredPerson.LinkedUserId!.Value);
        expiredNotification.MetadataJson.Should().Contain(expiredEntry.Id.ToString());
        expiredNotification.MetadataJson.Should().Contain(slot.Id.ToString());

        await pushSender.Received(1)
            .SendPushToUserAsync(expiredPerson.LinkedUserId!.Value, spaceId, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await pushSender.Received(1)
            .SendPushToUserAsync(waitingPerson.LinkedUserId!.Value, spaceId, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    private static WaitlistService CreateService(AppDbContext db, IPushNotificationSender? pushSender = null) =>
        new(
            db,
            pushSender ?? Substitute.For<IPushNotificationSender>(),
            TimeProvider.System,
            NullLogger<WaitlistService>.Instance);

    private static (Guid spaceId, Guid groupId, Guid cycleId, Guid taskId) SeedBaseData(AppDbContext db)
    {
        var spaceId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Operations", createdByUserId: ownerUserId);
        group.SetSchedulingMode(SchedulingMode.SelfService);
        db.Groups.Add(group);

        var utcNow = DateTime.UtcNow;
        var cycle = SchedulingCycle.Create(
            spaceId,
            group.Id,
            startsAt: utcNow.AddDays(1),
            endsAt: utcNow.AddDays(8),
            requestWindowOpensAt: utcNow.AddDays(-2),
            requestWindowClosesAt: utcNow.AddHours(1));
        db.SchedulingCycles.Add(cycle);

        var task = GroupTask.Create(
            spaceId,
            group.Id,
            "Guard",
            utcNow,
            utcNow.AddDays(30),
            shiftDurationMinutes: 480,
            requiredHeadcount: 1,
            burdenLevel: TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: ownerUserId);
        db.GroupTasks.Add(task);
        db.SaveChanges();

        return (spaceId, group.Id, cycle.Id, task.Id);
    }

    private static ShiftSlot CreateSlot(
        Guid spaceId,
        Guid groupId,
        Guid taskId,
        Guid cycleId) =>
        ShiftSlot.Create(
            spaceId,
            groupId,
            taskId,
            shiftTemplateId: Guid.NewGuid(),
            schedulingCycleId: cycleId,
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            startTime: new TimeOnly(8, 0),
            endTime: new TimeOnly(16, 0),
            capacity: 1);
}
