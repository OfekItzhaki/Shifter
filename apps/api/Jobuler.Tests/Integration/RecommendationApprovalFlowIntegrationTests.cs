// Feature: recommendation-approval-flow
// Integration tests for the recommendation approval flow refactoring (Task 8.4)

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Groups.Queries;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Application.Tasks.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Integration;

/// <summary>
/// Integration tests for the recommendation approval flow feature.
/// Validates: Requirements 4.1, 4.3, 4.4, 7.1, 7.2
/// </summary>
public class RecommendationApprovalFlowIntegrationTests
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

    private static ICacheService NoOpCache() => new Helpers.NoOpCacheService();

    /// <summary>
    /// Seeds a group with members, tasks, a published schedule version, and assignments.
    /// Returns all IDs needed for assertions.
    /// </summary>
    private static async Task<TestSeedData> SeedFullScheduleScenario(AppDbContext db)
    {
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var personId = Guid.NewGuid();

        // Create group
        var group = Group.Create(spaceId, null, "Test Group", null, null);
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
        db.Groups.Add(group);

        // Create person
        var person = Person.Create(spaceId, "Test Person");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(person, personId);
        db.People.Add(person);

        // Create group membership
        var membership = GroupMembership.Create(spaceId, groupId, personId);
        db.GroupMemberships.Add(membership);

        // Create group tasks
        var now = DateTime.UtcNow;
        var task1 = GroupTask.Create(
            spaceId, groupId, "Kitchen",
            now, now.AddDays(7),
            240, 2, TaskBurdenLevel.Normal, false, false, userId);
        var task2 = GroupTask.Create(
            spaceId, groupId, "Guard",
            now, now.AddDays(7),
            480, 1, TaskBurdenLevel.Hard, true, false, userId,
            new TimeOnly(8, 0), new TimeOnly(20, 0));

        db.GroupTasks.AddRange(task1, task2);

        // Create published schedule version
        var version = ScheduleVersion.CreateDraft(spaceId, 1, null, null, userId);
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(version, Guid.NewGuid());
        db.ScheduleVersions.Add(version);
        await db.SaveChangesAsync();

        // Publish the version
        version.Publish(userId);
        await db.SaveChangesAsync();

        // Create an assignment using a derived shift GUID from task1
        var shiftGuid = DeriveShiftGuid(task1.Id, 0);
        var assignment = Assignment.Create(spaceId, version.Id, shiftGuid, personId);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        return new TestSeedData(spaceId, groupId, userId, personId, task1.Id, task2.Id, version.Id);
    }

    private static Guid DeriveShiftGuid(Guid taskId, int shiftIndex)
    {
        var bytes = taskId.ToByteArray();
        var indexBytes = BitConverter.GetBytes(shiftIndex);
        for (int i = 0; i < 4; i++)
            bytes[12 + i] ^= indexBytes[i];
        return new Guid(bytes);
    }

    private record TestSeedData(
        Guid SpaceId, Guid GroupId, Guid UserId, Guid PersonId,
        Guid Task1Id, Guid Task2Id, Guid VersionId);

    // ── Test 1: Accept endpoint returns 404 after removal ────────────────────
    // Validates: Requirement 4.1

    [Fact]
    public void Integration_8_4_AcceptEndpoint_RemovedFromController()
    {
        // The accept endpoint (POST /spaces/{spaceId}/recommendations/{id}/accept)
        // has been removed from RecommendationsController.
        // Verify that no method with route "accept" exists on the controller.

        var controllerType = typeof(Jobuler.Api.Controllers.RecommendationsController);
        var methods = controllerType.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var acceptMethods = methods
            .Where(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), false)
                .Cast<Microsoft.AspNetCore.Mvc.HttpPostAttribute>()
                .Any(attr => attr.Template != null && attr.Template.Contains("accept")))
            .ToList();

        acceptMethods.Should().BeEmpty(
            because: "the accept endpoint was removed as part of the recommendation approval flow refactoring (Req 4.1)");
    }

    [Fact]
    public void Integration_8_4_AcceptRecommendationCommand_DoesNotExist()
    {
        // Verify that AcceptRecommendationCommand class no longer exists in the assembly.
        var applicationAssembly = typeof(DismissRecommendationCommand).Assembly;

        var acceptCommandType = applicationAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "AcceptRecommendationCommand");

        acceptCommandType.Should().BeNull(
            because: "AcceptRecommendationCommand was deleted as part of the refactoring (Req 4.1)");
    }

    // ── Test 2: Schedule endpoint includes taskConfigurations in response ────
    // Validates: Requirements 7.1, 7.2

    [Fact]
    public async Task Integration_8_4_ScheduleEndpoint_IncludesTaskConfigurations()
    {
        // Arrange
        var db = CreateDb();
        var seed = await SeedFullScheduleScenario(db);

        var handler = new GetGroupScheduleQueryHandler(db, NoOpCache());

        // Act
        var response = await handler.Handle(
            new GetGroupScheduleQuery(seed.SpaceId, seed.GroupId),
            CancellationToken.None);

        // Assert — response includes TaskConfigurations
        response.Should().NotBeNull();
        response.TaskConfigurations.Should().NotBeNull();
        response.TaskConfigurations.Should().NotBeEmpty(
            because: "the schedule response must include task configurations for all active tasks in the group (Req 7.1)");
    }

    [Fact]
    public async Task Integration_8_4_ScheduleEndpoint_TaskConfigurations_ContainAllActiveGroupTasks()
    {
        // Arrange
        var db = CreateDb();
        var seed = await SeedFullScheduleScenario(db);

        var handler = new GetGroupScheduleQueryHandler(db, NoOpCache());

        // Act
        var response = await handler.Handle(
            new GetGroupScheduleQuery(seed.SpaceId, seed.GroupId),
            CancellationToken.None);

        // Assert — both tasks are present in taskConfigurations
        response.TaskConfigurations.Should().ContainKey("Kitchen");
        response.TaskConfigurations.Should().ContainKey("Guard");

        // Verify Kitchen task config
        var kitchenConfig = response.TaskConfigurations["Kitchen"];
        kitchenConfig.AllowsDoubleShift.Should().BeFalse();
        kitchenConfig.AllowsOverlap.Should().BeFalse();
        kitchenConfig.BurdenLevel.Should().Be("Normal");
        kitchenConfig.DailyStartTime.Should().BeNull();
        kitchenConfig.DailyEndTime.Should().BeNull();
        kitchenConfig.SplitCount.Should().Be(1);

        // Verify Guard task config
        var guardConfig = response.TaskConfigurations["Guard"];
        guardConfig.AllowsDoubleShift.Should().BeTrue();
        guardConfig.AllowsOverlap.Should().BeFalse();
        guardConfig.BurdenLevel.Should().Be("Hard");
        guardConfig.DailyStartTime.Should().Be("08:00");
        guardConfig.DailyEndTime.Should().Be("20:00");
        guardConfig.SplitCount.Should().Be(1);
    }

    [Fact]
    public async Task Integration_8_4_ScheduleEndpoint_TaskConfigurations_IncludeRequiredFields()
    {
        // Arrange
        var db = CreateDb();
        var seed = await SeedFullScheduleScenario(db);

        var handler = new GetGroupScheduleQueryHandler(db, NoOpCache());

        // Act
        var response = await handler.Handle(
            new GetGroupScheduleQuery(seed.SpaceId, seed.GroupId),
            CancellationToken.None);

        // Assert — each TaskConfigSummaryDto has all required fields (Req 7.2)
        foreach (var (taskName, config) in response.TaskConfigurations)
        {
            config.TaskId.Should().NotBeNullOrEmpty(
                because: $"TaskId must be present for task '{taskName}'");
            config.BurdenLevel.Should().NotBeNullOrEmpty(
                because: $"BurdenLevel must be present for task '{taskName}'");
            config.RequiredQualificationNames.Should().NotBeNull(
                because: $"RequiredQualificationNames must not be null for task '{taskName}'");
        }
    }

    // ── Test 3: Recommendation engine still generates recommendations ────────
    // Validates: Requirement 4.3

    [Fact]
    public async Task Integration_8_4_RecommendationEngine_StillGeneratesRecommendations()
    {
        // Arrange — set up a scenario with uncovered slots and eligible tasks
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        // Create two tasks that don't allow double shift (candidates for recommendation)
        var task1 = GroupTask.Create(
            spaceId, groupId, "Task A",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            240, 1, TaskBurdenLevel.Normal, false, false, Guid.NewGuid());
        var task2 = GroupTask.Create(
            spaceId, groupId, "Task B",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            240, 1, TaskBurdenLevel.Normal, false, false, Guid.NewGuid());

        db.GroupTasks.AddRange(task1, task2);
        await db.SaveChangesAsync();

        var logger = Substitute.For<ILogger<RecommendationEngine>>();
        var engine = new RecommendationEngine(db, logger);

        // Build solver input with task slots that have consecutive uncovered slots
        var slot1Id = task1.Id.ToString() + "-slot-0";
        var slot2Id = task1.Id.ToString() + "-slot-1";
        var horizonStart = DateTime.UtcNow.Date;
        var horizonEnd = horizonStart.AddDays(7);

        var input = new SolverInputDto(
            SpaceId: spaceId.ToString(),
            RunId: runId.ToString(),
            TriggerMode: "standard",
            HorizonStart: horizonStart.ToString("O"),
            HorizonEnd: horizonEnd.ToString("O"),
            Locale: "en",
            StabilityWeights: new StabilityWeightsDto(1.0, 0.5, 0.25),
            People: Enumerable.Range(0, 5).Select(i =>
                new PersonEligibilityDto(Guid.NewGuid().ToString(), new(), new(), new())).ToList(),
            AvailabilityWindows: new(),
            PresenceWindows: new(),
            TaskSlots: new List<TaskSlotDto>
            {
                new(slot1Id, task1.Id.ToString(), "Task A", "normal",
                    horizonStart.ToString("O"), horizonStart.AddHours(4).ToString("O"),
                    1, 1, new(), new(), false, false),
                new(slot2Id, task1.Id.ToString(), "Task A", "normal",
                    horizonStart.AddHours(4).ToString("O"), horizonStart.AddHours(8).ToString("O"),
                    1, 1, new(), new(), false, false),
            },
            HardConstraints: new(),
            SoftConstraints: new(),
            EmergencyConstraints: new(),
            BaselineAssignments: new(),
            FairnessCounters: new());

        // Output with uncovered slots and home leave causing shortfall
        var output = new SolverOutputDto
        {
            RunId = runId.ToString(),
            Feasible = true,
            UncoveredSlotIds = new List<string> { slot1Id, slot2Id },
            HomeLeaveAssignments = Enumerable.Range(0, 4).Select(i =>
                new HomeLeaveAssignmentDto
                {
                    PersonId = Guid.NewGuid().ToString(),
                    StartsAt = horizonStart.ToString("O"),
                    EndsAt = horizonStart.AddDays(3).ToString("O")
                }).ToList()
        };

        // Act
        var result = await engine.AnalyzeAsync(spaceId, groupId, runId, input, output, CancellationToken.None);

        // Assert — engine still generates recommendations after the refactoring (Req 4.3)
        result.HasShortfall.Should().BeTrue(
            because: "the scenario has more people on leave than available at base");
        result.Recommendations.Should().NotBeEmpty(
            because: "the recommendation engine should still generate recommendations for tasks with uncovered consecutive slots (Req 4.3)");
        result.Recommendations.Should().Contain(r => r.TaskName == "Task A",
            because: "Task A has consecutive uncovered slots that could benefit from double shift");
    }

    // ── Test 4: Task update endpoint remains the only way to enable double shift ─
    // Validates: Requirement 4.4

    [Fact]
    public async Task Integration_8_4_TaskUpdateEndpoint_IsOnlyWayToEnableDoubleShift()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var task = GroupTask.Create(
            spaceId, groupId, "Test Task",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            240, 1, TaskBurdenLevel.Normal, false, false, userId);
        db.GroupTasks.Add(task);
        await db.SaveChangesAsync();

        task.AllowsDoubleShift.Should().BeFalse("task starts with double shift disabled");

        // Act — use the UpdateGroupTaskCommand (the only way to enable double shift)
        var handler = new UpdateGroupTaskCommandHandler(db, AllowAllPermissions());
        await handler.Handle(new UpdateGroupTaskCommand(
            spaceId, groupId, task.Id, userId,
            "Test Task",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            240, 1, "normal",
            AllowsDoubleShift: true,
            AllowsOverlap: false), CancellationToken.None);

        // Assert — double shift is now enabled via the task update endpoint
        var updatedTask = await db.GroupTasks.FindAsync(task.Id);
        updatedTask!.AllowsDoubleShift.Should().BeTrue(
            because: "the task update endpoint is the only way to enable double shift (Req 4.4)");
    }

    [Fact]
    public async Task Integration_8_4_DismissRecommendation_DoesNotEnableDoubleShift()
    {
        // Arrange — create a task and a recommendation for it
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        var task = GroupTask.Create(
            spaceId, groupId, "Test Task",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            240, 1, TaskBurdenLevel.Normal, false, false, userId);
        db.GroupTasks.Add(task);

        var recommendation = DoubleShiftRecommendation.Create(
            spaceId, groupId, runId, task.Id, "Test Task",
            3, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(5), 10);
        db.DoubleShiftRecommendations.Add(recommendation);
        await db.SaveChangesAsync();

        // Act — dismiss the recommendation
        var handler = new DismissRecommendationCommandHandler(db, AllowAllPermissions());
        await handler.Handle(
            new DismissRecommendationCommand(spaceId, recommendation.Id, userId),
            CancellationToken.None);

        // Assert — task's AllowsDoubleShift remains unchanged (Req 4.4)
        var unchangedTask = await db.GroupTasks.FindAsync(task.Id);
        unchangedTask!.AllowsDoubleShift.Should().BeFalse(
            because: "dismissing a recommendation must NOT modify the task's AllowsDoubleShift property — " +
                     "the task update endpoint is the only way to enable double shift (Req 4.4)");

        // Assert — recommendation is dismissed
        var dismissedRec = await db.DoubleShiftRecommendations.FindAsync(recommendation.Id);
        dismissedRec!.Status.Should().Be(RecommendationStatus.Dismissed);
    }

    [Fact]
    public void Integration_8_4_GroupTask_NoEnableDoubleShiftMethod()
    {
        // Verify that the EnableDoubleShift method has been removed from GroupTask (Req 4.4).
        // The only way to change AllowsDoubleShift is through the full Update() method.
        var groupTaskType = typeof(GroupTask);

        var enableMethod = groupTaskType.GetMethod("EnableDoubleShift",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        enableMethod.Should().BeNull(
            because: "EnableDoubleShift was removed — the Update method is the only way to change AllowsDoubleShift (Req 4.4)");
    }
}
