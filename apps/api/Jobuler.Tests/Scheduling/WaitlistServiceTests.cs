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
using Microsoft.EntityFrameworkCore.Diagnostics;
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
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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
    public async Task JoinWaitlistAsync_RejectsClosedSlot()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var person = Person.Create(spaceId, "Member", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);
        slot.IncrementFillCount();
        slot.Close();

        db.People.Add(person);
        db.ShiftSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.JoinWaitlistAsync(person.Id, slot.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("The shift slot is no longer open.");
        (await db.WaitlistEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task JoinWaitlistAsync_RejectsStartedSlot()
    {
        using var db = CreateDb();
        var fixedNow = new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var person = Person.Create(spaceId, "Member", linkedUserId: Guid.NewGuid());
        var slot = ShiftSlot.Create(
            spaceId,
            groupId,
            taskId,
            shiftTemplateId: Guid.NewGuid(),
            schedulingCycleId: cycleId,
            date: DateOnly.FromDateTime(fixedNow.UtcDateTime),
            startTime: new TimeOnly(8, 0),
            endTime: new TimeOnly(16, 0),
            capacity: 1);
        slot.IncrementFillCount();

        db.People.Add(person);
        db.ShiftSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = CreateService(db, timeProvider: new FixedTimeProvider(fixedNow));

        var result = await service.JoinWaitlistAsync(person.Id, slot.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("The shift has already started.");
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
        var notificationService = Substitute.For<INotificationService>();
        var handler = new AcceptWaitlistOfferCommandHandler(
            db,
            waitlistService,
            pushSender,
            notificationService,
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
        await notificationService.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.waitlist_accepted",
            "Waitlist Offer Accepted",
            Arg.Is<string>(body => body.Contains("Offered") && body.Contains("Guard")),
            Arg.Is<string>(metadata => metadata.Contains(result.ShiftRequestId!.Value.ToString()) && metadata.Contains(slot.Id.ToString())),
            groupId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptWaitlistOffer_RejectsWhenOfferWouldOverlapApprovedShift()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var offeredPerson = Person.Create(spaceId, "Offered", linkedUserId: Guid.NewGuid());
        var shiftDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var existingSlot = CreateSlot(spaceId, groupId, taskId, cycleId, shiftDate, new TimeOnly(10, 0), new TimeOnly(14, 0));
        var offeredSlot = CreateSlot(spaceId, groupId, taskId, cycleId, shiftDate, new TimeOnly(12, 0), new TimeOnly(16, 0));
        var existingRequest = ShiftRequest.Create(spaceId, existingSlot.Id, offeredPerson.Id, groupId, cycleId);
        existingRequest.Approve();
        existingSlot.IncrementFillCount();
        var entry = WaitlistEntry.Create(spaceId, offeredSlot.Id, offeredPerson.Id, position: 1);
        entry.Offer(DateTime.UtcNow.AddMinutes(30));

        db.People.Add(offeredPerson);
        db.ShiftSlots.AddRange(existingSlot, offeredSlot);
        db.ShiftRequests.Add(existingRequest);
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var waitlistService = Substitute.For<IWaitlistService>();
        var pushSender = Substitute.For<IPushNotificationSender>();
        var notificationService = Substitute.For<INotificationService>();
        var handler = new AcceptWaitlistOfferCommandHandler(
            db,
            waitlistService,
            pushSender,
            notificationService,
            NullLogger<AcceptWaitlistOfferCommandHandler>.Instance);

        var result = await handler.Handle(
            new AcceptWaitlistOfferCommand(spaceId, offeredPerson.Id, offeredSlot.Id),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("This shift overlaps with an existing approved shift.");
        result.ShiftRequestId.Should().BeNull();

        var updatedEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == entry.Id);
        updatedEntry.Status.Should().Be(WaitlistEntryStatus.Removed);
        (await db.ShiftRequests.CountAsync()).Should().Be(1);

        var updatedOfferedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == offeredSlot.Id);
        updatedOfferedSlot.CurrentFillCount.Should().Be(0);

        await waitlistService.Received(1)
            .ProcessSlotReleasedAsync(offeredSlot.Id, Arg.Any<CancellationToken>());
        await pushSender.DidNotReceiveWithAnyArgs()
            .SendPushToUserAsync(default, default, default!, default);
        await notificationService.DidNotReceiveWithAnyArgs()
            .NotifySpaceAdminsAsync(default, default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task AcceptWaitlistOffer_RejectsWhenOfferWouldViolateRestWindow()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var offeredPerson = Person.Create(spaceId, "Offered", linkedUserId: Guid.NewGuid());
        var shiftDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var existingSlot = CreateSlot(spaceId, groupId, taskId, cycleId, shiftDate, new TimeOnly(8, 0), new TimeOnly(12, 0));
        var offeredSlot = CreateSlot(spaceId, groupId, taskId, cycleId, shiftDate, new TimeOnly(18, 0), new TimeOnly(22, 0));
        var existingRequest = ShiftRequest.Create(spaceId, existingSlot.Id, offeredPerson.Id, groupId, cycleId);
        existingRequest.Approve();
        existingSlot.IncrementFillCount();
        var entry = WaitlistEntry.Create(spaceId, offeredSlot.Id, offeredPerson.Id, position: 1);
        entry.Offer(DateTime.UtcNow.AddMinutes(30));

        db.People.Add(offeredPerson);
        db.ShiftSlots.AddRange(existingSlot, offeredSlot);
        db.ShiftRequests.Add(existingRequest);
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var waitlistService = Substitute.For<IWaitlistService>();
        var pushSender = Substitute.For<IPushNotificationSender>();
        var notificationService = Substitute.For<INotificationService>();
        var handler = new AcceptWaitlistOfferCommandHandler(
            db,
            waitlistService,
            pushSender,
            notificationService,
            NullLogger<AcceptWaitlistOfferCommandHandler>.Instance);

        var result = await handler.Handle(
            new AcceptWaitlistOfferCommand(spaceId, offeredPerson.Id, offeredSlot.Id),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("This shift does not leave enough rest time after an existing approved shift.");
        result.ShiftRequestId.Should().BeNull();

        var updatedEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == entry.Id);
        updatedEntry.Status.Should().Be(WaitlistEntryStatus.Removed);
        (await db.ShiftRequests.CountAsync()).Should().Be(1);

        var updatedOfferedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == offeredSlot.Id);
        updatedOfferedSlot.CurrentFillCount.Should().Be(0);

        await waitlistService.Received(1)
            .ProcessSlotReleasedAsync(offeredSlot.Id, Arg.Any<CancellationToken>());
        await pushSender.DidNotReceiveWithAnyArgs()
            .SendPushToUserAsync(default, default, default!, default);
        await notificationService.DidNotReceiveWithAnyArgs()
            .NotifySpaceAdminsAsync(default, default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task AcceptWaitlistOffer_RejectsStaleOffer_WhenSlotNoLongerHasCapacity()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var assignedPerson = Person.Create(spaceId, "Assigned", linkedUserId: Guid.NewGuid());
        var offeredPerson = Person.Create(spaceId, "Offered", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);
        var assignedRequest = ShiftRequest.Create(spaceId, slot.Id, assignedPerson.Id, groupId, cycleId);
        assignedRequest.Approve();
        slot.IncrementFillCount();
        var entry = WaitlistEntry.Create(spaceId, slot.Id, offeredPerson.Id, position: 1);
        entry.Offer(DateTime.UtcNow.AddMinutes(30));

        db.People.AddRange(assignedPerson, offeredPerson);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(assignedRequest);
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var waitlistService = Substitute.For<IWaitlistService>();
        var handler = new AcceptWaitlistOfferCommandHandler(
            db,
            waitlistService,
            Substitute.For<IPushNotificationSender>(),
            Substitute.For<INotificationService>(),
            NullLogger<AcceptWaitlistOfferCommandHandler>.Instance);

        var result = await handler.Handle(
            new AcceptWaitlistOfferCommand(spaceId, offeredPerson.Id, slot.Id),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("The shift slot is no longer available.");
        result.ShiftRequestId.Should().BeNull();

        var updatedEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == entry.Id);
        updatedEntry.Status.Should().Be(WaitlistEntryStatus.Removed);

        var updatedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == slot.Id);
        updatedSlot.CurrentFillCount.Should().Be(1);
        (await db.ShiftRequests.CountAsync()).Should().Be(1);

        await waitlistService.DidNotReceiveWithAnyArgs()
            .ProcessSlotReleasedAsync(default, default);
    }

    [Fact]
    public async Task AcceptWaitlistOffer_RejectsStaleOffer_WhenMemberAlreadyHasActiveRequestForSlot()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var offeredPerson = Person.Create(spaceId, "Offered", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);
        var existingRequest = ShiftRequest.Create(spaceId, slot.Id, offeredPerson.Id, groupId, cycleId);
        var entry = WaitlistEntry.Create(spaceId, slot.Id, offeredPerson.Id, position: 1);
        entry.Offer(DateTime.UtcNow.AddMinutes(30));

        db.People.Add(offeredPerson);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(existingRequest);
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var waitlistService = Substitute.For<IWaitlistService>();
        var handler = new AcceptWaitlistOfferCommandHandler(
            db,
            waitlistService,
            Substitute.For<IPushNotificationSender>(),
            Substitute.For<INotificationService>(),
            NullLogger<AcceptWaitlistOfferCommandHandler>.Instance);

        var result = await handler.Handle(
            new AcceptWaitlistOfferCommand(spaceId, offeredPerson.Id, slot.Id),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("You already have an active request for this slot.");
        result.ShiftRequestId.Should().BeNull();

        var updatedEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == entry.Id);
        updatedEntry.Status.Should().Be(WaitlistEntryStatus.Removed);
        (await db.ShiftRequests.CountAsync()).Should().Be(1);

        await waitlistService.Received(1)
            .ProcessSlotReleasedAsync(slot.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptWaitlistOffer_CascadeFailure_DoesNotNotifyAcceptance()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var offeredPerson = Person.Create(spaceId, "Offered", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);
        var existingRequest = ShiftRequest.Create(spaceId, slot.Id, offeredPerson.Id, groupId, cycleId);
        var entry = WaitlistEntry.Create(spaceId, slot.Id, offeredPerson.Id, position: 1);
        entry.Offer(DateTime.UtcNow.AddMinutes(30));

        db.People.Add(offeredPerson);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(existingRequest);
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var waitlistService = Substitute.For<IWaitlistService>();
        waitlistService
            .ProcessSlotReleasedAsync(slot.Id, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Waitlist cascade failed."));
        var pushSender = Substitute.For<IPushNotificationSender>();
        var notificationService = Substitute.For<INotificationService>();
        var handler = new AcceptWaitlistOfferCommandHandler(
            db,
            waitlistService,
            pushSender,
            notificationService,
            NullLogger<AcceptWaitlistOfferCommandHandler>.Instance);

        var act = () => handler.Handle(
            new AcceptWaitlistOfferCommand(spaceId, offeredPerson.Id, slot.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Waitlist cascade failed.");

        await pushSender.DidNotReceiveWithAnyArgs()
            .SendPushToUserAsync(default, default, default!, default);
        await notificationService.DidNotReceiveWithAnyArgs()
            .NotifySpaceAdminsAsync(default, default!, default!, default!, default!, default, default);
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
        var notificationService = Substitute.For<INotificationService>();
        var service = CreateService(db, pushSender, notificationService);

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
        await notificationService.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.waitlist_offer_declined",
            "Waitlist Offer Declined",
            Arg.Is<string>(body => body.Contains("Offered") && body.Contains("declined") && body.Contains("Guard")),
            Arg.Is<string>(metadata => metadata.Contains(offeredEntry.Id.ToString()) && metadata.Contains(slot.Id.ToString())),
            groupId,
            Arg.Any<CancellationToken>());
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
        var notificationService = Substitute.For<INotificationService>();
        var service = CreateService(db, pushSender, notificationService);

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
        await notificationService.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.waitlist_offer_expired",
            "Waitlist Offer Expired",
            Arg.Is<string>(body => body.Contains("Expired") && body.Contains("expired") && body.Contains("Guard")),
            Arg.Is<string>(metadata => metadata.Contains(expiredEntry.Id.ToString()) && metadata.Contains(slot.Id.ToString())),
            groupId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSlotReleasedAsync_DoesNotOfferNextMember_WhenSlotAlreadyHasActiveOffer()
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

        await service.ProcessSlotReleasedAsync(slot.Id);

        var updatedWaitingEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == waitingEntry.Id);
        updatedWaitingEntry.Status.Should().Be(WaitlistEntryStatus.Waiting);
        updatedWaitingEntry.OfferedAt.Should().BeNull();
        updatedWaitingEntry.ExpiresAt.Should().BeNull();
        await pushSender.DidNotReceiveWithAnyArgs()
            .SendPushToUserAsync(default, default, default!, default);
    }

    [Fact]
    public async Task ProcessSlotReleasedAsync_DoesNotOfferNextMember_WhenSlotIsStillFull()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var assignedPerson = Person.Create(spaceId, "Assigned", linkedUserId: Guid.NewGuid());
        var waitingPerson = Person.Create(spaceId, "Waiting", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);
        var assignedRequest = ShiftRequest.Create(spaceId, slot.Id, assignedPerson.Id, groupId, cycleId);
        assignedRequest.Approve();
        slot.IncrementFillCount();
        var waitingEntry = WaitlistEntry.Create(spaceId, slot.Id, waitingPerson.Id, position: 1);

        db.People.AddRange(assignedPerson, waitingPerson);
        db.ShiftSlots.Add(slot);
        db.ShiftRequests.Add(assignedRequest);
        db.WaitlistEntries.Add(waitingEntry);
        await db.SaveChangesAsync();

        var pushSender = Substitute.For<IPushNotificationSender>();
        var service = CreateService(db, pushSender);

        await service.ProcessSlotReleasedAsync(slot.Id);

        var updatedWaitingEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == waitingEntry.Id);
        updatedWaitingEntry.Status.Should().Be(WaitlistEntryStatus.Waiting);
        updatedWaitingEntry.OfferedAt.Should().BeNull();
        updatedWaitingEntry.ExpiresAt.Should().BeNull();
        await pushSender.DidNotReceiveWithAnyArgs()
            .SendPushToUserAsync(default, default, default!, default);
    }

    [Fact]
    public async Task ProcessSlotReleasedAsync_UsesConfiguredWaitlistOfferMinutes()
    {
        using var db = CreateDb();
        var fixedNow = new DateTimeOffset(2026, 6, 11, 8, 30, 0, TimeSpan.Zero);
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var waitingPerson = Person.Create(spaceId, "Waiting", linkedUserId: Guid.NewGuid());
        var slot = CreateSlot(spaceId, groupId, taskId, cycleId);
        var waitingEntry = WaitlistEntry.Create(spaceId, slot.Id, waitingPerson.Id, position: 1);
        db.SelfServiceConfigs.Add(SelfServiceConfig.Create(
            spaceId,
            groupId,
            minShiftsPerCycle: 1,
            maxShiftsPerCycle: 3,
            requestWindowOpenOffsetHours: 168,
            requestWindowCloseOffsetHours: 24,
            cancellationCutoffHours: 24,
            maxLateCancellationsPerCycle: 2,
            lateCancellationWindowHours: 24,
            waitlistOfferMinutes: 45,
            cycleDurationDays: 7));

        db.People.Add(waitingPerson);
        db.ShiftSlots.Add(slot);
        db.WaitlistEntries.Add(waitingEntry);
        await db.SaveChangesAsync();

        var service = CreateService(db, timeProvider: new FixedTimeProvider(fixedNow));

        await service.ProcessSlotReleasedAsync(slot.Id);

        var updatedWaitingEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == waitingEntry.Id);
        updatedWaitingEntry.Status.Should().Be(WaitlistEntryStatus.Offered);
        updatedWaitingEntry.OfferedAt.Should().NotBeNull();
        updatedWaitingEntry.ExpiresAt.Should().Be(fixedNow.UtcDateTime.AddMinutes(45));
    }

    private static WaitlistService CreateService(
        AppDbContext db,
        IPushNotificationSender? pushSender = null,
        INotificationService? notificationService = null,
        TimeProvider? timeProvider = null) =>
        new(
            db,
            pushSender ?? Substitute.For<IPushNotificationSender>(),
            notificationService ?? Substitute.For<INotificationService>(),
            timeProvider ?? TimeProvider.System,
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
        Guid cycleId,
        DateOnly? date = null,
        TimeOnly? startTime = null,
        TimeOnly? endTime = null,
        int capacity = 1) =>
        ShiftSlot.Create(
            spaceId,
            groupId,
            taskId,
            shiftTemplateId: Guid.NewGuid(),
            schedulingCycleId: cycleId,
            date: date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            startTime: startTime ?? new TimeOnly(8, 0),
            endTime: endTime ?? new TimeOnly(16, 0),
            capacity: capacity);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
