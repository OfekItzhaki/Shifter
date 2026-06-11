using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Scheduling.SelfService.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class SelfServiceNotificationTests
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
    public async Task ReportCannotAttendAsync_NotifiesSpaceAdmins_WhenAbsenceReportIsCreated()
    {
        using var db = CreateDb();
        var notifications = Substitute.For<INotificationService>();
        var audit = CreateAuditLogger();
        var (spaceId, groupId, cycleId, taskId, _) = SeedBaseData(db);

        var linkedUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Alex Member", linkedUserId: linkedUserId);
        var slot = AddSlot(db, spaceId, groupId, taskId, cycleId, daysFromNow: 2);
        slot.IncrementFillCount();

        var request = ShiftRequest.Create(spaceId, slot.Id, person.Id, groupId, cycleId);
        request.Approve();

        db.People.Add(person);
        db.ShiftRequests.Add(request);
        await db.SaveChangesAsync();

        var service = new ShiftRequestService(
            db,
            Substitute.For<ISlotLockService>(),
            TimeProvider.System,
            NullLogger<ShiftRequestService>.Instance,
            notifications,
            Substitute.For<IPushNotificationSender>(),
            audit);

        var result = await service.ReportCannotAttendAsync(person.Id, request.Id, "Sick");

        result.Success.Should().BeTrue();
        await notifications.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.absence_reported",
            "Absence Reported",
            Arg.Is<string>(body => body.Contains("Alex Member") && body.Contains("Guard")),
            Arg.Is<string>(metadata => metadata.Contains("\"reason\":\"Sick\"")),
            groupId,
            Arg.Any<CancellationToken>());

        await audit.Received(1).LogAsync(
            spaceId,
            linkedUserId,
            "self_service.report_absence",
            "shift_absence_report",
            result.AbsenceReportId,
            Arg.Is<string?>(json => json != null
                && json.Contains(request.Id.ToString())
                && json.Contains("\"shift_request_status\":\"approved\"")),
            Arg.Is<string?>(json => json != null
                && json.Contains(result.AbsenceReportId!.Value.ToString())
                && json.Contains("\"shift_request_status\":\"cancelled\"")
                && json.Contains("\"was_late\":false")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportCannotAttendAsync_EnforcesLateAbsenceLimit_ExcludingRejectedReports()
    {
        using var db = CreateDb();
        var notifications = Substitute.For<INotificationService>();
        var audit = CreateAuditLogger();
        var (spaceId, groupId, cycleId, taskId, ownerUserId) = SeedBaseData(db);

        db.SelfServiceConfigs.Add(SelfServiceConfig.Create(
            spaceId,
            groupId,
            minShiftsPerCycle: 0,
            maxShiftsPerCycle: 5,
            requestWindowOpenOffsetHours: 48,
            requestWindowCloseOffsetHours: 12,
            cancellationCutoffHours: 24,
            maxLateCancellationsPerCycle: 1,
            lateCancellationWindowHours: 24,
            waitlistOfferMinutes: 60,
            cycleDurationDays: 7));

        var person = Person.Create(spaceId, "Late Member", linkedUserId: Guid.NewGuid());
        db.People.Add(person);

        var rejectedSlot = AddLateSlot(db, spaceId, groupId, taskId, cycleId, startsInHours: 2);
        var firstSlot = AddLateSlot(db, spaceId, groupId, taskId, cycleId, startsInHours: 3);
        var secondSlot = AddLateSlot(db, spaceId, groupId, taskId, cycleId, startsInHours: 4);

        var rejectedRequest = AddApprovedRequest(db, spaceId, groupId, cycleId, person.Id, rejectedSlot);
        var firstRequest = AddApprovedRequest(db, spaceId, groupId, cycleId, person.Id, firstSlot);
        var secondRequest = AddApprovedRequest(db, spaceId, groupId, cycleId, person.Id, secondSlot);

        var rejectedReport = ShiftAbsenceReport.Create(
            spaceId,
            groupId,
            cycleId,
            rejectedRequest.Id,
            rejectedSlot.Id,
            person.Id,
            "Rejected earlier",
            isLate: true,
            reportedAt: DateTime.UtcNow);
        rejectedReport.Reject(ownerUserId, "Does not count");
        db.ShiftAbsenceReports.Add(rejectedReport);
        await db.SaveChangesAsync();

        var service = new ShiftRequestService(
            db,
            Substitute.For<ISlotLockService>(),
            TimeProvider.System,
            NullLogger<ShiftRequestService>.Instance,
            notifications,
            Substitute.For<IPushNotificationSender>(),
            audit);

        var firstResult = await service.ReportCannotAttendAsync(person.Id, firstRequest.Id, "Cannot make first late shift");

        firstResult.Success.Should().BeTrue();
        firstResult.WasLate.Should().BeTrue();
        firstResult.LateReportsUsed.Should().Be(1);
        firstResult.MaxLateReports.Should().Be(1);

        var secondResult = await service.ReportCannotAttendAsync(person.Id, secondRequest.Id, "Cannot make second late shift");

        secondResult.Success.Should().BeFalse();
        secondResult.WasLate.Should().BeTrue();
        secondResult.LateReportsUsed.Should().Be(1);
        secondResult.MaxLateReports.Should().Be(1);
        secondResult.ErrorMessage.Should().Contain("late absence limit");

        var persistedReports = await db.ShiftAbsenceReports
            .Where(r => r.PersonId == person.Id)
            .ToListAsync();
        persistedReports.Should().HaveCount(2);
        persistedReports.Count(r => r.Status == ShiftAbsenceReportStatus.Rejected).Should().Be(1);
        persistedReports.Count(r => r.Status == ShiftAbsenceReportStatus.Pending).Should().Be(1);
    }

    [Fact]
    public async Task CancelRequestAsync_NotifiesSpaceAdminsAndAuditsCancellation()
    {
        using var db = CreateDb();
        var notifications = Substitute.For<INotificationService>();
        var audit = CreateAuditLogger();
        var (spaceId, groupId, cycleId, taskId, _) = SeedBaseData(db);

        var linkedUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Alex Member", linkedUserId: linkedUserId);
        var slot = AddSlot(db, spaceId, groupId, taskId, cycleId, daysFromNow: 2);
        slot.IncrementFillCount();

        var request = ShiftRequest.Create(spaceId, slot.Id, person.Id, groupId, cycleId);
        request.Approve();

        db.People.Add(person);
        db.ShiftRequests.Add(request);
        await db.SaveChangesAsync();

        var service = new ShiftRequestService(
            db,
            Substitute.For<ISlotLockService>(),
            TimeProvider.System,
            NullLogger<ShiftRequestService>.Instance,
            notifications,
            Substitute.For<IPushNotificationSender>(),
            audit);

        var result = await service.CancelRequestAsync(person.Id, request.Id, "Family issue");

        result.Success.Should().BeTrue();

        var updatedRequest = await db.ShiftRequests.SingleAsync(r => r.Id == request.Id);
        updatedRequest.Status.Should().Be(ShiftRequestStatus.Cancelled);

        await notifications.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.shift_cancelled",
            "Shift Cancelled",
            Arg.Is<string>(body => body.Contains("Alex Member") && body.Contains("Guard")),
            Arg.Is<string>(metadata => metadata.Contains("\"reason\":\"Family issue\"")),
            groupId,
            Arg.Any<CancellationToken>());

        await audit.Received(1).LogAsync(
            spaceId,
            linkedUserId,
            "self_service.cancel_shift",
            "shift_request",
            request.Id,
            Arg.Is<string?>(json => json != null
                && json.Contains(request.Id.ToString())
                && json.Contains("\"shift_request_status\":\"approved\"")),
            Arg.Is<string?>(json => json != null
                && json.Contains(request.Id.ToString())
                && json.Contains("\"shift_request_status\":\"cancelled\"")
                && json.Contains("\"cancellation_reason\":\"Family issue\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptSwapAsync_CreatesAcceptedNotifications_ForBothMembers()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, guardTaskId, ownerUserId) = SeedBaseData(db);
        var deskTask = AddTask(db, spaceId, groupId, "Desk", ownerUserId);
        var audit = CreateAuditLogger();

        var initiatorUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var initiator = Person.Create(spaceId, "Initiator", linkedUserId: initiatorUserId);
        var target = Person.Create(spaceId, "Target", linkedUserId: targetUserId);

        var initiatorSlot = AddSlot(db, spaceId, groupId, guardTaskId, cycleId, daysFromNow: 3);
        var targetSlot = AddSlot(db, spaceId, groupId, deskTask.Id, cycleId, daysFromNow: 4);

        var initiatorRequest = ShiftRequest.Create(spaceId, initiatorSlot.Id, initiator.Id, groupId, cycleId);
        initiatorRequest.Approve();
        var targetRequest = ShiftRequest.Create(spaceId, targetSlot.Id, target.Id, groupId, cycleId);
        targetRequest.Approve();

        var swap = SwapRequest.Create(
            spaceId,
            groupId,
            initiator.Id,
            target.Id,
            initiatorRequest.Id,
            targetRequest.Id);

        db.People.AddRange(initiator, target);
        db.ShiftRequests.AddRange(initiatorRequest, targetRequest);
        db.SwapRequests.Add(swap);
        await db.SaveChangesAsync();

        var service = new ShiftSwapService(
            db,
            Substitute.For<IPushNotificationSender>(),
            audit,
            TimeProvider.System,
            NullLogger<ShiftSwapService>.Instance);

        var result = await service.AcceptSwapAsync(target.Id, swap.Id);

        result.Success.Should().BeTrue();

        var acceptedNotifications = await db.Notifications
            .Where(n => n.EventType == "self_service.swap_accepted")
            .ToListAsync();

        acceptedNotifications.Should().HaveCount(2);
        acceptedNotifications.Select(n => n.UserId)
            .Should().BeEquivalentTo([initiatorUserId, targetUserId]);
        acceptedNotifications.Should().AllSatisfy(n =>
            n.MetadataJson.Should().Contain(swap.Id.ToString()));

        await audit.Received(1).LogAsync(
            spaceId,
            targetUserId,
            "self_service.accept_swap",
            "swap_request",
            swap.Id,
            Arg.Is<string?>(json => json != null
                && json.Contains(swap.Id.ToString())
                && json.Contains("\"status\":\"pending\"")),
            Arg.Is<string?>(json => json != null
                && json.Contains(initiatorSlot.Id.ToString())
                && json.Contains(targetSlot.Id.ToString())
                && json.Contains("\"status\":\"accepted\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProposeSwapAsync_NotifiesTargetAndAuditsProposal()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, guardTaskId, ownerUserId) = SeedBaseData(db);
        var deskTask = AddTask(db, spaceId, groupId, "Desk", ownerUserId);
        var audit = CreateAuditLogger();
        var pushSender = Substitute.For<IPushNotificationSender>();

        var initiatorUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var initiator = Person.Create(spaceId, "Initiator", linkedUserId: initiatorUserId);
        var target = Person.Create(spaceId, "Target", linkedUserId: targetUserId);

        var initiatorSlot = AddSlot(db, spaceId, groupId, guardTaskId, cycleId, daysFromNow: 3);
        var targetSlot = AddSlot(db, spaceId, groupId, deskTask.Id, cycleId, daysFromNow: 4);

        var initiatorRequest = ShiftRequest.Create(spaceId, initiatorSlot.Id, initiator.Id, groupId, cycleId);
        initiatorRequest.Approve();
        var targetRequest = ShiftRequest.Create(spaceId, targetSlot.Id, target.Id, groupId, cycleId);
        targetRequest.Approve();

        db.People.AddRange(initiator, target);
        db.ShiftRequests.AddRange(initiatorRequest, targetRequest);
        await db.SaveChangesAsync();

        var service = new ShiftSwapService(
            db,
            pushSender,
            audit,
            TimeProvider.System,
            NullLogger<ShiftSwapService>.Instance);

        var result = await service.ProposeSwapAsync(initiator.Id, initiatorRequest.Id, targetRequest.Id);

        result.Success.Should().BeTrue();
        result.SwapRequestId.Should().NotBeNull();

        var swap = await db.SwapRequests.SingleAsync(s => s.Id == result.SwapRequestId);
        swap.Status.Should().Be(SwapRequestStatus.Pending);
        swap.InitiatorPersonId.Should().Be(initiator.Id);
        swap.TargetPersonId.Should().Be(target.Id);
        swap.InitiatorShiftRequestId.Should().Be(initiatorRequest.Id);
        swap.TargetShiftRequestId.Should().Be(targetRequest.Id);

        var notification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.swap_proposal_received");
        notification.UserId.Should().Be(targetUserId);
        notification.MetadataJson.Should().Contain(swap.Id.ToString());
        notification.MetadataJson.Should().Contain(initiatorSlot.Id.ToString());
        notification.MetadataJson.Should().Contain(targetSlot.Id.ToString());

        await pushSender.Received(1)
            .SendPushToUserAsync(targetUserId, spaceId, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());

        await audit.Received(1).LogAsync(
            spaceId,
            initiatorUserId,
            "self_service.propose_swap",
            "swap_request",
            swap.Id,
            Arg.Is<string?>(json => json == null),
            Arg.Is<string?>(json => json != null
                && json.Contains(swap.Id.ToString())
                && json.Contains(initiatorSlot.Id.ToString())
                && json.Contains(targetSlot.Id.ToString())
                && json.Contains("\"status\":\"pending\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProposeThenAcceptSwapAsync_SwapsAssignmentsAndNotifiesBothMembers()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, guardTaskId, ownerUserId) = SeedBaseData(db);
        var deskTask = AddTask(db, spaceId, groupId, "Desk", ownerUserId);
        var audit = CreateAuditLogger();
        var pushSender = Substitute.For<IPushNotificationSender>();

        var initiatorUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var initiator = Person.Create(spaceId, "Initiator", linkedUserId: initiatorUserId);
        var target = Person.Create(spaceId, "Target", linkedUserId: targetUserId);

        var initiatorSlot = AddSlot(db, spaceId, groupId, guardTaskId, cycleId, daysFromNow: 3);
        var targetSlot = AddSlot(db, spaceId, groupId, deskTask.Id, cycleId, daysFromNow: 4);

        var initiatorRequest = ShiftRequest.Create(spaceId, initiatorSlot.Id, initiator.Id, groupId, cycleId);
        initiatorRequest.Approve();
        var targetRequest = ShiftRequest.Create(spaceId, targetSlot.Id, target.Id, groupId, cycleId);
        targetRequest.Approve();

        db.People.AddRange(initiator, target);
        db.ShiftRequests.AddRange(initiatorRequest, targetRequest);
        await db.SaveChangesAsync();

        var service = new ShiftSwapService(
            db,
            pushSender,
            audit,
            TimeProvider.System,
            NullLogger<ShiftSwapService>.Instance);

        var proposal = await service.ProposeSwapAsync(initiator.Id, initiatorRequest.Id, targetRequest.Id);
        proposal.Success.Should().BeTrue();
        proposal.SwapRequestId.Should().NotBeNull();

        var acceptance = await service.AcceptSwapAsync(target.Id, proposal.SwapRequestId!.Value);
        acceptance.Success.Should().BeTrue();

        var updatedSwap = await db.SwapRequests.SingleAsync(s => s.Id == proposal.SwapRequestId);
        updatedSwap.Status.Should().Be(SwapRequestStatus.Accepted);

        var updatedInitiatorRequest = await db.ShiftRequests.SingleAsync(r => r.Id == initiatorRequest.Id);
        updatedInitiatorRequest.PersonId.Should().Be(initiator.Id);
        updatedInitiatorRequest.ShiftSlotId.Should().Be(targetSlot.Id);

        var updatedTargetRequest = await db.ShiftRequests.SingleAsync(r => r.Id == targetRequest.Id);
        updatedTargetRequest.PersonId.Should().Be(target.Id);
        updatedTargetRequest.ShiftSlotId.Should().Be(initiatorSlot.Id);

        var proposalNotification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.swap_proposal_received");
        proposalNotification.UserId.Should().Be(targetUserId);
        proposalNotification.MetadataJson.Should().Contain(updatedSwap.Id.ToString());

        var acceptedNotifications = await db.Notifications
            .Where(n => n.EventType == "self_service.swap_accepted")
            .ToListAsync();
        acceptedNotifications.Should().HaveCount(2);
        acceptedNotifications.Select(n => n.UserId)
            .Should().BeEquivalentTo([initiatorUserId, targetUserId]);
        acceptedNotifications.Should().AllSatisfy(n =>
            n.MetadataJson.Should().Contain(updatedSwap.Id.ToString()));
        acceptedNotifications.Select(n => n.MetadataJson).Should().Contain(metadata =>
            metadata.Contains(initiatorSlot.Id.ToString()));
        acceptedNotifications.Select(n => n.MetadataJson).Should().Contain(metadata =>
            metadata.Contains(targetSlot.Id.ToString()));

        await pushSender.Received(1)
            .SendPushToUserAsync(targetUserId, spaceId, Arg.Is<PushPayload>(p => p.Title.Contains("Swap Proposal")), Arg.Any<CancellationToken>());

        await audit.Received(1).LogAsync(
            spaceId,
            initiatorUserId,
            "self_service.propose_swap",
            "swap_request",
            updatedSwap.Id,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await audit.Received(1).LogAsync(
            spaceId,
            targetUserId,
            "self_service.accept_swap",
            "swap_request",
            updatedSwap.Id,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeclineSwapAsync_NotifiesInitiatorAndAuditsReview()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, guardTaskId, ownerUserId) = SeedBaseData(db);
        var deskTask = AddTask(db, spaceId, groupId, "Desk", ownerUserId);
        var audit = CreateAuditLogger();
        var pushSender = Substitute.For<IPushNotificationSender>();

        var initiatorUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var initiator = Person.Create(spaceId, "Initiator", linkedUserId: initiatorUserId);
        var target = Person.Create(spaceId, "Target", linkedUserId: targetUserId);

        var initiatorSlot = AddSlot(db, spaceId, groupId, guardTaskId, cycleId, daysFromNow: 3);
        var targetSlot = AddSlot(db, spaceId, groupId, deskTask.Id, cycleId, daysFromNow: 4);

        var initiatorRequest = ShiftRequest.Create(spaceId, initiatorSlot.Id, initiator.Id, groupId, cycleId);
        initiatorRequest.Approve();
        var targetRequest = ShiftRequest.Create(spaceId, targetSlot.Id, target.Id, groupId, cycleId);
        targetRequest.Approve();

        var swap = SwapRequest.Create(
            spaceId,
            groupId,
            initiator.Id,
            target.Id,
            initiatorRequest.Id,
            targetRequest.Id);

        db.People.AddRange(initiator, target);
        db.ShiftRequests.AddRange(initiatorRequest, targetRequest);
        db.SwapRequests.Add(swap);
        await db.SaveChangesAsync();

        var service = new ShiftSwapService(
            db,
            pushSender,
            audit,
            TimeProvider.System,
            NullLogger<ShiftSwapService>.Instance);

        await service.DeclineSwapAsync(target.Id, swap.Id);

        var updatedSwap = await db.SwapRequests.SingleAsync(s => s.Id == swap.Id);
        updatedSwap.Status.Should().Be(SwapRequestStatus.Declined);

        var notification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.swap_declined");
        notification.UserId.Should().Be(initiatorUserId);
        notification.MetadataJson.Should().Contain(swap.Id.ToString());

        await pushSender.Received(1)
            .SendPushToUserAsync(initiatorUserId, spaceId, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());

        await audit.Received(1).LogAsync(
            spaceId,
            targetUserId,
            "self_service.decline_swap",
            "swap_request",
            swap.Id,
            Arg.Is<string?>(json => json != null
                && json.Contains(swap.Id.ToString())
                && json.Contains("\"status\":\"pending\"")),
            Arg.Is<string?>(json => json != null
                && json.Contains(swap.Id.ToString())
                && json.Contains("\"status\":\"declined\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelSwapAsync_NotifiesTargetAndAuditsCancellation()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, guardTaskId, ownerUserId) = SeedBaseData(db);
        var deskTask = AddTask(db, spaceId, groupId, "Desk", ownerUserId);
        var audit = CreateAuditLogger();
        var pushSender = Substitute.For<IPushNotificationSender>();

        var initiatorUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var initiator = Person.Create(spaceId, "Initiator", linkedUserId: initiatorUserId);
        var target = Person.Create(spaceId, "Target", linkedUserId: targetUserId);

        var initiatorSlot = AddSlot(db, spaceId, groupId, guardTaskId, cycleId, daysFromNow: 3);
        var targetSlot = AddSlot(db, spaceId, groupId, deskTask.Id, cycleId, daysFromNow: 4);

        var initiatorRequest = ShiftRequest.Create(spaceId, initiatorSlot.Id, initiator.Id, groupId, cycleId);
        initiatorRequest.Approve();
        var targetRequest = ShiftRequest.Create(spaceId, targetSlot.Id, target.Id, groupId, cycleId);
        targetRequest.Approve();

        var swap = SwapRequest.Create(
            spaceId,
            groupId,
            initiator.Id,
            target.Id,
            initiatorRequest.Id,
            targetRequest.Id);

        db.People.AddRange(initiator, target);
        db.ShiftRequests.AddRange(initiatorRequest, targetRequest);
        db.SwapRequests.Add(swap);
        await db.SaveChangesAsync();

        var service = new ShiftSwapService(
            db,
            pushSender,
            audit,
            TimeProvider.System,
            NullLogger<ShiftSwapService>.Instance);

        await service.CancelSwapAsync(initiator.Id, swap.Id);

        var updatedSwap = await db.SwapRequests.SingleAsync(s => s.Id == swap.Id);
        updatedSwap.Status.Should().Be(SwapRequestStatus.Cancelled);

        var notification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.swap_cancelled");
        notification.UserId.Should().Be(targetUserId);
        notification.MetadataJson.Should().Contain(swap.Id.ToString());

        await pushSender.Received(1)
            .SendPushToUserAsync(targetUserId, spaceId, Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());

        await audit.Received(1).LogAsync(
            spaceId,
            initiatorUserId,
            "self_service.cancel_swap",
            "swap_request",
            swap.Id,
            Arg.Is<string?>(json => json != null
                && json.Contains(swap.Id.ToString())
                && json.Contains("\"status\":\"pending\"")),
            Arg.Is<string?>(json => json != null
                && json.Contains(swap.Id.ToString())
                && json.Contains("\"status\":\"cancelled\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdminAssignShiftCommand_CreatesNotification_ForAssignedMember()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId, _) = SeedBaseData(db);
        var assignedUserId = Guid.NewGuid();
        var member = Person.Create(spaceId, "Assigned Member", linkedUserId: assignedUserId);
        var slot = AddSlot(db, spaceId, groupId, taskId, cycleId, daysFromNow: 2);

        db.People.Add(member);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, groupId, member.Id));
        await db.SaveChangesAsync();

        var permissions = Substitute.For<IPermissionService>();
        permissions
            .RequirePermissionAsync(Arg.Any<Guid>(), spaceId, Permissions.SchedulePublish, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var audit = CreateAuditLogger();

        var handler = new AdminAssignShiftCommandHandler(
            db,
            permissions,
            audit,
            Substitute.For<IPushNotificationSender>(),
            NullLogger<AdminAssignShiftCommandHandler>.Instance);

        var requestingUserId = Guid.NewGuid();
        var result = await handler.Handle(
            new AdminAssignShiftCommand(spaceId, groupId, slot.Id, member.Id, requestingUserId),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var notification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.admin_assigned");
        notification.UserId.Should().Be(assignedUserId);
        notification.MetadataJson.Should().Contain(slot.Id.ToString());

        await audit.Received(1).LogAsync(
            spaceId,
            requestingUserId,
            "self_service.admin_assign_shift",
            "shift_request",
            result.ShiftRequestId,
            Arg.Is<string?>(json => json == null),
            Arg.Is<string?>(json => json != null
                && json.Contains(slot.Id.ToString())
                && json.Contains(member.Id.ToString())
                && json.Contains("\"is_admin_override\":true")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdminAssignShiftCommand_AcceptsMatchingActiveWaitlistEntry()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId, _) = SeedBaseData(db);
        var member = Person.Create(spaceId, "Waitlisted Member", linkedUserId: Guid.NewGuid());
        var slot = AddSlot(db, spaceId, groupId, taskId, cycleId, daysFromNow: 2);
        var entry = WaitlistEntry.Create(spaceId, slot.Id, member.Id, position: 1);

        db.People.Add(member);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, groupId, member.Id));
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var permissions = Substitute.For<IPermissionService>();
        permissions
            .RequirePermissionAsync(Arg.Any<Guid>(), spaceId, Permissions.SchedulePublish, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var audit = CreateAuditLogger();

        var handler = new AdminAssignShiftCommandHandler(
            db,
            permissions,
            audit,
            Substitute.For<IPushNotificationSender>(),
            NullLogger<AdminAssignShiftCommandHandler>.Instance);

        var result = await handler.Handle(
            new AdminAssignShiftCommand(spaceId, groupId, slot.Id, member.Id, Guid.NewGuid()),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var updatedEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == entry.Id);
        updatedEntry.Status.Should().Be(WaitlistEntryStatus.Accepted);
        var hasApprovedOverride = await db.ShiftRequests.AnyAsync(r =>
            r.ShiftSlotId == slot.Id
            && r.PersonId == member.Id
            && r.Status == ShiftRequestStatus.Approved
            && r.IsAdminOverride);
        hasApprovedOverride.Should().BeTrue();

        await audit.Received(1).LogAsync(
            spaceId,
            Arg.Any<Guid?>(),
            "self_service.admin_assign_shift",
            "shift_request",
            result.ShiftRequestId,
            Arg.Is<string?>(json => json == null),
            Arg.Is<string?>(json => json != null
                && json.Contains(entry.Id.ToString())
                && json.Contains("\"waitlist_entry_accepted\":true")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdminAssignShiftCommand_DoesNotAcceptInactiveWaitlistEntry()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId, _) = SeedBaseData(db);
        var member = Person.Create(spaceId, "Expired Waitlisted Member", linkedUserId: Guid.NewGuid());
        var slot = AddSlot(db, spaceId, groupId, taskId, cycleId, daysFromNow: 2);
        var entry = WaitlistEntry.Create(spaceId, slot.Id, member.Id, position: 1);
        entry.Expire();

        db.People.Add(member);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, groupId, member.Id));
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var permissions = Substitute.For<IPermissionService>();
        permissions
            .RequirePermissionAsync(Arg.Any<Guid>(), spaceId, Permissions.SchedulePublish, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var audit = CreateAuditLogger();

        var handler = new AdminAssignShiftCommandHandler(
            db,
            permissions,
            audit,
            Substitute.For<IPushNotificationSender>(),
            NullLogger<AdminAssignShiftCommandHandler>.Instance);

        var result = await handler.Handle(
            new AdminAssignShiftCommand(spaceId, groupId, slot.Id, member.Id, Guid.NewGuid()),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var updatedEntry = await db.WaitlistEntries.SingleAsync(e => e.Id == entry.Id);
        updatedEntry.Status.Should().Be(WaitlistEntryStatus.Expired);

        var hasApprovedOverride = await db.ShiftRequests.AnyAsync(r =>
            r.ShiftSlotId == slot.Id
            && r.PersonId == member.Id
            && r.Status == ShiftRequestStatus.Approved
            && r.IsAdminOverride);
        hasApprovedOverride.Should().BeTrue();

        await audit.Received(1).LogAsync(
            spaceId,
            Arg.Any<Guid?>(),
            "self_service.admin_assign_shift",
            "shift_request",
            result.ShiftRequestId,
            Arg.Is<string?>(json => json == null),
            Arg.Is<string?>(json => json != null
                && json.Contains("\"waitlist_entry_accepted\":false")
                && !json.Contains(entry.Id.ToString())),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdminRemoveShiftCommand_CreatesNotification_ForRemovedMember()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId, _) = SeedBaseData(db);
        var removedUserId = Guid.NewGuid();
        var member = Person.Create(spaceId, "Removed Member", linkedUserId: removedUserId);
        var slot = AddSlot(db, spaceId, groupId, taskId, cycleId, daysFromNow: 2);
        slot.IncrementFillCount();

        var shiftRequest = ShiftRequest.Create(spaceId, slot.Id, member.Id, groupId, cycleId);
        shiftRequest.Approve();

        db.People.Add(member);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, groupId, member.Id));
        db.ShiftRequests.Add(shiftRequest);
        await db.SaveChangesAsync();

        var permissions = Substitute.For<IPermissionService>();
        permissions
            .RequirePermissionAsync(Arg.Any<Guid>(), spaceId, Permissions.SchedulePublish, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var audit = CreateAuditLogger();

        var handler = new AdminRemoveShiftCommandHandler(
            db,
            permissions,
            audit,
            Substitute.For<IWaitlistService>(),
            Substitute.For<IPushNotificationSender>(),
            NullLogger<AdminRemoveShiftCommandHandler>.Instance);

        var requestingUserId = Guid.NewGuid();
        var result = await handler.Handle(
            new AdminRemoveShiftCommand(spaceId, groupId, slot.Id, member.Id, requestingUserId),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var notification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.admin_removed");
        notification.UserId.Should().Be(removedUserId);
        notification.MetadataJson.Should().Contain(shiftRequest.Id.ToString());

        await audit.Received(1).LogAsync(
            spaceId,
            requestingUserId,
            "self_service.admin_remove_shift",
            "shift_request",
            shiftRequest.Id,
            Arg.Is<string?>(json => json != null
                && json.Contains(slot.Id.ToString())
                && json.Contains("\"status\":\"approved\"")),
            Arg.Is<string?>(json => json != null
                && json.Contains(member.Id.ToString())
                && json.Contains("\"status\":\"cancelled\"")
                && json.Contains("\"cancellation_reason\":\"admin_removed\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    private static IAuditLogger CreateAuditLogger()
    {
        var audit = Substitute.For<IAuditLogger>();
        audit.LogAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return audit;
    }

    private static (Guid spaceId, Guid groupId, Guid cycleId, Guid taskId, Guid ownerUserId) SeedBaseData(AppDbContext db)
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

        var task = AddTask(db, spaceId, group.Id, "Guard", ownerUserId);
        db.SaveChanges();

        return (spaceId, group.Id, cycle.Id, task.Id, ownerUserId);
    }

    private static GroupTask AddTask(
        AppDbContext db,
        Guid spaceId,
        Guid groupId,
        string name,
        Guid createdByUserId)
    {
        var utcNow = DateTime.UtcNow;
        var task = GroupTask.Create(
            spaceId,
            groupId,
            name,
            utcNow,
            utcNow.AddDays(30),
            shiftDurationMinutes: 480,
            requiredHeadcount: 1,
            burdenLevel: TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: createdByUserId);

        db.GroupTasks.Add(task);
        return task;
    }

    private static ShiftSlot AddSlot(
        AppDbContext db,
        Guid spaceId,
        Guid groupId,
        Guid taskId,
        Guid cycleId,
        int daysFromNow)
    {
        var slot = ShiftSlot.Create(
            spaceId,
            groupId,
            taskId,
            shiftTemplateId: Guid.NewGuid(),
            schedulingCycleId: cycleId,
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow)),
            startTime: new TimeOnly(8, 0),
            endTime: new TimeOnly(16, 0),
            capacity: 1);

        db.ShiftSlots.Add(slot);
        return slot;
    }

    private static ShiftSlot AddLateSlot(
        AppDbContext db,
        Guid spaceId,
        Guid groupId,
        Guid taskId,
        Guid cycleId,
        int startsInHours)
    {
        var start = DateTime.UtcNow.AddHours(startsInHours);
        var end = start.AddHours(1);
        var slot = ShiftSlot.Create(
            spaceId,
            groupId,
            taskId,
            shiftTemplateId: Guid.NewGuid(),
            schedulingCycleId: cycleId,
            date: DateOnly.FromDateTime(start),
            startTime: TimeOnly.FromDateTime(start),
            endTime: TimeOnly.FromDateTime(end),
            capacity: 1);

        db.ShiftSlots.Add(slot);
        return slot;
    }

    private static ShiftRequest AddApprovedRequest(
        AppDbContext db,
        Guid spaceId,
        Guid groupId,
        Guid cycleId,
        Guid personId,
        ShiftSlot slot)
    {
        slot.IncrementFillCount();
        var request = ShiftRequest.Create(spaceId, slot.Id, personId, groupId, cycleId);
        request.Approve();
        db.ShiftRequests.Add(request);
        return request;
    }
}
