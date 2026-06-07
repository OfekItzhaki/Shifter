using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.People.SpecialLeave;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
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

        var handler = new SubmitSpecialLeaveRequestCommandHandler(db);
        var start = DateTime.UtcNow.AddDays(3);

        var requestId = await handler.Handle(new SubmitSpecialLeaveRequestCommand(
            spaceId, person.Id, start, start.AddDays(1), "Wedding", userId), CancellationToken.None);

        var request = await db.SpecialLeaveRequests.SingleAsync(r => r.Id == requestId);
        request.Status.Should().Be(SpecialLeaveRequestStatus.Pending);
        request.PersonId.Should().Be(person.Id);
        request.Reason.Should().Be("Wedding");
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
        var audit = Substitute.For<IAuditLogger>();
        var queue = Substitute.For<ISolverJobQueue>();
        queue.EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new ApproveSpecialLeaveRequestCommandHandler(db, cumulative, cache, audit, queue);

        var result = await handler.Handle(new ApproveSpecialLeaveRequestCommand(
            spaceId, request.Id, adminId, "approved"), CancellationToken.None);

        result.RegenerationRunIds.Should().BeEmpty();

        var presence = await db.PresenceWindows.SingleAsync(p => p.Id == result.PresenceWindowId);
        presence.State.Should().Be(PresenceState.AtHome);
        presence.PersonId.Should().Be(person.Id);
        presence.StartsAt.Should().Be(start);
        presence.EndsAt.Should().Be(start.AddDays(1));

        var updatedRequest = await db.SpecialLeaveRequests.SingleAsync(r => r.Id == request.Id);
        updatedRequest.Status.Should().Be(SpecialLeaveRequestStatus.Approved);
        updatedRequest.PresenceWindowId.Should().Be(result.PresenceWindowId);

        await cumulative.Received(1).RecomputeForPersonAsync(spaceId, person.Id, Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveByPatternAsync($"status:{spaceId}:*", Arg.Any<CancellationToken>());
        await queue.DidNotReceive().EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_WhenLeaveOverlapsPublishedAssignment_QueuesRegenerationForAffectedGroup()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Platoon", createdByUserId: adminId);
        var person = Person.Create(spaceId, "Ofek", linkedUserId: userId);
        var taskStart = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var task = GroupTask.Create(
            spaceId,
            group.Id,
            "Guard",
            taskStart,
            taskStart.AddDays(2),
            shiftDurationMinutes: 240,
            requiredHeadcount: 1,
            TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: adminId);
        var published = ScheduleVersion.CreateDraft(spaceId, 1, null, null, adminId);
        published.Publish(adminId);
        var assignedSlotId = DeriveShiftGuid(task.Id, 2); // 08:00-12:00 on the first day
        var request = SpecialLeaveRequest.Create(
            spaceId, person.Id, taskStart.AddHours(9), taskStart.AddHours(11), "Family event", userId);

        db.Groups.Add(group);
        db.People.Add(person);
        db.GroupTasks.Add(task);
        db.ScheduleVersions.Add(published);
        db.Assignments.Add(Assignment.Create(spaceId, published.Id, assignedSlotId, person.Id));
        db.SpecialLeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var queue = Substitute.For<ISolverJobQueue>();
        queue.EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new ApproveSpecialLeaveRequestCommandHandler(
            db,
            Substitute.For<ICumulativeTracker>(),
            Substitute.For<ICacheService>(),
            Substitute.For<IAuditLogger>(),
            queue);

        var result = await handler.Handle(new ApproveSpecialLeaveRequestCommand(
            spaceId, request.Id, adminId, "approved"), CancellationToken.None);

        var run = await db.ScheduleRuns.SingleAsync();
        result.RegenerationRunIds.Should().Equal(run.Id);
        run.TriggerType.Should().Be(ScheduleRunTrigger.Regeneration);
        run.BaselineVersionId.Should().Be(published.Id);
        run.GroupId.Should().Be(group.Id);
        run.RequestedByUserId.Should().Be(adminId);

        await queue.Received(1).EnqueueAsync(
            Arg.Is<SolverJobMessage>(job =>
                job.RunId == run.Id
                && job.SpaceId == spaceId
                && job.TriggerMode == "regeneration"
                && job.BaselineVersionId == published.Id
                && job.GroupId == group.Id
                && job.StartTime == request.StartsAt.Date),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_WhenLeaveDoesNotOverlapPublishedAssignment_DoesNotQueueRegeneration()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Platoon", createdByUserId: adminId);
        var person = Person.Create(spaceId, "Ofek", linkedUserId: userId);
        var taskStart = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var task = GroupTask.Create(
            spaceId,
            group.Id,
            "Guard",
            taskStart,
            taskStart.AddDays(1),
            shiftDurationMinutes: 240,
            requiredHeadcount: 1,
            TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: adminId);
        var published = ScheduleVersion.CreateDraft(spaceId, 1, null, null, adminId);
        published.Publish(adminId);
        var assignedSlotId = DeriveShiftGuid(task.Id, 1); // 04:00-08:00
        var request = SpecialLeaveRequest.Create(
            spaceId, person.Id, taskStart.AddHours(12), taskStart.AddHours(14), "Family event", userId);

        db.Groups.Add(group);
        db.People.Add(person);
        db.GroupTasks.Add(task);
        db.ScheduleVersions.Add(published);
        db.Assignments.Add(Assignment.Create(spaceId, published.Id, assignedSlotId, person.Id));
        db.SpecialLeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var queue = Substitute.For<ISolverJobQueue>();
        queue.EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new ApproveSpecialLeaveRequestCommandHandler(
            db,
            Substitute.For<ICumulativeTracker>(),
            Substitute.For<ICacheService>(),
            Substitute.For<IAuditLogger>(),
            queue);

        await handler.Handle(new ApproveSpecialLeaveRequestCommand(
            spaceId, request.Id, adminId, "approved"), CancellationToken.None);

        (await db.ScheduleRuns.CountAsync()).Should().Be(0);
        await queue.DidNotReceive().EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>());
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

        var handler = new SubmitSpecialLeaveRequestCommandHandler(db);

        var act = () => handler.Handle(new SubmitSpecialLeaveRequestCommand(
            spaceId, person.Id, start.AddHours(2), start.AddHours(4), "Other event", userId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("An active special leave request already overlaps this time.");
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

    private static Guid DeriveShiftGuid(Guid taskId, int shiftIndex)
    {
        var bytes = taskId.ToByteArray();
        var indexBytes = BitConverter.GetBytes(shiftIndex);
        for (var i = 0; i < 4; i++)
            bytes[12 + i] ^= indexBytes[i];
        return new Guid(bytes);
    }
}
