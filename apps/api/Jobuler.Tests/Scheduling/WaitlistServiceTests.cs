using FluentAssertions;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling.SelfService;
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

    private static WaitlistService CreateService(AppDbContext db) =>
        new(
            db,
            Substitute.For<IPushNotificationSender>(),
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
