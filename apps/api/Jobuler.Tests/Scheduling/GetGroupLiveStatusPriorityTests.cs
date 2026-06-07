// Feature: home-leave-protection
// Tests for live status priority hierarchy in GetGroupLiveStatusQuery.
// Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Queries;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jobuler.Tests.Scheduling;

/// <summary>
/// Verifies the live status priority hierarchy:
///   1. Active presence window (AtHome) → "at_home"
///   2. Active assignment (no presence window) → "on_mission"
///   3. Neither → "free_in_base"
///   4. AtHome window + active assignment → "at_home" (AtHome takes precedence)
/// </summary>
public class GetGroupLiveStatusPriorityTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<TestContext> SetupAsync()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        // Create space
        var space = Jobuler.Domain.Spaces.Space.Create("Test Space", Guid.NewGuid());
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(space, spaceId);
        db.Spaces.Add(space);

        // Create group
        var group = Group.Create(spaceId, null, "Test Group");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
        db.Groups.Add(group);

        await db.SaveChangesAsync();
        return new TestContext(db, spaceId, groupId);
    }

    private static Person CreatePerson(AppDbContext db, Guid spaceId, Guid groupId, string name)
    {
        var person = Person.Create(spaceId, name);
        db.People.Add(person);
        var membership = GroupMembership.Create(spaceId, groupId, person.Id);
        db.GroupMemberships.Add(membership);
        return person;
    }

    private static ScheduleVersion CreatePublishedVersion(AppDbContext db, Guid spaceId)
    {
        var version = ScheduleVersion.CreateDraft(spaceId, 1, null, null, Guid.NewGuid());
        version.Publish(Guid.NewGuid());
        db.ScheduleVersions.Add(version);
        return version;
    }

    private static TaskSlot CreateActiveSlot(AppDbContext db, Guid spaceId, Guid taskTypeId)
    {
        var now = DateTime.UtcNow.AddHours(3); // Match the Israel time offset used in the handler
        var slot = TaskSlot.Create(
            spaceId, taskTypeId,
            now.AddHours(-1), now.AddHours(4),
            requiredHeadcount: 1, priority: 5, createdByUserId: Guid.NewGuid());
        db.TaskSlots.Add(slot);
        return slot;
    }

    // ── Requirement 6.4: No AtHome, no assignment → "free_in_base" ────────────

    [Fact]
    public async Task Person_WithNoPresenceWindow_AndNoAssignment_IsFreeInBase()
    {
        // Arrange
        var ctx = await SetupAsync();
        var person = CreatePerson(ctx.Db, ctx.SpaceId, ctx.GroupId, "Alice");
        await ctx.Db.SaveChangesAsync();

        var handler = new GetGroupLiveStatusQueryHandler(ctx.Db, new NoOpCacheService());
        var query = new GetGroupLiveStatusQuery(ctx.SpaceId, ctx.GroupId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be("free_in_base",
            "a person with no presence window and no assignment should be free_in_base");
    }

    // ── Requirement 6.3: Active assignment, no AtHome → "on_mission" ──────────

    [Fact]
    public async Task Person_WithActiveAssignment_AndNoPresenceWindow_IsOnMission()
    {
        // Arrange
        var ctx = await SetupAsync();
        var person = CreatePerson(ctx.Db, ctx.SpaceId, ctx.GroupId, "Bob");

        var taskType = TaskType.Create(ctx.SpaceId, "Guard Duty", TaskBurdenLevel.Normal, Guid.NewGuid());
        ctx.Db.TaskTypes.Add(taskType);

        var version = CreatePublishedVersion(ctx.Db, ctx.SpaceId);
        var slot = CreateActiveSlot(ctx.Db, ctx.SpaceId, taskType.Id);

        var assignment = Assignment.Create(ctx.SpaceId, version.Id, slot.Id, person.Id);
        ctx.Db.Assignments.Add(assignment);

        await ctx.Db.SaveChangesAsync();

        var handler = new GetGroupLiveStatusQueryHandler(ctx.Db, new NoOpCacheService());
        var query = new GetGroupLiveStatusQuery(ctx.SpaceId, ctx.GroupId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be("on_mission",
            "a person with an active assignment and no presence window should be on_mission");
    }

    // ── Requirement 6.2: Active AtHome window, no assignment → "at_home" ─────

    [Fact]
    public async Task Person_WithAtHomeWindow_AndNoAssignment_IsAtHome()
    {
        // Arrange
        var ctx = await SetupAsync();
        var person = CreatePerson(ctx.Db, ctx.SpaceId, ctx.GroupId, "Charlie");

        var now = DateTime.UtcNow.AddHours(3); // Match handler's Israel time offset
        var atHomeWindow = PresenceWindow.CreateManual(
            ctx.SpaceId, person.Id, PresenceState.AtHome,
            now.AddHours(-2), now.AddHours(24));
        ctx.Db.PresenceWindows.Add(atHomeWindow);

        await ctx.Db.SaveChangesAsync();

        var handler = new GetGroupLiveStatusQueryHandler(ctx.Db, new NoOpCacheService());
        var query = new GetGroupLiveStatusQuery(ctx.SpaceId, ctx.GroupId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be("at_home",
            "a person with an active AtHome window should be at_home regardless of assignments");
    }

    // ── Requirement 6.1: AtHome window + active assignment → "at_home" (precedence) ──

    [Fact]
    public async Task Person_WithAtHomeWindow_AndActiveAssignment_IsAtHome_PrecedenceOverOnMission()
    {
        // Arrange
        var ctx = await SetupAsync();
        var person = CreatePerson(ctx.Db, ctx.SpaceId, ctx.GroupId, "Dana");

        var now = DateTime.UtcNow.AddHours(3); // Match handler's Israel time offset

        // Create an active AtHome window
        var atHomeWindow = PresenceWindow.CreateManual(
            ctx.SpaceId, person.Id, PresenceState.AtHome,
            now.AddHours(-2), now.AddHours(24));
        ctx.Db.PresenceWindows.Add(atHomeWindow);

        // Also create an active assignment
        var taskType = TaskType.Create(ctx.SpaceId, "Patrol", TaskBurdenLevel.Normal, Guid.NewGuid());
        ctx.Db.TaskTypes.Add(taskType);

        var version = CreatePublishedVersion(ctx.Db, ctx.SpaceId);
        var slot = CreateActiveSlot(ctx.Db, ctx.SpaceId, taskType.Id);

        var assignment = Assignment.Create(ctx.SpaceId, version.Id, slot.Id, person.Id);
        ctx.Db.Assignments.Add(assignment);

        await ctx.Db.SaveChangesAsync();

        var handler = new GetGroupLiveStatusQueryHandler(ctx.Db, new NoOpCacheService());
        var query = new GetGroupLiveStatusQuery(ctx.SpaceId, ctx.GroupId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be("at_home",
            "AtHome window MUST take precedence over on_mission derived from assignments (Req 6.1, 6.5)");
    }

    // ── Requirement 6.5: Presence windows evaluated before assignments ────────

    [Fact]
    public async Task MultipleMembers_EachGetsCorrectPriorityStatus()
    {
        // Arrange
        var ctx = await SetupAsync();
        var now = DateTime.UtcNow.AddHours(3);

        // Person A: AtHome window only → "at_home"
        var personA = CreatePerson(ctx.Db, ctx.SpaceId, ctx.GroupId, "A-AtHome");
        var atHomeWindow = PresenceWindow.CreateManual(
            ctx.SpaceId, personA.Id, PresenceState.AtHome,
            now.AddHours(-1), now.AddHours(10));
        ctx.Db.PresenceWindows.Add(atHomeWindow);

        // Person B: Assignment only → "on_mission"
        var personB = CreatePerson(ctx.Db, ctx.SpaceId, ctx.GroupId, "B-OnMission");
        var taskType = TaskType.Create(ctx.SpaceId, "Guard", TaskBurdenLevel.Normal, Guid.NewGuid());
        ctx.Db.TaskTypes.Add(taskType);
        var version = CreatePublishedVersion(ctx.Db, ctx.SpaceId);
        var slot = CreateActiveSlot(ctx.Db, ctx.SpaceId, taskType.Id);
        var assignmentB = Assignment.Create(ctx.SpaceId, version.Id, slot.Id, personB.Id);
        ctx.Db.Assignments.Add(assignmentB);

        // Person C: Neither → "free_in_base"
        var personC = CreatePerson(ctx.Db, ctx.SpaceId, ctx.GroupId, "C-Free");

        // Person D: AtHome + Assignment → "at_home" (precedence)
        var personD = CreatePerson(ctx.Db, ctx.SpaceId, ctx.GroupId, "D-AtHomePrecedence");
        var atHomeWindowD = PresenceWindow.CreateManual(
            ctx.SpaceId, personD.Id, PresenceState.AtHome,
            now.AddHours(-1), now.AddHours(10));
        ctx.Db.PresenceWindows.Add(atHomeWindowD);
        var slot2 = CreateActiveSlot(ctx.Db, ctx.SpaceId, taskType.Id);
        var assignmentD = Assignment.Create(ctx.SpaceId, version.Id, slot2.Id, personD.Id);
        ctx.Db.Assignments.Add(assignmentD);

        await ctx.Db.SaveChangesAsync();

        var handler = new GetGroupLiveStatusQueryHandler(ctx.Db, new NoOpCacheService());
        var query = new GetGroupLiveStatusQuery(ctx.SpaceId, ctx.GroupId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(4);

        var statusA = result.First(r => r.DisplayName == "A-AtHome");
        statusA.Status.Should().Be("at_home", "Person with AtHome window → at_home");

        var statusB = result.First(r => r.DisplayName == "B-OnMission");
        statusB.Status.Should().Be("on_mission", "Person with assignment only → on_mission");

        var statusC = result.First(r => r.DisplayName == "C-Free");
        statusC.Status.Should().Be("free_in_base", "Person with neither → free_in_base");

        var statusD = result.First(r => r.DisplayName == "D-AtHomePrecedence");
        statusD.Status.Should().Be("at_home", "Person with AtHome + assignment → at_home (precedence)");
    }

    // ── Derived AtHome window also takes precedence ───────────────────────────

    [Fact]
    public async Task Person_WithDerivedAtHomeWindow_AndActiveAssignment_IsAtHome()
    {
        // Arrange
        var ctx = await SetupAsync();
        var now = DateTime.UtcNow.AddHours(3);

        var person = CreatePerson(ctx.Db, ctx.SpaceId, ctx.GroupId, "Eve");

        // Create a derived AtHome window (from solver home-leave assignment)
        var derivedAtHome = PresenceWindow.CreateDerivedAtHome(
            ctx.SpaceId, person.Id,
            now.AddHours(-2), now.AddHours(24));
        ctx.Db.PresenceWindows.Add(derivedAtHome);

        // Also create an active assignment
        var taskType = TaskType.Create(ctx.SpaceId, "Patrol", TaskBurdenLevel.Normal, Guid.NewGuid());
        ctx.Db.TaskTypes.Add(taskType);
        var version = CreatePublishedVersion(ctx.Db, ctx.SpaceId);
        var slot = CreateActiveSlot(ctx.Db, ctx.SpaceId, taskType.Id);
        var assignment = Assignment.Create(ctx.SpaceId, version.Id, slot.Id, person.Id);
        ctx.Db.Assignments.Add(assignment);

        await ctx.Db.SaveChangesAsync();

        var handler = new GetGroupLiveStatusQueryHandler(ctx.Db, new NoOpCacheService());
        var query = new GetGroupLiveStatusQuery(ctx.SpaceId, ctx.GroupId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be("at_home",
            "derived AtHome window should also take precedence over assignment-based on_mission");
    }

    [Fact]
    public async Task Person_WithPreviousAndNextAssignments_ReturnsRestWindowAnchors()
    {
        // Arrange
        var ctx = await SetupAsync();
        var now = DateTime.UtcNow.AddHours(3);
        var person = CreatePerson(ctx.Db, ctx.SpaceId, ctx.GroupId, "Frank");

        var taskType = TaskType.Create(ctx.SpaceId, "Guard", TaskBurdenLevel.Normal, Guid.NewGuid());
        ctx.Db.TaskTypes.Add(taskType);
        var version = CreatePublishedVersion(ctx.Db, ctx.SpaceId);

        var olderSlot = TaskSlot.Create(
            ctx.SpaceId, taskType.Id,
            now.AddHours(-14), now.AddHours(-10),
            requiredHeadcount: 1, priority: 5, createdByUserId: Guid.NewGuid());
        var previousSlot = TaskSlot.Create(
            ctx.SpaceId, taskType.Id,
            now.AddHours(-6), now.AddHours(-2),
            requiredHeadcount: 1, priority: 5, createdByUserId: Guid.NewGuid());
        var nextSlot = TaskSlot.Create(
            ctx.SpaceId, taskType.Id,
            now.AddHours(5), now.AddHours(9),
            requiredHeadcount: 1, priority: 5, createdByUserId: Guid.NewGuid());

        ctx.Db.TaskSlots.AddRange(olderSlot, previousSlot, nextSlot);
        ctx.Db.Assignments.AddRange(
            Assignment.Create(ctx.SpaceId, version.Id, olderSlot.Id, person.Id),
            Assignment.Create(ctx.SpaceId, version.Id, previousSlot.Id, person.Id),
            Assignment.Create(ctx.SpaceId, version.Id, nextSlot.Id, person.Id));

        await ctx.Db.SaveChangesAsync();

        var handler = new GetGroupLiveStatusQueryHandler(ctx.Db, new NoOpCacheService());
        var query = new GetGroupLiveStatusQuery(ctx.SpaceId, ctx.GroupId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be("free_in_base");
        result[0].PreviousTaskName.Should().Be("Guard");
        result[0].PreviousEndsAt.Should().Be(previousSlot.EndsAt);
        result[0].NextTaskName.Should().Be("Guard");
        result[0].NextStartsAt.Should().Be(nextSlot.StartsAt);
    }

    private record TestContext(AppDbContext Db, Guid SpaceId, Guid GroupId);
}
