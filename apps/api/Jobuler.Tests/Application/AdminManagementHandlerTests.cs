// Feature: admin-management-and-scheduling
// Unit tests for Application layer handlers (Task 28)

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Constraints.Commands;
using Jobuler.Application.Groups.Commands;
using Jobuler.Application.Tasks.Commands;
using Jobuler.Domain.Constraints;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class AdminManagementHandlerTests
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

    private static async Task<Guid> SeedPersonAndMembership(
        AppDbContext db, Guid spaceId, Guid groupId, Guid userId)
    {
        var person = Person.Create(spaceId, "Test User", null, userId, null);
        db.People.Add(person);
        var membership = GroupMembership.Create(spaceId, groupId, person.Id, false);
        db.GroupMemberships.Add(membership);
        await db.SaveChangesAsync();
        return person.Id;
    }

    // ── Task 28.1: DeleteGroupAlertCommandHandler — any people.manage holder ──
    // Feature: admin-management-and-scheduling, Property 10: any admin can delete any alert

    [Fact]
    public async Task DeleteGroupAlertHandler_AnyPeopleManageHolder_CanDeleteAnyAlert()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userA = Guid.NewGuid(); // creator
        var userB = Guid.NewGuid(); // different user, also has people.manage

        var personAId = await SeedPersonAndMembership(db, spaceId, groupId, userA);
        await SeedPersonAndMembership(db, spaceId, groupId, userB);

        // Create alert as user A
        var alert = GroupAlert.Create(spaceId, groupId, "Alert Title", "Alert Body", AlertSeverity.Info, personAId);
        db.GroupAlerts.Add(alert);
        await db.SaveChangesAsync();

        var handler = new DeleteGroupAlertCommandHandler(db, AllowAllPermissions());

        // Act — user B (different person) deletes user A's alert
        var act = async () => await handler.Handle(
            new DeleteGroupAlertCommand(spaceId, groupId, alert.Id, userB),
            CancellationToken.None);

        // Assert — no exception; alert is gone
        await act.Should().NotThrowAsync();
        db.GroupAlerts.Should().HaveCount(0);
        (await db.GroupAlerts.FindAsync(alert.Id)).Should().BeNull();
    }

    // ── Task 28.2: DeleteGroupMessageCommandHandler — people.manage bypass ────
    // Feature: admin-management-and-scheduling, Property 11: admin can delete any message

    [Fact]
    public async Task DeleteGroupMessageHandler_PeopleManageHolder_CanDeleteAnyMessage()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var authorUserId = Guid.NewGuid(); // message author
        var adminUserId = Guid.NewGuid();  // different user with people.manage

        // Create message as author
        var message = GroupMessage.Create(spaceId, groupId, authorUserId, "Hello world", false);
        db.GroupMessages.Add(message);
        await db.SaveChangesAsync();

        var handler = new DeleteGroupMessageCommandHandler(db, AllowAllPermissions());

        // Act — admin (not the author) deletes the message
        var act = async () => await handler.Handle(
            new DeleteGroupMessageCommand(spaceId, groupId, message.Id, adminUserId),
            CancellationToken.None);

        // Assert — no exception; message is gone
        await act.Should().NotThrowAsync();
        db.GroupMessages.Should().HaveCount(0);
        (await db.GroupMessages.FindAsync(message.Id)).Should().BeNull();
    }

    // ── Task 28.3: PinGroupMessageCommandHandler — sets IsPinned correctly ────
    // Feature: admin-management-and-scheduling, Property 12: pin/unpin round-trip

    [Fact]
    public async Task PinGroupMessageHandler_SetsIsPinnedTrue_ThenFalse()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Create message with isPinned = false
        var message = GroupMessage.Create(spaceId, groupId, userId, "Test message", isPinned: false);
        db.GroupMessages.Add(message);
        await db.SaveChangesAsync();

        var handler = new PinGroupMessageCommandHandler(db, AllowAllPermissions());

        // Act — pin it
        await handler.Handle(
            new PinGroupMessageCommand(spaceId, groupId, message.Id, userId, IsPinned: true),
            CancellationToken.None);

        // Assert — isPinned = true
        var pinned = await db.GroupMessages.FindAsync(message.Id);
        pinned!.IsPinned.Should().BeTrue();

        // Act — unpin it
        await handler.Handle(
            new PinGroupMessageCommand(spaceId, groupId, message.Id, userId, IsPinned: false),
            CancellationToken.None);

        // Assert — isPinned = false
        var unpinned = await db.GroupMessages.FindAsync(message.Id);
        unpinned!.IsPinned.Should().BeFalse();
    }

    // ── Task 28.4: UpdateConstraintCommandValidator ───────────────────────────
    // Feature: admin-management-and-scheduling, Property 7/8: constraint validator

    [Theory]
    [InlineData("not json")]
    [InlineData("123abc")]
    [InlineData("{bad json")]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateConstraintValidator_RejectsInvalidJson(string badJson)
    {
        var validator = new UpdateConstraintCommandValidator();
        var cmd = new UpdateConstraintCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            badJson,
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 12, 31));

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateConstraintValidator_RejectsEffectiveUntilBeforeEffectiveFrom()
    {
        var validator = new UpdateConstraintCommandValidator();
        var cmd = new UpdateConstraintCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "{}",
            new DateOnly(2025, 6, 1),   // effectiveFrom
            new DateOnly(2025, 1, 1));  // effectiveUntil BEFORE effectiveFrom

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateConstraintValidator_AcceptsValidJsonAndValidDates()
    {
        var validator = new UpdateConstraintCommandValidator();
        var cmd = new UpdateConstraintCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "{\"hours\": 8}",
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 12, 31));

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateConstraintValidator_AcceptsNullDates()
    {
        var validator = new UpdateConstraintCommandValidator();
        var cmd = new UpdateConstraintCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "{}",
            null, null);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    // ── Task 28.5: CreateGroupTaskCommandValidator ────────────────────────────
    // Feature: admin-management-and-scheduling, Property 2/3: task validator

    [Theory]
    [InlineData("")]           // empty name
    [InlineData("   ")]        // whitespace-only name
    public void CreateGroupTaskValidator_RejectsEmptyOrWhitespaceName(string name)
    {
        var validator = new CreateGroupTaskCommandValidator();
        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            name,
            DateTime.UtcNow, DateTime.UtcNow.AddHours(8),
            8, 1, "neutral", false, false);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateGroupTaskValidator_RejectsNameOver200Chars()
    {
        var validator = new CreateGroupTaskCommandValidator();
        var longName = new string('x', 201);
        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            longName,
            DateTime.UtcNow, DateTime.UtcNow.AddHours(8),
            8, 1, "neutral", false, false);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]   // ends_at == starts_at
    [InlineData(-1)]  // ends_at < starts_at
    [InlineData(-60)] // ends_at well before starts_at
    public void CreateGroupTaskValidator_RejectsEndsAtNotAfterStartsAt(int offsetMinutes)
    {
        var validator = new CreateGroupTaskCommandValidator();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var endsAt = startsAt.AddMinutes(offsetMinutes);

        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Valid Name",
            startsAt, endsAt,
            8, 1, "neutral", false, false);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]    // duration_hours == 0
    [InlineData(-1)]   // duration_hours < 0
    public void CreateGroupTaskValidator_RejectsShiftDurationMinutesNotPositive(int ShiftDurationMinutes)
    {
        var validator = new CreateGroupTaskCommandValidator();
        var startsAt = DateTime.UtcNow;
        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Valid Name",
            startsAt, startsAt.AddHours(8),
            ShiftDurationMinutes, 1, "neutral", false, false);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]   // required_headcount == 0
    [InlineData(-1)]  // required_headcount < 0
    public void CreateGroupTaskValidator_RejectsRequiredHeadcountLessThanOne(int headcount)
    {
        var validator = new CreateGroupTaskCommandValidator();
        var startsAt = DateTime.UtcNow;
        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Valid Name",
            startsAt, startsAt.AddHours(8),
            8, headcount, "neutral", false, false);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("EXTREME")]
    [InlineData("medium")]
    [InlineData("bad")]
    [InlineData("")]
    public void CreateGroupTaskValidator_RejectsInvalidBurdenLevel(string burdenLevel)
    {
        var validator = new CreateGroupTaskCommandValidator();
        var startsAt = DateTime.UtcNow;
        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Valid Name",
            startsAt, startsAt.AddHours(8),
            8, 1, burdenLevel, false, false);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("favorable")]
    [InlineData("neutral")]
    [InlineData("disliked")]
    [InlineData("hated")]
    public void CreateGroupTaskValidator_AcceptsValidBurdenLevels(string burdenLevel)
    {
        var validator = new CreateGroupTaskCommandValidator();
        var startsAt = DateTime.UtcNow;
        var cmd = new CreateGroupTaskCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Valid Name",
            startsAt, startsAt.AddHours(8),
            8, 1, burdenLevel, false, false);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }
}
