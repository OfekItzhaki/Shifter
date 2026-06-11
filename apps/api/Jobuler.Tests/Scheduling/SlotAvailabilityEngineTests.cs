using FluentAssertions;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class SlotAvailabilityEngineTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (Guid spaceId, Guid groupId, Guid cycleId, Guid taskId) SeedBaseData(
        AppDbContext db,
        bool windowOpen = true)
    {
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();

        var group = Group.Create(spaceId, null, "Test Group");
        // Use reflection to set the Id since Group.Create generates one
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!
            .SetValue(group, groupId);
        group.SetSchedulingMode(SchedulingMode.SelfService);
        db.Groups.Add(group);

        var utcNow = DateTime.UtcNow;
        var cycle = SchedulingCycle.Create(
            spaceId,
            groupId,
            startsAt: utcNow.AddDays(7),
            endsAt: utcNow.AddDays(14),
            requestWindowOpensAt: windowOpen ? utcNow.AddDays(-1) : utcNow.AddDays(1),
            requestWindowClosesAt: windowOpen ? utcNow.AddDays(5) : utcNow.AddDays(6));

        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!
            .SetValue(cycle, cycleId);
        db.SchedulingCycles.Add(cycle);

        var task = GroupTask.Create(
            spaceId, groupId, "Morning Shift",
            utcNow, utcNow.AddDays(30),
            shiftDurationMinutes: 480,
            requiredHeadcount: 2,
            burdenLevel: TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: Guid.NewGuid());

        var taskId = task.Id;
        db.GroupTasks.Add(task);

        db.SaveChanges();
        return (spaceId, groupId, cycleId, taskId);
    }

    private static ShiftSlot AddSlot(
        AppDbContext db,
        Guid spaceId, Guid groupId, Guid taskId, Guid cycleId,
        DateOnly date, TimeOnly startTime, TimeOnly endTime,
        int capacity = 3, int fillCount = 0)
    {
        var slot = ShiftSlot.Create(
            spaceId, groupId, taskId,
            shiftTemplateId: Guid.NewGuid(),
            schedulingCycleId: cycleId,
            date: date,
            startTime: startTime,
            endTime: endTime,
            capacity: capacity);

        for (int i = 0; i < fillCount; i++)
            slot.IncrementFillCount();

        db.ShiftSlots.Add(slot);
        db.SaveChanges();
        return slot;
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ReturnsEmptyList_WhenCycleNotFound()
    {
        using var db = CreateDb();
        var engine = new SlotAvailabilityEngine(db);

        var result = await engine.GetAvailableSlotsAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        result.Slots.Should().BeEmpty();
        result.IsReadOnly.Should().BeFalse();
        result.Message.Should().BeNull();
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ReturnsEmptyList_WhenNoSlots()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, _) = SeedBaseData(db);
        var engine = new SlotAvailabilityEngine(db);

        var result = await engine.GetAvailableSlotsAsync(Guid.NewGuid(), groupId, cycleId);

        result.Slots.Should().BeEmpty();
        result.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ReturnsSlotsWithRemainingCapacity()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(8, 0), new TimeOnly(16, 0), capacity: 3, fillCount: 1);

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(Guid.NewGuid(), groupId, cycleId);

        result.Slots.Should().HaveCount(1);
        result.Slots[0].CurrentFillCount.Should().Be(1);
        result.Slots[0].Capacity.Should().Be(3);
        result.Slots[0].TaskName.Should().Be("Morning Shift");
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ExcludesFullSlots()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        // Full slot (capacity 2, fill 2)
        AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(8, 0), new TimeOnly(16, 0), capacity: 2, fillCount: 2);
        // Available slot
        AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(16, 0), new TimeOnly(22, 0), capacity: 3, fillCount: 1);

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(Guid.NewGuid(), groupId, cycleId);

        result.Slots.Should().HaveCount(1);
        result.Slots[0].StartTime.Should().Be(new TimeOnly(16, 0));
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_IncludesFullSlots_WhenRequestedForWaitlistBrowse()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        var fullSlot = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(8, 0), new TimeOnly(16, 0), capacity: 2, fillCount: 2);
        var availableSlot = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(16, 0), new TimeOnly(22, 0), capacity: 3, fillCount: 1);

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(
            Guid.NewGuid(),
            groupId,
            cycleId,
            includeFullSlots: true);

        result.Slots.Select(s => s.ShiftSlotId).Should()
            .Contain(fullSlot.Id)
            .And.Contain(availableSlot.Id);
        result.Slots.Single(s => s.ShiftSlotId == fullSlot.Id).CurrentFillCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ExcludesStartedSlots()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);

        var startedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        var startedSlot = AddSlot(db, spaceId, groupId, taskId, cycleId, startedDate,
            new TimeOnly(8, 0), new TimeOnly(16, 0));
        var futureSlot = AddSlot(db, spaceId, groupId, taskId, cycleId, futureDate,
            new TimeOnly(8, 0), new TimeOnly(16, 0));

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(Guid.NewGuid(), groupId, cycleId);

        result.Slots.Select(s => s.ShiftSlotId).Should()
            .Contain(futureSlot.Id)
            .And.NotContain(startedSlot.Id);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ExcludesSlotsWithMemberPendingOrApprovedRequest()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var group = await db.Groups.SingleAsync(g => g.Id == groupId);
        group.SetMinRestBetweenShifts(0);
        var personId = Guid.NewGuid();

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        var slot1 = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(8, 0), new TimeOnly(12, 0));
        var slot2 = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(12, 0), new TimeOnly(16, 0));
        var slot3 = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(16, 0), new TimeOnly(20, 0));

        // Person has a pending request on slot1
        var req1 = ShiftRequest.Create(spaceId, slot1.Id, personId, groupId, cycleId);
        db.ShiftRequests.Add(req1);

        // Person has an approved request on slot2
        var req2 = ShiftRequest.Create(spaceId, slot2.Id, personId, groupId, cycleId);
        req2.Approve();
        db.ShiftRequests.Add(req2);

        await db.SaveChangesAsync();

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(personId, groupId, cycleId);

        // Only slot3 should be returned
        result.Slots.Should().HaveCount(1);
        result.Slots[0].ShiftSlotId.Should().Be(slot3.Id);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ExcludesOverlappingSlots_ExclusiveEndpoints()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var group = await db.Groups.SingleAsync(g => g.Id == groupId);
        group.SetMinRestBetweenShifts(0);
        var personId = Guid.NewGuid();

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));

        // Person's approved shift: 08:00 - 12:00
        var approvedSlot = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(8, 0), new TimeOnly(12, 0));
        var approvedReq = ShiftRequest.Create(spaceId, approvedSlot.Id, personId, groupId, cycleId);
        approvedReq.Approve();
        db.ShiftRequests.Add(approvedReq);

        // Slot that overlaps: 10:00 - 14:00 (overlaps with 08:00-12:00)
        var overlappingSlot = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(10, 0), new TimeOnly(14, 0));

        // Slot that starts exactly when approved ends: 12:00 - 16:00 (no overlap, exclusive endpoints)
        var adjacentSlot = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(12, 0), new TimeOnly(16, 0));

        // Slot on a different date: should not be excluded
        var differentDate = date.AddDays(1);
        var differentDaySlot = AddSlot(db, spaceId, groupId, taskId, cycleId, differentDate,
            new TimeOnly(9, 0), new TimeOnly(13, 0));

        await db.SaveChangesAsync();

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(personId, groupId, cycleId);

        // Should include adjacentSlot and differentDaySlot, but NOT overlappingSlot or approvedSlot
        result.Slots.Should().HaveCount(2);
        result.Slots.Select(s => s.ShiftSlotId).Should()
            .Contain(adjacentSlot.Id)
            .And.Contain(differentDaySlot.Id)
            .And.NotContain(overlappingSlot.Id);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ExcludesSlotsInsideMinimumRestWindow()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var group = await db.Groups.SingleAsync(g => g.Id == groupId);
        group.SetMinRestBetweenShifts(8);
        var personId = Guid.NewGuid();

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));

        var approvedSlot = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(8, 0), new TimeOnly(12, 0));
        var approvedReq = ShiftRequest.Create(spaceId, approvedSlot.Id, personId, groupId, cycleId);
        approvedReq.Approve();
        db.ShiftRequests.Add(approvedReq);

        var restViolationSlot = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(18, 0), new TimeOnly(22, 0));
        var safeSlot = AddSlot(db, spaceId, groupId, taskId, cycleId, date.AddDays(1),
            new TimeOnly(8, 0), new TimeOnly(12, 0));

        await db.SaveChangesAsync();

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(personId, groupId, cycleId);

        result.Slots.Select(s => s.ShiftSlotId).Should()
            .Contain(safeSlot.Id)
            .And.NotContain(restViolationSlot.Id);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_SortsByDateThenStartTime()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);

        var date1 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9));
        var date2 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));

        // Add slots out of order
        AddSlot(db, spaceId, groupId, taskId, cycleId, date1,
            new TimeOnly(14, 0), new TimeOnly(18, 0));
        AddSlot(db, spaceId, groupId, taskId, cycleId, date2,
            new TimeOnly(16, 0), new TimeOnly(20, 0));
        AddSlot(db, spaceId, groupId, taskId, cycleId, date2,
            new TimeOnly(8, 0), new TimeOnly(12, 0));

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(Guid.NewGuid(), groupId, cycleId);

        result.Slots.Should().HaveCount(3);
        // date2 comes first (earlier date), then sorted by start time
        result.Slots[0].Date.Should().Be(date2);
        result.Slots[0].StartTime.Should().Be(new TimeOnly(8, 0));
        result.Slots[1].Date.Should().Be(date2);
        result.Slots[1].StartTime.Should().Be(new TimeOnly(16, 0));
        result.Slots[2].Date.Should().Be(date1);
        result.Slots[2].StartTime.Should().Be(new TimeOnly(14, 0));
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_IncludesAllRequiredFields()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        var slot = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(8, 0), new TimeOnly(16, 0), capacity: 5, fillCount: 2);

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(Guid.NewGuid(), groupId, cycleId);

        result.Slots.Should().HaveCount(1);
        var dto = result.Slots[0];
        dto.ShiftSlotId.Should().Be(slot.Id);
        dto.Date.Should().Be(date);
        dto.StartTime.Should().Be(new TimeOnly(8, 0));
        dto.EndTime.Should().Be(new TimeOnly(16, 0));
        dto.TaskName.Should().Be("Morning Shift");
        dto.CurrentFillCount.Should().Be(2);
        dto.Capacity.Should().Be(5);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_LabelsSlotsOnSpaceSpecialDays()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);

        var specialDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        var normalDate = specialDate.AddDays(1);
        var specialSlot = AddSlot(db, spaceId, groupId, taskId, cycleId, specialDate,
            new TimeOnly(8, 0), new TimeOnly(16, 0));
        AddSlot(db, spaceId, groupId, taskId, cycleId, normalDate,
            new TimeOnly(8, 0), new TimeOnly(16, 0));
        db.SpaceSpecialDays.Add(SpaceSpecialDay.Create(
            spaceId,
            specialDate,
            "Independence Day",
            SpaceSpecialDayKind.Holiday,
            requiresCoverage: true));
        db.SpaceSpecialDays.Add(SpaceSpecialDay.Create(
            Guid.NewGuid(),
            normalDate,
            "Wrong Space Holiday",
            SpaceSpecialDayKind.Holiday,
            requiresCoverage: true));
        await db.SaveChangesAsync();

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(Guid.NewGuid(), groupId, cycleId);

        var labeledSlot = result.Slots.Single(s => s.ShiftSlotId == specialSlot.Id);
        labeledSlot.IsSpecialDay.Should().BeTrue();
        labeledSlot.SpecialDayName.Should().Be("Independence Day");
        labeledSlot.SpecialDayKind.Should().Be(nameof(SpaceSpecialDayKind.Holiday));
        result.Slots.Single(s => s.Date == normalDate).IsSpecialDay.Should().BeFalse();
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_SetsReadOnlyFlag_WhenWindowClosed()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db, windowOpen: false);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(8, 0), new TimeOnly(16, 0));

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(Guid.NewGuid(), groupId, cycleId);

        result.IsReadOnly.Should().BeTrue();
        result.Message.Should().NotBeNullOrEmpty();
        // Slots are still returned even in read-only mode
        result.Slots.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_SetsReadOnlyFalse_WhenWindowOpen()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db, windowOpen: true);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(8, 0), new TimeOnly(16, 0));

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(Guid.NewGuid(), groupId, cycleId);

        result.IsReadOnly.Should().BeFalse();
        result.Message.Should().BeNull();
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_CancelledRequestDoesNotExcludeSlot()
    {
        using var db = CreateDb();
        var (spaceId, groupId, cycleId, taskId) = SeedBaseData(db);
        var personId = Guid.NewGuid();

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        var slot = AddSlot(db, spaceId, groupId, taskId, cycleId, date,
            new TimeOnly(8, 0), new TimeOnly(16, 0));

        // Person had an approved request that was cancelled
        var req = ShiftRequest.Create(spaceId, slot.Id, personId, groupId, cycleId);
        req.Approve();
        req.Cancel("Changed my mind");
        db.ShiftRequests.Add(req);
        await db.SaveChangesAsync();

        var engine = new SlotAvailabilityEngine(db);
        var result = await engine.GetAvailableSlotsAsync(personId, groupId, cycleId);

        // Slot should be available since the request was cancelled
        result.Slots.Should().HaveCount(1);
        result.Slots[0].ShiftSlotId.Should().Be(slot.Id);
    }
}
