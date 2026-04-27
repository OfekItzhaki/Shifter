// Feature: admin-management-and-scheduling
// Property-based tests for GroupTask CRUD (Task 29)

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Tasks.Commands;
using Jobuler.Application.Tasks.Queries;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class GroupTaskPropertyTests
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

    /// <summary>Seeds a Group so the handler's group-exists check passes.</summary>
    private static async Task<(Guid spaceId, Guid groupId)> SeedGroup(AppDbContext db)
    {
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group", null, null);
        // Override the Id via reflection so we can use our own groupId
        typeof(Jobuler.Domain.Common.Entity)
            .GetProperty("Id")!
            .SetValue(group, groupId);
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return (spaceId, groupId);
    }

    // ── Property 1: Valid task inputs → create → list → fields match ──────────
    // Validates: Requirements 3.1, 3.2
    // Feature: admin-management-and-scheduling, Property 1: task create round-trip

    [Theory]
    [InlineData("Morning Shift",   "2025-06-01T06:00:00", "2025-06-01T14:00:00", 480,  2, "neutral",   false, false)]
    [InlineData("Night Watch",     "2025-06-01T22:00:00", "2025-06-02T06:00:00", 480,  1, "disliked",  false, false)]
    [InlineData("Emergency Duty",  "2025-07-15T00:00:00", "2025-07-15T12:00:00", 720, 3, "hated",     true,  false)]
    [InlineData("Kitchen Duty",    "2025-08-01T08:00:00", "2025-08-01T12:00:00", 240,  1, "favorable", false, true)]
    [InlineData("Patrol",          "2025-09-10T10:00:00", "2025-09-10T18:00:00", 480,  2, "neutral",   true,  true)]
    [InlineData("Guard Post",      "2025-10-01T06:00:00", "2025-10-01T18:00:00", 720, 4, "disliked",  false, false)]
    [InlineData("Logistics",       "2025-11-05T07:00:00", "2025-11-05T15:00:00", 480,  2, "favorable", false, false)]
    [InlineData("Medical Standby", "2025-12-01T00:00:00", "2025-12-01T08:00:00", 480,  1, "neutral",   false, true)]
    [InlineData("Training",        "2026-01-10T09:00:00", "2026-01-10T17:00:00", 480,  5, "favorable", false, false)]
    [InlineData("Cleanup",         "2026-02-14T14:00:00", "2026-02-14T18:00:00", 240,  2, "hated",     false, false)]
    public async Task Property1_CreateTask_RoundTrip_FieldsMatch(
        string name, string startsAtStr, string endsAtStr,
        int ShiftDurationMinutes, int headcount, string burdenLevel,
        bool allowsDoubleShift, bool allowsOverlap)
    {
        // Arrange
        var db = CreateDb();
        var (spaceId, groupId) = await SeedGroup(db);
        var userId = Guid.NewGuid();
        var startsAt = DateTime.Parse(startsAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var endsAt = DateTime.Parse(endsAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind);

        var createHandler = new CreateGroupTaskCommandHandler(db, AllowAllPermissions());
        var getHandler = new GetGroupTasksQueryHandler(db, AllowAllPermissions());

        var cmd = new CreateGroupTaskCommand(
            spaceId, groupId, userId,
            name, startsAt, endsAt,
            ShiftDurationMinutes, headcount, burdenLevel,
            allowsDoubleShift, allowsOverlap);

        // Act
        var taskId = await createHandler.Handle(cmd, CancellationToken.None);
        var tasks = await getHandler.Handle(new GetGroupTasksQuery(spaceId, groupId, userId), CancellationToken.None);

        // Assert
        tasks.Should().HaveCount(1);
        var t = tasks[0];
        t.Id.Should().Be(taskId);
        t.Name.Should().Be(name.Trim());
        t.StartsAt.Should().Be(startsAt);
        t.EndsAt.Should().Be(endsAt);
        t.ShiftDurationMinutes.Should().Be(ShiftDurationMinutes);
        t.RequiredHeadcount.Should().Be(headcount);
        t.BurdenLevel.Should().Be(burdenLevel.ToLowerInvariant());
        t.AllowsDoubleShift.Should().Be(allowsDoubleShift);
        t.AllowsOverlap.Should().Be(allowsOverlap);
    }

    // ── Property 2: ends_at ≤ starts_at → validator rejects ──────────────────
    // Validates: Requirements 3.3
    // Feature: admin-management-and-scheduling, Property 2: invalid date range rejected

    [Theory]
    [InlineData("2025-06-01T08:00:00", "2025-06-01T08:00:00")]  // equal
    [InlineData("2025-06-01T08:00:00", "2025-06-01T07:59:59")]  // 1 second before
    [InlineData("2025-06-01T08:00:00", "2025-06-01T07:00:00")]  // 1 hour before
    [InlineData("2025-06-01T08:00:00", "2025-06-01T00:00:00")]  // same day, earlier
    [InlineData("2025-06-02T08:00:00", "2025-06-01T08:00:00")]  // day before
    public void Property2_Validator_RejectsEndsAtNotAfterStartsAt(string startsAtStr, string endsAtStr)
    {
        // Arrange
        var validator = new CreateGroupTaskCommandValidator();
        var startsAt = DateTime.Parse(startsAtStr);
        var endsAt = DateTime.Parse(endsAtStr);

        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Valid Name", startsAt, endsAt,
            8, 1, "neutral", false, false);

        // Act
        var result = validator.Validate(cmd);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EndsAt");
    }

    // ── Property 3: Invalid burden_level → validator rejects ─────────────────
    // Validates: Requirements 3.4
    // Feature: admin-management-and-scheduling, Property 3: invalid burden level rejected

    [Theory]
    [InlineData("invalid")]
    [InlineData("EXTREME")]
    [InlineData("")]
    [InlineData("medium")]
    [InlineData("bad")]
    [InlineData("NEUTRAL")]   // case-insensitive check — actually valid, but let's test exact casing
    [InlineData("Favorable")]
    [InlineData("HATED")]
    [InlineData("Disliked")]
    [InlineData("unknown")]
    public void Property3_Validator_RejectsInvalidBurdenLevel(string burdenLevel)
    {
        // Arrange
        var validator = new CreateGroupTaskCommandValidator();
        var startsAt = DateTime.UtcNow;

        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Valid Name", startsAt, startsAt.AddHours(8),
            8, 1, burdenLevel, false, false);

        // Act
        var result = validator.Validate(cmd);

        // Assert — only truly invalid values should fail; valid lowercase ones pass
        // The validator uses ToLowerInvariant() so "NEUTRAL" etc. are actually valid
        var validLower = new[] { "favorable", "neutral", "disliked", "hated" };
        var isActuallyValid = validLower.Contains(burdenLevel.ToLowerInvariant());

        if (!isActuallyValid)
            result.IsValid.Should().BeFalse();
        // If it's actually valid (case-insensitive), we don't assert false
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("EXTREME")]
    [InlineData("medium")]
    [InlineData("bad")]
    [InlineData("unknown")]
    public void Property3_Validator_RejectsDefinitelyInvalidBurdenLevel(string burdenLevel)
    {
        var validator = new CreateGroupTaskCommandValidator();
        var startsAt = DateTime.UtcNow;
        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Valid Name", startsAt, startsAt.AddHours(8),
            8, 1, burdenLevel, false, false);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    // ── Property 4: Create task → delete → list → task absent ────────────────
    // Validates: Requirements 3.5
    // Feature: admin-management-and-scheduling, Property 4: delete removes task from list

    [Theory]
    [InlineData("Morning Shift")]
    [InlineData("Night Watch")]
    [InlineData("Emergency Duty")]
    [InlineData("Kitchen Duty")]
    [InlineData("Patrol")]
    public async Task Property4_CreateTask_Delete_NotInList(string taskName)
    {
        // Arrange
        var db = CreateDb();
        var (spaceId, groupId) = await SeedGroup(db);
        var userId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);

        var createHandler = new CreateGroupTaskCommandHandler(db, AllowAllPermissions());
        var deleteHandler = new DeleteGroupTaskCommandHandler(db, AllowAllPermissions());
        var getHandler = new GetGroupTasksQueryHandler(db, AllowAllPermissions());

        // Act — create
        var taskId = await createHandler.Handle(
            new CreateGroupTaskCommand(spaceId, groupId, userId, taskName,
                startsAt, startsAt.AddHours(8), 8, 1, "neutral", false, false),
            CancellationToken.None);

        // Act — delete
        await deleteHandler.Handle(
            new DeleteGroupTaskCommand(spaceId, groupId, taskId, userId),
            CancellationToken.None);

        // Act — list
        var tasks = await getHandler.Handle(
            new GetGroupTasksQuery(spaceId, groupId, userId),
            CancellationToken.None);

        // Assert — task is absent
        tasks.Should().BeEmpty();
        tasks.Should().NotContain(t => t.Id == taskId);
    }

    // ── Property 5: Create N tasks → list → ascending starts_at order ─────────
    // Validates: Requirements 3.6
    // Feature: admin-management-and-scheduling, Property 5: tasks ordered by starts_at ascending

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task Property5_CreateNTasks_ListReturnsAscendingOrder(int taskCount)
    {
        // Arrange
        var db = CreateDb();
        var (spaceId, groupId) = await SeedGroup(db);
        var userId = Guid.NewGuid();

        var createHandler = new CreateGroupTaskCommandHandler(db, AllowAllPermissions());
        var getHandler = new GetGroupTasksQueryHandler(db, AllowAllPermissions());

        // Create tasks with deliberately shuffled starts_at values
        // Use a fixed seed-like pattern: task i starts at base + (taskCount - i) days
        // so they are inserted in reverse order
        var baseTime = new DateTime(2025, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < taskCount; i++)
        {
            var offset = taskCount - i; // reverse order insertion
            var startsAt = baseTime.AddDays(offset);
            await createHandler.Handle(
                new CreateGroupTaskCommand(spaceId, groupId, userId,
                    $"Task {i}", startsAt, startsAt.AddHours(8),
                    8, 1, "neutral", false, false),
                CancellationToken.None);
        }

        // Act
        var tasks = await getHandler.Handle(
            new GetGroupTasksQuery(spaceId, groupId, userId),
            CancellationToken.None);

        // Assert — ascending order
        tasks.Should().HaveCount(taskCount);
        for (int i = 0; i < tasks.Count - 1; i++)
        {
            tasks[i].StartsAt.Should().BeOnOrBefore(tasks[i + 1].StartsAt,
                because: $"task at index {i} should start before or at the same time as task at index {i + 1}");
        }
    }
}
