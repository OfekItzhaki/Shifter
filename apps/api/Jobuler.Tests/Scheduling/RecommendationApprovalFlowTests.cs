// Feature: recommendation-approval-flow
// Unit tests for backend changes (Task 1.4)

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Groups.Queries;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

/// <summary>
/// Unit tests for the recommendation approval flow backend changes.
/// Validates: Requirements 1.3, 4.1, 4.2, 7.1
/// </summary>
public class RecommendationApprovalFlowTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IPermissionService AllowAllPermissions()
    {
        var svc = Substitute.For<IPermissionService>();
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        svc.HasPermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return svc;
    }

    // ── Test 1: Dismiss handler sets status to Dismissed without modifying GroupTask.AllowsDoubleShift ──
    // Validates: Requirements 1.3, 4.2

    [Fact]
    public async Task DismissHandler_SetsStatusToDismissed_WithoutModifyingGroupTaskAllowsDoubleShift()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Create a GroupTask with AllowsDoubleShift = false
        var groupTask = GroupTask.Create(
            spaceId, groupId, "Kitchen",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            240, 1, TaskBurdenLevel.Normal,
            allowsDoubleShift: false, allowsOverlap: false,
            createdByUserId: userId);
        db.GroupTasks.Add(groupTask);

        // Create an active recommendation for that task
        var recommendation = DoubleShiftRecommendation.Create(
            spaceId, groupId, Guid.NewGuid(), groupTask.Id,
            "Kitchen", additionalSlotsCovered: 3,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            totalUncoveredSlotsInRun: 5);
        db.DoubleShiftRecommendations.Add(recommendation);
        await db.SaveChangesAsync();

        var handler = new DismissRecommendationCommandHandler(db, AllowAllPermissions());

        // Act
        await handler.Handle(
            new DismissRecommendationCommand(spaceId, recommendation.Id, userId),
            CancellationToken.None);

        // Assert — recommendation is dismissed
        var dismissed = await db.DoubleShiftRecommendations.FindAsync(recommendation.Id);
        dismissed!.Status.Should().Be(RecommendationStatus.Dismissed);
        dismissed.DismissedByUserId.Should().Be(userId);
        dismissed.DismissedAt.Should().NotBeNull();

        // Assert — GroupTask.AllowsDoubleShift is unchanged (still false)
        var task = await db.GroupTasks.FindAsync(groupTask.Id);
        task!.AllowsDoubleShift.Should().BeFalse(
            because: "dismissing a recommendation must not modify the GroupTask");
    }

    [Fact]
    public async Task DismissHandler_WhenTaskHasDoubleShiftEnabled_DoesNotChangeIt()
    {
        // Arrange — task already has AllowsDoubleShift = true
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var groupTask = GroupTask.Create(
            spaceId, groupId, "Guard Duty",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            240, 1, TaskBurdenLevel.Hard,
            allowsDoubleShift: true, allowsOverlap: false,
            createdByUserId: userId);
        db.GroupTasks.Add(groupTask);

        var recommendation = DoubleShiftRecommendation.Create(
            spaceId, groupId, Guid.NewGuid(), groupTask.Id,
            "Guard Duty", additionalSlotsCovered: 2,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            totalUncoveredSlotsInRun: 4);
        db.DoubleShiftRecommendations.Add(recommendation);
        await db.SaveChangesAsync();

        var handler = new DismissRecommendationCommandHandler(db, AllowAllPermissions());

        // Act
        await handler.Handle(
            new DismissRecommendationCommand(spaceId, recommendation.Id, userId),
            CancellationToken.None);

        // Assert — recommendation is dismissed
        var dismissed = await db.DoubleShiftRecommendations.FindAsync(recommendation.Id);
        dismissed!.Status.Should().Be(RecommendationStatus.Dismissed);

        // Assert — GroupTask.AllowsDoubleShift remains true (unchanged)
        var task = await db.GroupTasks.FindAsync(groupTask.Id);
        task!.AllowsDoubleShift.Should().BeTrue(
            because: "dismissing a recommendation must not modify the GroupTask");
    }

    // ── Test 2: Schedule query response includes TaskConfigurations for each task ──
    // Validates: Requirement 7.1

    [Fact]
    public async Task GetGroupScheduleQuery_IncludesTaskConfigurations_ForEachActiveTask()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Create a person and group membership
        var person = Person.Create(spaceId, "Test Person", null, userId);
        db.People.Add(person);

        var membership = GroupMembership.Create(spaceId, groupId, person.Id);
        db.GroupMemberships.Add(membership);

        // Create two active GroupTasks
        var task1 = GroupTask.Create(
            spaceId, groupId, "Kitchen",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            240, 2, TaskBurdenLevel.Normal,
            allowsDoubleShift: true, allowsOverlap: false,
            createdByUserId: userId,
            dailyStartTime: new TimeOnly(8, 0),
            dailyEndTime: new TimeOnly(20, 0),
            splitCount: 2);
        db.GroupTasks.Add(task1);

        var task2 = GroupTask.Create(
            spaceId, groupId, "Guard",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            480, 1, TaskBurdenLevel.Hard,
            allowsDoubleShift: false, allowsOverlap: true,
            createdByUserId: userId);
        db.GroupTasks.Add(task2);

        // Create a published schedule version
        var version = ScheduleVersion.CreateDraft(spaceId, 1, null, null, userId);
        version.Publish(userId);
        db.ScheduleVersions.Add(version);

        // Create assignments using derived shift GUIDs for the tasks
        var shiftGuid1 = DeriveShiftGuid(task1.Id, 0);
        var shiftGuid2 = DeriveShiftGuid(task2.Id, 0);

        var assignment1 = Assignment.Create(spaceId, version.Id, shiftGuid1, person.Id);
        var assignment2 = Assignment.Create(spaceId, version.Id, shiftGuid2, person.Id);
        db.Assignments.AddRange(assignment1, assignment2);

        await db.SaveChangesAsync();

        var handler = new GetGroupScheduleQueryHandler(db, new NoOpCacheService());

        // Act
        var result = await handler.Handle(
            new GetGroupScheduleQuery(spaceId, groupId),
            CancellationToken.None);

        // Assert — TaskConfigurations contains both tasks
        result.TaskConfigurations.Should().ContainKey("Kitchen");
        result.TaskConfigurations.Should().ContainKey("Guard");

        // Assert — Kitchen task config is correct
        var kitchenConfig = result.TaskConfigurations["Kitchen"];
        kitchenConfig.AllowsDoubleShift.Should().BeTrue();
        kitchenConfig.AllowsOverlap.Should().BeFalse();
        kitchenConfig.DailyStartTime.Should().Be("08:00");
        kitchenConfig.DailyEndTime.Should().Be("20:00");
        kitchenConfig.BurdenLevel.Should().Be("Normal");
        kitchenConfig.SplitCount.Should().Be(2);

        // Assert — Guard task config is correct
        var guardConfig = result.TaskConfigurations["Guard"];
        guardConfig.AllowsDoubleShift.Should().BeFalse();
        guardConfig.AllowsOverlap.Should().BeTrue();
        guardConfig.DailyStartTime.Should().BeNull();
        guardConfig.DailyEndTime.Should().BeNull();
        guardConfig.BurdenLevel.Should().Be("Hard");
        guardConfig.SplitCount.Should().Be(1);
    }

    [Fact]
    public async Task GetGroupScheduleQuery_EmptySchedule_ReturnsEmptyTaskConfigurations()
    {
        // Arrange — no published version
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var handler = new GetGroupScheduleQueryHandler(db, new NoOpCacheService());

        // Act
        var result = await handler.Handle(
            new GetGroupScheduleQuery(spaceId, groupId),
            CancellationToken.None);

        // Assert
        result.Assignments.Should().BeEmpty();
        result.TaskConfigurations.Should().BeEmpty();
    }

    // ── Test 3: Accept endpoint returns 404 after removal ──
    // Validates: Requirement 4.1

    [Fact]
    public void AcceptEndpoint_IsRemoved_NoAcceptMethodOnController()
    {
        // The accept endpoint (POST /spaces/{spaceId}/recommendations/{id}/accept)
        // has been removed from RecommendationsController.
        // Verify via reflection that no "Accept" action method exists.

        var controllerType = typeof(Jobuler.Api.Controllers.RecommendationsController);
        var acceptMethod = controllerType.GetMethod("Accept");

        acceptMethod.Should().BeNull(
            because: "the accept endpoint was removed as part of the recommendation approval flow simplification");
    }

    [Fact]
    public void AcceptEndpoint_NoAcceptRecommendationCommandExists()
    {
        // Verify that AcceptRecommendationCommand no longer exists in the Application assembly.
        var applicationAssembly = typeof(DismissRecommendationCommand).Assembly;
        var acceptCommandType = applicationAssembly.GetType(
            "Jobuler.Application.Scheduling.Commands.AcceptRecommendationCommand");

        acceptCommandType.Should().BeNull(
            because: "AcceptRecommendationCommand was deleted as part of the backend simplification");
    }

    // ── Helper: Derive shift GUID (same logic as GetGroupScheduleQueryHandler) ──

    private static Guid DeriveShiftGuid(Guid taskId, int shiftIndex)
    {
        var bytes = taskId.ToByteArray();
        var indexBytes = BitConverter.GetBytes(shiftIndex);
        for (int i = 0; i < 4; i++)
            bytes[12 + i] ^= indexBytes[i];
        return new Guid(bytes);
    }
}
