using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Notifications;
using Jobuler.Application.People.SpecialLeave;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class SpecialLeaveRequestCommandTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Submit_ForLinkedPerson_CreatesPendingRequest()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Ofek", linkedUserId: userId);
        db.People.Add(person);
        await db.SaveChangesAsync();

        var notifications = Substitute.For<INotificationService>();
        var audit = CreateAuditLogger();
        var handler = new SubmitSpecialLeaveRequestCommandHandler(db, notifications, audit);
        var start = DateTime.UtcNow.AddDays(3);

        var requestId = await handler.Handle(new SubmitSpecialLeaveRequestCommand(
            spaceId, person.Id, start, start.AddDays(1), "Wedding", userId), CancellationToken.None);

        var request = await db.SpecialLeaveRequests.SingleAsync(r => r.Id == requestId);
        request.Status.Should().Be(SpecialLeaveRequestStatus.Pending);
        request.PersonId.Should().Be(person.Id);
        request.Reason.Should().Be("Wedding");
        await notifications.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.special_leave_requested",
            "Time-off Requested",
            Arg.Is<string>(body => body.Contains("Ofek") && body.Contains("requested time off")),
            Arg.Is<string>(metadata => metadata.Contains(requestId.ToString()) && metadata.Contains("\"reason\":\"Wedding\"")),
            null,
            Arg.Any<CancellationToken>());

        await audit.Received(1).LogAsync(
            spaceId,
            userId,
            "self_service.submit_special_leave",
            "special_leave_request",
            requestId,
            Arg.Is<string?>(json => json == null),
            Arg.Is<string?>(json => json != null
                && json.Contains(requestId.ToString())
                && json.Contains(person.Id.ToString())
                && json.Contains("\"reason\":\"Wedding\"")
                && json.Contains("\"status\":\"pending\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_CreatesAtHomePresenceWindow()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Ofek", linkedUserId: userId);
        var start = DateTime.UtcNow.AddDays(3);
        var request = SpecialLeaveRequest.Create(
            spaceId, person.Id, start, start.AddDays(1), "Family event", userId);

        db.People.Add(person);
        db.SpecialLeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var cumulative = Substitute.For<ICumulativeTracker>();
        var cache = Substitute.For<ICacheService>();
        var audit = CreateAuditLogger();
        var handler = new ApproveSpecialLeaveRequestCommandHandler(db, cumulative, cache, audit);

        var presenceWindowId = await handler.Handle(new ApproveSpecialLeaveRequestCommand(
            spaceId, request.Id, adminId, "approved"), CancellationToken.None);

        var presence = await db.PresenceWindows.SingleAsync(p => p.Id == presenceWindowId);
        presence.State.Should().Be(PresenceState.AtHome);
        presence.PersonId.Should().Be(person.Id);
        presence.StartsAt.Should().Be(start);
        presence.EndsAt.Should().Be(start.AddDays(1));

        var updatedRequest = await db.SpecialLeaveRequests.SingleAsync(r => r.Id == request.Id);
        updatedRequest.Status.Should().Be(SpecialLeaveRequestStatus.Approved);
        updatedRequest.PresenceWindowId.Should().Be(presenceWindowId);

        await cumulative.Received(1).RecomputeForPersonAsync(spaceId, person.Id, Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveByPatternAsync($"status:{spaceId}:*", Arg.Any<CancellationToken>());

        var notification = await db.Notifications.SingleAsync(n => n.EventType == "self_service.special_leave_approved");
        notification.UserId.Should().Be(userId);
        notification.MetadataJson.Should().Contain(request.Id.ToString());
        notification.MetadataJson.Should().Contain("\"adminNote\":\"approved\"");

        await audit.Received(1).LogAsync(
            spaceId,
            adminId,
            "approve_special_leave_request",
            "special_leave_request",
            request.Id,
            Arg.Is<string?>(json => json == null),
            Arg.Is<string?>(json => json != null
                && json.Contains(request.Id.ToString())
                && json.Contains(person.Id.ToString())
                && json.Contains(presenceWindowId.ToString())),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitThenApprove_CreatesPresenceWindowAndNotifiesMember()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Ofek", displayName: "Ofek I.", linkedUserId: memberUserId);
        var start = DateTime.UtcNow.AddDays(4);
        var end = start.AddDays(2);

        db.People.Add(person);
        await db.SaveChangesAsync();

        var notifications = Substitute.For<INotificationService>();
        var audit = CreateAuditLogger();
        var submitHandler = new SubmitSpecialLeaveRequestCommandHandler(db, notifications, audit);
        var requestId = await submitHandler.Handle(new SubmitSpecialLeaveRequestCommand(
            spaceId,
            person.Id,
            start,
            end,
            "Family trip",
            memberUserId), CancellationToken.None);

        var submittedRequest = await db.SpecialLeaveRequests.SingleAsync(r => r.Id == requestId);
        submittedRequest.Status.Should().Be(SpecialLeaveRequestStatus.Pending);

        var cumulative = Substitute.For<ICumulativeTracker>();
        var cache = Substitute.For<ICacheService>();
        var approveHandler = new ApproveSpecialLeaveRequestCommandHandler(db, cumulative, cache, audit);
        var presenceWindowId = await approveHandler.Handle(new ApproveSpecialLeaveRequestCommand(
            spaceId,
            requestId,
            adminUserId,
            "Enjoy"), CancellationToken.None);

        var approvedRequest = await db.SpecialLeaveRequests.SingleAsync(r => r.Id == requestId);
        approvedRequest.Status.Should().Be(SpecialLeaveRequestStatus.Approved);
        approvedRequest.AdminNote.Should().Be("Enjoy");
        approvedRequest.PresenceWindowId.Should().Be(presenceWindowId);

        var presence = await db.PresenceWindows.SingleAsync(p => p.Id == presenceWindowId);
        presence.PersonId.Should().Be(person.Id);
        presence.SpaceId.Should().Be(spaceId);
        presence.State.Should().Be(PresenceState.AtHome);
        presence.StartsAt.Should().Be(start);
        presence.EndsAt.Should().Be(end);
        presence.Note.Should().Contain("Family trip");
        presence.Note.Should().Contain("Enjoy");

        var memberNotification = await db.Notifications
            .SingleAsync(n => n.EventType == "self_service.special_leave_approved");
        memberNotification.UserId.Should().Be(memberUserId);
        memberNotification.MetadataJson.Should().Contain(requestId.ToString());
        memberNotification.MetadataJson.Should().Contain("\"adminNote\":\"Enjoy\"");

        await notifications.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.special_leave_requested",
            "Time-off Requested",
            Arg.Is<string>(body => body.Contains("Ofek I.") && body.Contains("requested time off")),
            Arg.Is<string>(metadata => metadata.Contains(requestId.ToString()) && metadata.Contains("\"reason\":\"Family trip\"")),
            null,
            Arg.Any<CancellationToken>());
        await cumulative.Received(1).RecomputeForPersonAsync(spaceId, person.Id, Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveByPatternAsync($"status:{spaceId}:*", Arg.Any<CancellationToken>());
        await audit.Received(1).LogAsync(
            spaceId,
            memberUserId,
            "self_service.submit_special_leave",
            "special_leave_request",
            requestId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await audit.Received(1).LogAsync(
            spaceId,
            adminUserId,
            "approve_special_leave_request",
            "special_leave_request",
            requestId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reject_NotifiesLinkedMember()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Ofek", linkedUserId: userId);
        var start = DateTime.UtcNow.AddDays(3);
        var request = SpecialLeaveRequest.Create(
            spaceId, person.Id, start, start.AddDays(1), "Family event", userId);

        db.People.Add(person);
        db.SpecialLeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var audit = CreateAuditLogger();
        var handler = new RejectSpecialLeaveRequestCommandHandler(db, audit);

        await handler.Handle(new RejectSpecialLeaveRequestCommand(
            spaceId, request.Id, adminId, "not this week"), CancellationToken.None);

        var updatedRequest = await db.SpecialLeaveRequests.SingleAsync(r => r.Id == request.Id);
        updatedRequest.Status.Should().Be(SpecialLeaveRequestStatus.Rejected);

        var notification = await db.Notifications.SingleAsync(n => n.EventType == "self_service.special_leave_rejected");
        notification.UserId.Should().Be(userId);
        notification.MetadataJson.Should().Contain(request.Id.ToString());
        notification.MetadataJson.Should().Contain("\"adminNote\":\"not this week\"");

        await audit.Received(1).LogAsync(
            spaceId,
            adminId,
            "reject_special_leave_request",
            "special_leave_request",
            request.Id,
            Arg.Is<string?>(json => json == null),
            Arg.Is<string?>(json => json != null
                && json.Contains(request.Id.ToString())
                && json.Contains(person.Id.ToString())
                && json.Contains("\"admin_note\":\"not this week\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_RejectsOverlappingActiveRequest()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Ofek", linkedUserId: userId);
        var start = DateTime.UtcNow.AddDays(3);
        db.People.Add(person);
        db.SpecialLeaveRequests.Add(SpecialLeaveRequest.Create(
            spaceId, person.Id, start, start.AddDays(1), "Family event", userId));
        await db.SaveChangesAsync();

        var handler = new SubmitSpecialLeaveRequestCommandHandler(
            db,
            Substitute.For<INotificationService>(),
            CreateAuditLogger());

        var act = () => handler.Handle(new SubmitSpecialLeaveRequestCommand(
            spaceId, person.Id, start.AddHours(2), start.AddHours(4), "Other event", userId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("An active special leave request already overlaps this time.");
    }

    [Fact]
    public async Task Cancel_NotifiesSpaceAdmins()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Ofek", linkedUserId: userId);
        var start = DateTime.UtcNow.AddDays(3);
        var request = SpecialLeaveRequest.Create(
            spaceId, person.Id, start, start.AddDays(1), "Family event", userId);

        db.People.Add(person);
        db.SpecialLeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var notifications = Substitute.For<INotificationService>();
        var audit = CreateAuditLogger();
        var handler = new CancelSpecialLeaveRequestCommandHandler(db, notifications, audit);

        await handler.Handle(new CancelSpecialLeaveRequestCommand(
            spaceId, request.Id, person.Id), CancellationToken.None);

        var updatedRequest = await db.SpecialLeaveRequests.SingleAsync(r => r.Id == request.Id);
        updatedRequest.Status.Should().Be(SpecialLeaveRequestStatus.Cancelled);
        await notifications.Received(1).NotifySpaceAdminsAsync(
            spaceId,
            "self_service.special_leave_cancelled",
            "Time-off Request Cancelled",
            Arg.Is<string>(body => body.Contains("Ofek") && body.Contains("cancelled a time-off request")),
            Arg.Is<string>(metadata => metadata.Contains(request.Id.ToString()) && metadata.Contains("\"reason\":\"Family event\"")),
            null,
            Arg.Any<CancellationToken>());

        await audit.Received(1).LogAsync(
            spaceId,
            userId,
            "self_service.cancel_special_leave",
            "special_leave_request",
            request.Id,
            Arg.Is<string?>(json => json != null
                && json.Contains(request.Id.ToString())
                && json.Contains("\"status\":\"pending\"")),
            Arg.Is<string?>(json => json != null
                && json.Contains(request.Id.ToString())
                && json.Contains(person.Id.ToString())
                && json.Contains("\"reason\":\"Family event\"")
                && json.Contains("\"status\":\"cancelled\"")),
            Arg.Is<string?>(ipAddress => ipAddress == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_ApprovedRequest_IsRejectedWithoutNotificationsOrAudit()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var presenceWindowId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Ofek", linkedUserId: userId);
        var start = DateTime.UtcNow.AddDays(3);
        var request = SpecialLeaveRequest.Create(
            spaceId, person.Id, start, start.AddDays(1), "Family event", userId);
        request.Approve(adminId, presenceWindowId, "approved");

        db.People.Add(person);
        db.SpecialLeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var notifications = Substitute.For<INotificationService>();
        var audit = CreateAuditLogger();
        var handler = new CancelSpecialLeaveRequestCommandHandler(db, notifications, audit);

        var act = () => handler.Handle(new CancelSpecialLeaveRequestCommand(
            spaceId, request.Id, person.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only pending special leave requests can be changed.");

        var unchangedRequest = await db.SpecialLeaveRequests.SingleAsync(r => r.Id == request.Id);
        unchangedRequest.Status.Should().Be(SpecialLeaveRequestStatus.Approved);
        unchangedRequest.PresenceWindowId.Should().Be(presenceWindowId);

        await notifications.DidNotReceiveWithAnyArgs()
            .NotifySpaceAdminsAsync(default, default!, default!, default!, default!, default, default);
        await audit.DidNotReceiveWithAnyArgs()
            .LogAsync(default, default, default!, default, default, default, default, default, default);
    }

    [Fact]
    public async Task GetMyRequests_ReturnsProjectedRequestsForLinkedPerson()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Ofek", displayName: "Ofek L.", linkedUserId: userId);
        var otherPerson = Person.Create(spaceId, "Other", linkedUserId: Guid.NewGuid());
        var start = DateTime.UtcNow.AddDays(3);

        db.People.AddRange(person, otherPerson);
        db.SpecialLeaveRequests.Add(SpecialLeaveRequest.Create(
            spaceId, person.Id, start, start.AddDays(1), "Family event", userId));
        db.SpecialLeaveRequests.Add(SpecialLeaveRequest.Create(
            spaceId, otherPerson.Id, start, start.AddDays(1), "Other event", Guid.NewGuid()));
        await db.SaveChangesAsync();

        var handler = new GetMySpecialLeaveRequestsQueryHandler(db);

        var result = await handler.Handle(new GetMySpecialLeaveRequestsQuery(
            spaceId, person.Id, start.AddHours(-1), start.AddDays(2)), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].PersonId.Should().Be(person.Id);
        result[0].PersonName.Should().Be("Ofek L.");
        result[0].Reason.Should().Be("Family event");
        result[0].Status.Should().Be(nameof(SpecialLeaveRequestStatus.Pending));
    }

    [Fact]
    public async Task GetAdminRequests_WithGroupId_ReturnsOnlyGroupMembers()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Route Group");
        var otherGroup = Group.Create(spaceId, null, "Other Group");
        var memberUserId = Guid.NewGuid();
        var otherMemberUserId = Guid.NewGuid();
        var member = Person.Create(spaceId, "Member", linkedUserId: memberUserId);
        var otherMember = Person.Create(spaceId, "Other Member", linkedUserId: otherMemberUserId);
        var start = DateTime.UtcNow.AddDays(3);
        var request = SpecialLeaveRequest.Create(
            spaceId, member.Id, start, start.AddDays(1), "Family event", memberUserId);
        var otherRequest = SpecialLeaveRequest.Create(
            spaceId, otherMember.Id, start, start.AddDays(1), "Other event", otherMemberUserId);

        db.Groups.AddRange(group, otherGroup);
        db.People.AddRange(member, otherMember);
        db.GroupMemberships.AddRange(
            GroupMembership.Create(spaceId, group.Id, member.Id),
            GroupMembership.Create(spaceId, otherGroup.Id, otherMember.Id));
        db.SpecialLeaveRequests.AddRange(request, otherRequest);
        await db.SaveChangesAsync();

        var handler = new GetSpecialLeaveRequestsForAdminQueryHandler(db);

        var result = await handler.Handle(new GetSpecialLeaveRequestsForAdminQuery(
            spaceId,
            Status: nameof(SpecialLeaveRequestStatus.Pending),
            GroupId: group.Id), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(request.Id);
        result[0].PersonId.Should().Be(member.Id);
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
}
