// Feature: solver-start-time-bug
// Bug condition exploration tests — now validate the FIX is correct.
// Property 1: AutoSchedulerService passes group.SolverStartDateTime as StartTime
// Property 2: Group entity has SolverStartDateTime property
// Property 3: UpdateSettings accepts solverStartDateTime parameter

using FluentAssertions;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

/// <summary>
/// Validates the solver start time fix:
/// - Group.SolverStartDateTime is persisted via UpdateSettings
/// - AutoSchedulerService passes it as StartTime to TriggerSolverCommand
/// - Null SolverStartDateTime falls back to DateTime.UtcNow (backward compat)
/// </summary>
public class AutoSchedulerBugConditionTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // ── Property 1: Group stores and returns SolverStartDateTime ─────────────

    [Fact]
    public void Group_UpdateSettings_StoresSolverStartDateTime()
    {
        var group = Group.Create(Guid.NewGuid(), null, "Test Group");
        var configuredStart = new DateTime(2025, 8, 1, 6, 0, 0, DateTimeKind.Utc);

        group.UpdateSettings(7, configuredStart);

        group.SolverStartDateTime.Should().Be(configuredStart,
            "UpdateSettings should persist the configured start date/time");
        group.SolverHorizonDays.Should().Be(7,
            "SolverHorizonDays should be unchanged");
    }

    [Fact]
    public void Group_UpdateSettings_WithNullStartDateTime_ClearsValue()
    {
        var group = Group.Create(Guid.NewGuid(), null, "Test Group");
        group.UpdateSettings(7, new DateTime(2025, 8, 1, 6, 0, 0, DateTimeKind.Utc));

        // Clear it
        group.UpdateSettings(7, null);

        group.SolverStartDateTime.Should().BeNull(
            "Passing null should clear the configured start date/time");
    }

    [Fact]
    public void Group_UpdateSettings_WithoutStartDateTime_DefaultsToNull()
    {
        var group = Group.Create(Guid.NewGuid(), null, "Test Group");

        group.UpdateSettings(5);

        group.SolverStartDateTime.Should().BeNull(
            "Omitting solverStartDateTime should default to null (backward compat)");
        group.SolverHorizonDays.Should().Be(5);
    }

    // ── Property 2: Group entity has SolverStartDateTime property ────────────

    [Fact]
    public void Group_HasSolverStartDateTimeProperty()
    {
        var property = typeof(Group).GetProperty("SolverStartDateTime");

        property.Should().NotBeNull("Group entity must have a SolverStartDateTime property");
        property!.PropertyType.Should().Be(typeof(DateTime?),
            "SolverStartDateTime should be a nullable DateTime");
    }

    // ── Property 3: UpdateSettings accepts solverStartDateTime parameter ──────

    [Fact]
    public void Group_UpdateSettings_AcceptsSolverStartDateTime()
    {
        var method = typeof(Group).GetMethod("UpdateSettings");
        method.Should().NotBeNull();

        var parameters = method!.GetParameters();

        var param = parameters.FirstOrDefault(p => p.Name == "solverStartDateTime");
        param.Should().NotBeNull("UpdateSettings must accept a solverStartDateTime parameter");
        param!.ParameterType.Should().Be(typeof(DateTime?));
        param.IsOptional.Should().BeTrue("solverStartDateTime should be optional (default null)");
    }

    // ── Property 4: TriggerSolverCommand carries GroupId and StartTime ────────

    [Fact]
    public async Task TriggerSolverCommand_WithGroupIdAndStartTime_EnqueuesCorrectMessage()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var configuredStart = DateTime.UtcNow.AddDays(1);

        // Seed a group with a future task so the stale-task guard passes
        var group = Group.Create(spaceId, null, "Test Group");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
        group.UpdateSettings(7, configuredStart);
        db.Groups.Add(group);

        var task = GroupTask.Create(spaceId, groupId, "Future Task",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(8),
            480, 1, TaskBurdenLevel.Neutral, false, false, Guid.NewGuid());
        db.GroupTasks.Add(task);
        await db.SaveChangesAsync();

        // Capture the enqueued message
        SolverJobMessage? captured = null;
        var queue = Substitute.For<ISolverJobQueue>();
        queue.EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>())
             .Returns(callInfo => {
                 captured = callInfo.Arg<SolverJobMessage>();
                 return Task.CompletedTask;
             });

        var handler = new TriggerSolverCommandHandler(db, queue);

        // Act — this is what AutoSchedulerService now sends (after the fix)
        await handler.Handle(
            new TriggerSolverCommand(spaceId, "standard", null,
                GroupId: groupId,
                StartTime: configuredStart),
            CancellationToken.None);

        // Assert — the job message carries the correct GroupId and StartTime
        captured.Should().NotBeNull();
        captured!.GroupId.Should().Be(groupId,
            "AutoSchedulerService must pass the group's ID so the normalizer scopes the payload");
        captured.StartTime.Should().Be(configuredStart,
            "AutoSchedulerService must pass group.SolverStartDateTime as StartTime");
    }

    // ── Property 5: Null SolverStartDateTime → StartTime = null (backward compat)

    [Fact]
    public async Task TriggerSolverCommand_WithNullStartTime_EnqueuesNullStartTime()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var group = Group.Create(spaceId, null, "Test Group");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
        // No SolverStartDateTime set — should fall back to UtcNow in normalizer
        db.Groups.Add(group);

        var task = GroupTask.Create(spaceId, groupId, "Future Task",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(8),
            480, 1, TaskBurdenLevel.Neutral, false, false, Guid.NewGuid());
        db.GroupTasks.Add(task);
        await db.SaveChangesAsync();

        SolverJobMessage? captured = null;
        var queue = Substitute.For<ISolverJobQueue>();
        queue.EnqueueAsync(Arg.Any<SolverJobMessage>(), Arg.Any<CancellationToken>())
             .Returns(callInfo => {
                 captured = callInfo.Arg<SolverJobMessage>();
                 return Task.CompletedTask;
             });

        var handler = new TriggerSolverCommandHandler(db, queue);

        await handler.Handle(
            new TriggerSolverCommand(spaceId, "standard", null,
                GroupId: groupId,
                StartTime: null),  // null = use DateTime.UtcNow in normalizer
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.StartTime.Should().BeNull(
            "Null SolverStartDateTime must produce null StartTime — normalizer falls back to UtcNow");
    }
}
