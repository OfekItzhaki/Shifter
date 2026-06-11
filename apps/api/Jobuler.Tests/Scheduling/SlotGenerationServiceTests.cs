using FluentAssertions;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class SlotGenerationServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GenerateSlotsForCycleAsync_CreatesSlotsFromActiveTemplatesOnMatchingWeekdays()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Operations");
        group.SetSchedulingMode(SchedulingMode.SelfService);
        var task = CreateTask(spaceId, group.Id, "Desk");
        var cycle = SchedulingCycle.Create(
            spaceId,
            group.Id,
            startsAt: new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            endsAt: new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc),
            requestWindowOpensAt: new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc),
            requestWindowClosesAt: new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc));
        var mondayTemplate = ShiftTemplate.Create(
            spaceId,
            group.Id,
            task.Id,
            DayOfWeek.Monday,
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            requiredHeadcount: 2,
            createdByUserId: Guid.NewGuid());
        var wednesdayTemplate = ShiftTemplate.Create(
            spaceId,
            group.Id,
            task.Id,
            DayOfWeek.Wednesday,
            new TimeOnly(10, 0),
            new TimeOnly(18, 0),
            requiredHeadcount: 1,
            createdByUserId: Guid.NewGuid());

        db.Groups.Add(group);
        db.GroupTasks.Add(task);
        db.SchedulingCycles.Add(cycle);
        db.ShiftTemplates.AddRange(mondayTemplate, wednesdayTemplate);
        await db.SaveChangesAsync();

        var service = new SlotGenerationService(db, NullLogger<SlotGenerationService>.Instance);

        await service.GenerateSlotsForCycleAsync(group.Id, cycle.Id);

        var slots = await db.ShiftSlots
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .ToListAsync();

        slots.Should().HaveCount(4);
        slots.Select(s => s.Date).Should().Equal(
            new DateOnly(2026, 6, 15),
            new DateOnly(2026, 6, 17),
            new DateOnly(2026, 6, 22),
            new DateOnly(2026, 6, 24));
        slots.Where(s => s.ShiftTemplateId == mondayTemplate.Id).Should().OnlyContain(s =>
            s.StartTime == new TimeOnly(9, 0)
            && s.EndTime == new TimeOnly(17, 0)
            && s.StartsAt == DateTime.SpecifyKind(s.Date.ToDateTime(new TimeOnly(9, 0)), DateTimeKind.Utc)
            && s.EndsAt == DateTime.SpecifyKind(s.Date.ToDateTime(new TimeOnly(17, 0)), DateTimeKind.Utc)
            && s.Capacity == 2
            && s.CurrentFillCount == 0);
        slots.Where(s => s.ShiftTemplateId == wednesdayTemplate.Id).Should().OnlyContain(s =>
            s.StartTime == new TimeOnly(10, 0)
            && s.EndTime == new TimeOnly(18, 0)
            && s.StartsAt == DateTime.SpecifyKind(s.Date.ToDateTime(new TimeOnly(10, 0)), DateTimeKind.Utc)
            && s.EndsAt == DateTime.SpecifyKind(s.Date.ToDateTime(new TimeOnly(18, 0)), DateTimeKind.Utc)
            && s.Capacity == 1
            && s.CurrentFillCount == 0);

        var generatedCycle = await db.SchedulingCycles.SingleAsync(c => c.Id == cycle.Id);
        generatedCycle.IsGenerated.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateSlotsForCycleAsync_IsIdempotentAndSkipsDeletedOrInactiveTemplates()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Operations");
        group.SetSchedulingMode(SchedulingMode.SelfService);
        var activeTask = CreateTask(spaceId, group.Id, "Desk");
        var inactiveTask = CreateTask(spaceId, group.Id, "Inactive");
        inactiveTask.Deactivate(Guid.NewGuid());
        var cycle = SchedulingCycle.Create(
            spaceId,
            group.Id,
            startsAt: new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            endsAt: new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc),
            requestWindowOpensAt: new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc),
            requestWindowClosesAt: new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc));
        var activeTemplate = ShiftTemplate.Create(
            spaceId,
            group.Id,
            activeTask.Id,
            DayOfWeek.Monday,
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            requiredHeadcount: 2,
            createdByUserId: Guid.NewGuid());
        var deletedTemplate = ShiftTemplate.Create(
            spaceId,
            group.Id,
            activeTask.Id,
            DayOfWeek.Monday,
            new TimeOnly(11, 0),
            new TimeOnly(19, 0),
            requiredHeadcount: 1,
            createdByUserId: Guid.NewGuid());
        deletedTemplate.SoftDelete();
        var inactiveTaskTemplate = ShiftTemplate.Create(
            spaceId,
            group.Id,
            inactiveTask.Id,
            DayOfWeek.Monday,
            new TimeOnly(12, 0),
            new TimeOnly(20, 0),
            requiredHeadcount: 1,
            createdByUserId: Guid.NewGuid());

        db.Groups.Add(group);
        db.GroupTasks.AddRange(activeTask, inactiveTask);
        db.SchedulingCycles.Add(cycle);
        db.ShiftTemplates.AddRange(activeTemplate, deletedTemplate, inactiveTaskTemplate);
        await db.SaveChangesAsync();

        var service = new SlotGenerationService(db, NullLogger<SlotGenerationService>.Instance);

        await service.GenerateSlotsForCycleAsync(group.Id, cycle.Id);
        await service.GenerateSlotsForCycleAsync(group.Id, cycle.Id);

        var slots = await db.ShiftSlots.ToListAsync();
        slots.Should().ContainSingle();
        slots.Single().ShiftTemplateId.Should().Be(activeTemplate.Id);
        slots.Single().Date.Should().Be(new DateOnly(2026, 6, 15));
    }

    private static GroupTask CreateTask(Guid spaceId, Guid groupId, string name)
    {
        var startsAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        return GroupTask.Create(
            spaceId,
            groupId,
            name,
            startsAt,
            startsAt.AddDays(30),
            shiftDurationMinutes: 480,
            requiredHeadcount: 1,
            burdenLevel: TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: Guid.NewGuid());
    }
}
