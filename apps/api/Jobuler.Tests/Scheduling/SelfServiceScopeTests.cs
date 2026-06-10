using FluentAssertions;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Scheduling.SelfService.Models;
using Jobuler.Application.Scheduling.SelfService.Queries;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class SelfServiceScopeTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetMyWaitlistEntriesQuery_ReturnsOnlyEntriesForRequestedGroup()
    {
        using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Member", linkedUserId: Guid.NewGuid());
        var groupA = Group.Create(spaceId, null, "Group A");
        var groupB = Group.Create(spaceId, null, "Group B");
        var cycleA = CreateCycle(spaceId, groupA.Id);
        var cycleB = CreateCycle(spaceId, groupB.Id);
        var taskA = CreateTask(spaceId, groupA.Id, "A", ownerUserId);
        var taskB = CreateTask(spaceId, groupB.Id, "B", ownerUserId);
        var slotA = CreateSlot(spaceId, groupA.Id, taskA.Id, cycleA.Id, daysFromNow: 2);
        var slotB = CreateSlot(spaceId, groupB.Id, taskB.Id, cycleB.Id, daysFromNow: 3);

        db.People.Add(person);
        db.Groups.AddRange(groupA, groupB);
        db.SchedulingCycles.AddRange(cycleA, cycleB);
        db.GroupTasks.AddRange(taskA, taskB);
        db.ShiftSlots.AddRange(slotA, slotB);
        db.WaitlistEntries.AddRange(
            WaitlistEntry.Create(spaceId, slotA.Id, person.Id, position: 1),
            WaitlistEntry.Create(spaceId, slotB.Id, person.Id, position: 1));
        await db.SaveChangesAsync();

        var handler = new GetMyWaitlistEntriesQueryHandler(db);

        var result = await handler.Handle(
            new GetMyWaitlistEntriesQuery(spaceId, groupA.Id, person.Id),
            CancellationToken.None);

        result.Should().ContainSingle();
        result[0].ShiftSlotId.Should().Be(slotA.Id);
        result[0].TaskName.Should().Be("A");
    }

    [Fact]
    public async Task GetAvailableSlotsQuery_ReturnsEmpty_WhenLinkedPersonIsNotGroupMember()
    {
        using var db = CreateDb();
        var availability = Substitute.For<ISlotAvailabilityEngine>();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Outsider", linkedUserId: userId);
        db.People.Add(person);
        await db.SaveChangesAsync();

        var handler = new GetAvailableSlotsQueryHandler(db, availability);

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(spaceId, groupId, Guid.NewGuid(), userId),
            CancellationToken.None);

        result.Slots.Should().BeEmpty();
        await availability.DidNotReceiveWithAnyArgs()
            .GetAvailableSlotsAsync(default, default, default, default);
    }

    private static SchedulingCycle CreateCycle(Guid spaceId, Guid groupId)
    {
        var utcNow = DateTime.UtcNow;
        return SchedulingCycle.Create(
            spaceId,
            groupId,
            startsAt: utcNow.AddDays(1),
            endsAt: utcNow.AddDays(8),
            requestWindowOpensAt: utcNow.AddDays(-2),
            requestWindowClosesAt: utcNow.AddHours(1));
    }

    private static GroupTask CreateTask(Guid spaceId, Guid groupId, string name, Guid createdByUserId)
    {
        var utcNow = DateTime.UtcNow;
        return GroupTask.Create(
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
}
