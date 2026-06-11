using FluentAssertions;
using Jobuler.Application.Common;
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

public class ManualSelfServiceLifecycleTests
{
    [Fact]
    public async Task DisabledWorkflowPolicies_RejectMemberServiceActions()
    {
        using var db = CreateDb();
        var notifications = Substitute.For<INotificationService>();
        var pushSender = Substitute.For<IPushNotificationSender>();
        var audit = Substitute.For<IAuditLogger>();
        var slotLock = Substitute.For<ISlotLockService>();
        slotLock
            .TryAcquireSlotLockAsync(Arg.Any<Guid>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (spaceId, groupId, cycleId, taskId) = SeedSelfServiceCycle(db);
        var config = await db.SelfServiceConfigs.SingleAsync(c => c.GroupId == groupId);
        config.SetWorkflowPermissions(
            allowMemberShiftClaims: false,
            allowWaitlist: false,
            allowShiftChangeRequests: false,
            allowAbsenceReports: false,
            allowShiftSwaps: false);

        var alice = Person.Create(spaceId, "Alice Member", linkedUserId: Guid.NewGuid());
        var bob = Person.Create(spaceId, "Bob Member", linkedUserId: Guid.NewGuid());
        var claimSlot = AddSlot(db, spaceId, groupId, taskId, cycleId);
        var fullSlot = AddSlot(db, spaceId, groupId, taskId, cycleId);
        var bobSlot = AddSlot(db, spaceId, groupId, taskId, cycleId);

        var aliceRequest = ShiftRequest.Create(spaceId, fullSlot.Id, alice.Id, groupId, cycleId);
        aliceRequest.Approve();
        fullSlot.IncrementFillCount();

        var bobRequest = ShiftRequest.Create(spaceId, bobSlot.Id, bob.Id, groupId, cycleId);
        bobRequest.Approve();
        bobSlot.IncrementFillCount();

        db.People.AddRange(alice, bob);
        db.ShiftRequests.AddRange(aliceRequest, bobRequest);
        await db.SaveChangesAsync();

        var waitlistService = new WaitlistService(
            db,
            pushSender,
            notifications,
            TimeProvider.System,
            NullLogger<WaitlistService>.Instance);

        var shiftRequests = new ShiftRequestService(
            db,
            slotLock,
            TimeProvider.System,
            NullLogger<ShiftRequestService>.Instance,
            notifications,
            pushSender,
            audit,
            waitlistService);

        var claimResult = await shiftRequests.ProcessRequestAsync(bob.Id, claimSlot.Id);
        claimResult.Success.Should().BeFalse();
        claimResult.RejectionReason.Should().Contain("disabled");

        var waitlistResult = await waitlistService.JoinWaitlistAsync(bob.Id, fullSlot.Id);
        waitlistResult.Success.Should().BeFalse();
        waitlistResult.ErrorMessage.Should().Contain("disabled");

        var absenceResult = await shiftRequests.ReportCannotAttendAsync(alice.Id, aliceRequest.Id, "Sick");
        absenceResult.Success.Should().BeFalse();
        absenceResult.ErrorMessage.Should().Contain("disabled");

        var swapService = new ShiftSwapService(
            db,
            pushSender,
            audit,
            TimeProvider.System,
            NullLogger<ShiftSwapService>.Instance);

        var swapResult = await swapService.ProposeSwapAsync(alice.Id, aliceRequest.Id, bobRequest.Id);
        swapResult.Success.Should().BeFalse();
        swapResult.ErrorMessage.Should().Contain("disabled");

        (await db.ShiftRequests.CountAsync()).Should().Be(2);
        (await db.WaitlistEntries.CountAsync()).Should().Be(0);
        (await db.ShiftAbsenceReports.CountAsync()).Should().Be(0);
        (await db.SwapRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task LateAbsenceLimit_DoesNotReleaseShiftOrWaitlist_WhenLimitIsReached()
    {
        using var db = CreateDb();
        var notifications = Substitute.For<INotificationService>();
        var pushSender = Substitute.For<IPushNotificationSender>();
        var audit = Substitute.For<IAuditLogger>();
        var slotLock = Substitute.For<ISlotLockService>();
        var waitlistService = Substitute.For<IWaitlistService>();
        var fixedNow = DateTimeOffset.UtcNow;

        var (spaceId, groupId, cycleId, taskId) = SeedSelfServiceCycle(db);
        var config = await db.SelfServiceConfigs.SingleAsync(c => c.GroupId == groupId);
        config.SetLateCancellationLimits(maxPerCycle: 1, windowHours: 72);

        var member = Person.Create(spaceId, "Limited Member", linkedUserId: Guid.NewGuid());
        var targetSlot = AddSlot(db, spaceId, groupId, taskId, cycleId);
        var targetRequest = ShiftRequest.Create(spaceId, targetSlot.Id, member.Id, groupId, cycleId);
        targetRequest.Approve();
        targetSlot.IncrementFillCount();

        var priorSlot = AddSlot(db, spaceId, groupId, taskId, cycleId);
        var priorRequest = ShiftRequest.Create(spaceId, priorSlot.Id, member.Id, groupId, cycleId);
        priorRequest.Approve();
        var priorReport = ShiftAbsenceReport.Create(
            spaceId,
            groupId,
            cycleId,
            priorRequest.Id,
            priorSlot.Id,
            member.Id,
            "Already used late absence",
            isLate: true,
            fixedNow.UtcDateTime);

        db.People.Add(member);
        db.ShiftRequests.AddRange(targetRequest, priorRequest);
        db.ShiftAbsenceReports.Add(priorReport);
        await db.SaveChangesAsync();

        var shiftRequests = new ShiftRequestService(
            db,
            slotLock,
            new FixedTimeProvider(fixedNow),
            NullLogger<ShiftRequestService>.Instance,
            notifications,
            pushSender,
            audit,
            waitlistService);

        var result = await shiftRequests.ReportCannotAttendAsync(
            member.Id,
            targetRequest.Id,
            "Cannot make this shift",
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.WasLate.Should().BeTrue();
        result.LateReportsUsed.Should().Be(1);
        result.MaxLateReports.Should().Be(1);
        result.ErrorMessage.Should().Contain("late absence limit");

        var unchangedRequest = await db.ShiftRequests.SingleAsync(r => r.Id == targetRequest.Id);
        unchangedRequest.Status.Should().Be(ShiftRequestStatus.Approved);
        unchangedRequest.CancellationReason.Should().BeNull();

        var unchangedSlot = await db.ShiftSlots.SingleAsync(s => s.Id == targetSlot.Id);
        unchangedSlot.CurrentFillCount.Should().Be(1);

        (await db.ShiftAbsenceReports.CountAsync()).Should().Be(1);
        await waitlistService.DidNotReceiveWithAnyArgs().ProcessSlotReleasedAsync(default, default);
        await audit.DidNotReceiveWithAnyArgs().LogAsync(default, default, default!, default, default, default, default, default, default);
        await notifications.DidNotReceiveWithAnyArgs().NotifySpaceAdminsAsync(default, default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task PickAbsenceWaitlistAcceptance_RebuildsCoverageAndNotifiesAdmins()
    {
        using var db = CreateDb();
        var notifications = Substitute.For<INotificationService>();
        var pushSender = Substitute.For<IPushNotificationSender>();
        var audit = Substitute.For<IAuditLogger>();
        var slotLock = Substitute.For<ISlotLockService>();
        slotLock
            .TryAcquireSlotLockAsync(Arg.Any<Guid>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (spaceId, groupId, cycleId, taskId) = SeedSelfServiceCycle(db);
        var aliceUserId = Guid.NewGuid();
        var bobUserId = Guid.NewGuid();
        var alice = Person.Create(spaceId, "Alice Member", linkedUserId: aliceUserId);
        var bob = Person.Create(spaceId, "Bob Member", linkedUserId: bobUserId);
        var slot = AddSlot(db, spaceId, groupId, taskId, cycleId);

        db.People.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var waitlistService = new WaitlistService(
            db,
            pushSender,
            notifications,
            TimeProvider.System,
            NullLogger<WaitlistService>.Instance);

        var shiftRequests = new ShiftRequestService(
            db,
            slotLock,
            TimeProvider.System,
            NullLogger<ShiftRequestService>.Instance,
            notifications,
            pushSender,
            audit,
            waitlistService);

        var alicePick = await shiftRequests.ProcessRequestAsync(alice.Id, slot.Id);
        alicePick.Success.Should().BeTrue();
        alicePick.ShiftRequestId.Should().NotBeNull();

        var waitlistJoin = await waitlistService.JoinWaitlistAsync(bob.Id, slot.Id);
        waitlistJoin.Success.Should().BeTrue();
        waitlistJoin.Position.Should().Be(1);

        var absence = await shiftRequests.ReportCannotAttendAsync(
            alice.Id,
            alicePick.ShiftRequestId!.Value,
            "Cannot make this shift");

        absence.Success.Should().BeTrue();
        absence.WasLate.Should().BeFalse();

        var offeredEntry = await db.WaitlistEntries.SingleAsync(e => e.PersonId == bob.Id);
        offeredEntry.Status.Should().Be(WaitlistEntryStatus.Offered);
        offeredEntry.ExpiresAt.Should().NotBeNull();

        var acceptHandler = new AcceptWaitlistOfferCommandHandler(
            db,
            waitlistService,
            pushSender,
            notifications,
            NullLogger<AcceptWaitlistOfferCommandHandler>.Instance);

        var accepted = await acceptHandler.Handle(
            new AcceptWaitlistOfferCommand(spaceId, bob.Id, slot.Id),
            CancellationToken.None);

        accepted.Success.Should().BeTrue();
        accepted.ShiftRequestId.Should().NotBeNull();

        var finalSlot = await db.ShiftSlots.SingleAsync(s => s.Id == slot.Id);
        finalSlot.CurrentFillCount.Should().Be(1);

        var aliceRequest = await db.ShiftRequests.SingleAsync(r => r.Id == alicePick.ShiftRequestId);
        aliceRequest.Status.Should().Be(ShiftRequestStatus.Cancelled);
        aliceRequest.CancellationReason.Should().Contain("Cannot attend");

        var bobRequest = await db.ShiftRequests.SingleAsync(r => r.Id == accepted.ShiftRequestId);
        bobRequest.PersonId.Should().Be(bob.Id);
        bobRequest.Status.Should().Be(ShiftRequestStatus.Approved);

        var finalWaitlistEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == offeredEntry.Id);
        finalWaitlistEntry.Status.Should().Be(WaitlistEntryStatus.Accepted);

        var absenceReport = await db.ShiftAbsenceReports.SingleAsync();
        absenceReport.PersonId.Should().Be(alice.Id);
        absenceReport.Status.Should().Be(ShiftAbsenceReportStatus.Pending);

        await notifications.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.absence_reported",
            "Absence Reported",
            Arg.Is<string>(body => body.Contains("Alice Member") && body.Contains("Guard")),
            Arg.Is<string>(metadata => metadata.Contains(absenceReport.Id.ToString())),
            groupId,
            Arg.Any<CancellationToken>());

        await notifications.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.waitlist_accepted",
            "Waitlist Offer Accepted",
            Arg.Is<string>(body => body.Contains("Bob Member") && body.Contains("Guard")),
            Arg.Is<string>(metadata => metadata.Contains(accepted.ShiftRequestId!.Value.ToString())),
            groupId,
            Arg.Any<CancellationToken>());

        var memberNotifications = await db.Notifications
            .Select(n => n.EventType)
            .ToListAsync();

        memberNotifications.Should().Contain("self_service.waitlist_offer");
        memberNotifications.Count(n => n == "self_service.request_approved").Should().Be(2);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private static (Guid SpaceId, Guid GroupId, Guid CycleId, Guid TaskId) SeedSelfServiceCycle(AppDbContext db)
    {
        var spaceId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Operations", createdByUserId: ownerUserId);
        group.SetSchedulingMode(SchedulingMode.SelfService);

        var now = DateTime.UtcNow;
        var cycle = SchedulingCycle.Create(
            spaceId,
            group.Id,
            startsAt: now.AddDays(1),
            endsAt: now.AddDays(8),
            requestWindowOpensAt: now.AddDays(-1),
            requestWindowClosesAt: now.AddHours(12));

        var config = SelfServiceConfig.Create(
            spaceId,
            group.Id,
            minShiftsPerCycle: 1,
            maxShiftsPerCycle: 3,
            requestWindowOpenOffsetHours: 48,
            requestWindowCloseOffsetHours: 12,
            cancellationCutoffHours: 12,
            maxLateCancellationsPerCycle: 2,
            lateCancellationWindowHours: 12,
            waitlistOfferMinutes: 60,
            cycleDurationDays: 7);

        var task = GroupTask.Create(
            spaceId,
            group.Id,
            "Guard",
            now,
            now.AddDays(30),
            shiftDurationMinutes: 480,
            requiredHeadcount: 1,
            burdenLevel: TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: ownerUserId);

        db.Groups.Add(group);
        db.SchedulingCycles.Add(cycle);
        db.SelfServiceConfigs.Add(config);
        db.GroupTasks.Add(task);
        db.SaveChanges();

        return (spaceId, group.Id, cycle.Id, task.Id);
    }

    private static ShiftSlot AddSlot(
        AppDbContext db,
        Guid spaceId,
        Guid groupId,
        Guid taskId,
        Guid cycleId)
    {
        var slot = ShiftSlot.Create(
            spaceId,
            groupId,
            taskId,
            shiftTemplateId: Guid.NewGuid(),
            schedulingCycleId: cycleId,
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            startTime: new TimeOnly(8, 0),
            endTime: new TimeOnly(16, 0),
            capacity: 1);

        db.ShiftSlots.Add(slot);
        return slot;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
