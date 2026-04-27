// Feature: group-alerts-and-phone
// Property tests for GroupAlert commands/queries and phone number DTO fidelity

using FluentAssertions;
using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Application.Groups.Commands;
using Jobuler.Application.Groups.Queries;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class GroupAlertPropertyTests
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
        return svc;
    }

    /// <summary>
    /// Seeds a Person linked to <paramref name="userId"/> in <paramref name="spaceId"/>,
    /// adds a GroupMembership for that person, and returns the person's Id.
    /// </summary>
    private static async Task<Guid> SeedPersonAndMembership(
        AppDbContext db, Guid spaceId, Guid groupId, Guid userId,
        string? phoneNumber = null, bool isOwner = false)
    {
        var person = Person.Create(spaceId, "Test User", null, userId, phoneNumber);
        db.People.Add(person);

        var membership = GroupMembership.Create(spaceId, groupId, person.Id, isOwner);
        db.GroupMemberships.Add(membership);

        await db.SaveChangesAsync();
        return person.Id;
    }

    // ── Property 1: Phone number in DTO matches people table ─────────────────
    // Validates: Requirements 1.1, 1.2, 1.3

    [Theory]
    [InlineData("050-1234567")]
    [InlineData("+972501234567")]
    [InlineData(null)]
    [InlineData("")]
    public async Task Property1_GroupMemberDto_PhoneNumber_MatchesPeopleTable(string? phoneNumber)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Normalise: Person.Create trims, so empty string becomes null-equivalent
        var normalised = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        await SeedPersonAndMembership(db, spaceId, groupId, userId, normalised);

        var handler = new GetGroupMembersQueryHandler(db);

        // Act
        var members = await handler.Handle(new GetGroupMembersQuery(spaceId, groupId), CancellationToken.None);

        // Assert
        members.Should().HaveCount(1);
        members[0].PhoneNumber.Should().Be(normalised);
    }

    [Theory]
    [InlineData("050-1234567")]
    [InlineData("+972501234567")]
    [InlineData(null)]
    public void Property1_GroupMemberDto_Record_PhoneNumber_MatchesConstructorArg(string? phoneNumber)
    {
        // The DTO record itself must faithfully carry the value
        var dto = new GroupMemberDto(Guid.NewGuid(), "Full Name", "Display", false, phoneNumber, "accepted", null);
        dto.PhoneNumber.Should().Be(phoneNumber);
    }

    // ── Property 3: Alert creation round-trip ─────────────────────────────────
    // Validates: Requirements 4.1, 5.1, 5.2

    [Theory]
    [InlineData("Emergency Alert", "All personnel report immediately", "critical")]
    [InlineData("Info Update", "Schedule has been updated", "info")]
    [InlineData("Warning", "Weather conditions may affect operations", "warning")]
    [InlineData("A", "B", "info")]
    [InlineData("  Trimmed Title  ", "  Trimmed Body  ", "warning")]
    public async Task Property3_CreateGroupAlert_RoundTrip(string title, string body, string severity)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedPersonAndMembership(db, spaceId, groupId, userId, isOwner: true);

        var createHandler = new CreateGroupAlertCommandHandler(db, AllowAllPermissions());
        var getHandler = new GetGroupAlertsQueryHandler(db);

        var cmd = new CreateGroupAlertCommand(spaceId, groupId, userId, title, body, severity);

        // Act
        var alertId = await createHandler.Handle(cmd, CancellationToken.None);
        var alerts = await getHandler.Handle(new GetGroupAlertsQuery(spaceId, groupId, userId), CancellationToken.None);

        // Assert
        alerts.Should().HaveCount(1);
        var alert = alerts[0];
        alert.Id.Should().Be(alertId);
        alert.Title.Should().Be(title.Trim());
        alert.Body.Should().Be(body.Trim());
        alert.Severity.Should().Be(severity.ToLowerInvariant());
    }

    // ── Property 3 (domain): GroupAlert.Create stores trimmed values ──────────

    [Theory]
    [InlineData("Emergency Alert", "All personnel report immediately", "critical")]
    [InlineData("Info Update", "Schedule has been updated", "info")]
    [InlineData("Warning", "Weather conditions may affect operations", "warning")]
    [InlineData("A", "B", "info")]
    public void Property3_GroupAlert_Create_DomainRoundTrip(string title, string body, string severity)
    {
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        Enum.TryParse<AlertSeverity>(severity, ignoreCase: true, out var sev);

        var alert = GroupAlert.Create(spaceId, groupId, title, body, sev, personId);

        alert.Title.Should().Be(title.Trim());
        alert.Body.Should().Be(body.Trim());
        alert.Severity.Should().Be(sev);
        alert.SpaceId.Should().Be(spaceId);
        alert.GroupId.Should().Be(groupId);
        alert.CreatedByPersonId.Should().Be(personId);
        alert.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── Property 4: Alert creation rejects invalid inputs ─────────────────────
    // Validates: Requirements 4.3, 4.4, 4.5

    [Theory]
    [InlineData("", "valid body", "info")]           // blank title
    [InlineData("   ", "valid body", "info")]         // whitespace-only title
    [InlineData("valid title", "", "info")]           // blank body
    [InlineData("valid title", "   ", "info")]        // whitespace-only body
    public void Property4_Validator_RejectsInvalidInputs(string title, string body, string severity)
    {
        var validator = new CreateGroupAlertCommandValidator();
        var cmd = new CreateGroupAlertCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), title, body, severity);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("valid title", "valid body", "invalid")]  // bad severity
    [InlineData("valid title", "valid body", "EXTREME")]  // bad severity
    [InlineData("valid title", "valid body", "")]         // empty severity
    public async Task Property4_Handler_RejectsInvalidSeverity(string title, string body, string severity)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedPersonAndMembership(db, spaceId, groupId, userId);

        var handler = new CreateGroupAlertCommandHandler(db, AllowAllPermissions());
        var cmd = new CreateGroupAlertCommand(spaceId, groupId, userId, title, body, severity);

        // Act & Assert
        var act = async () => await handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // No alert persisted
        db.GroupAlerts.Count().Should().Be(0);
    }

    [Fact]
    public void Property4_Validator_RejectsTitleOver200Chars()
    {
        var validator = new CreateGroupAlertCommandValidator();
        var longTitle = new string('x', 201);
        var cmd = new CreateGroupAlertCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), longTitle, "valid body", "info");

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Property4_Validator_RejectsBodyOver2000Chars()
    {
        var validator = new CreateGroupAlertCommandValidator();
        var longBody = new string('x', 2001);
        var cmd = new CreateGroupAlertCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "valid title", longBody, "info");

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    // ── Property 5: Alerts ordered newest-first ───────────────────────────────
    // Validates: Requirements 5.1

    [Fact]
    public async Task Property5_GetGroupAlertsQuery_ReturnsNewestFirst()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var personId = await SeedPersonAndMembership(db, spaceId, groupId, userId, isOwner: true);

        // Seed 10 alerts with distinct timestamps by manipulating CreatedAt via EF
        var baseTime = DateTime.UtcNow.AddMinutes(-10);
        for (int i = 0; i < 10; i++)
        {
            var alert = GroupAlert.Create(spaceId, groupId, $"Alert {i}", "body", AlertSeverity.Info, personId);
            // Use EF entry to override CreatedAt so timestamps are deterministically ordered
            var entry = db.GroupAlerts.Add(alert);
            await db.SaveChangesAsync();
            // Update CreatedAt to a known offset so ordering is deterministic
            entry.Property("CreatedAt").CurrentValue = baseTime.AddMinutes(i);
            await db.SaveChangesAsync();
        }

        var handler = new GetGroupAlertsQueryHandler(db);

        // Act
        var alerts = await handler.Handle(new GetGroupAlertsQuery(spaceId, groupId, userId), CancellationToken.None);

        // Assert — each item must be >= the next (newest first)
        alerts.Should().HaveCount(10);
        for (int i = 0; i < alerts.Count - 1; i++)
        {
            alerts[i].CreatedAt.Should().BeOnOrAfter(alerts[i + 1].CreatedAt,
                because: $"alert at index {i} should be newer than alert at index {i + 1}");
        }
    }

    [Fact]
    public void Property5_GroupAlerts_OrderedNewestFirst_DomainLevel()
    {
        // Verify ordering logic on in-memory list (no DB needed)
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var personId = Guid.NewGuid();

        var alerts = new List<GroupAlert>();
        for (int i = 0; i < 10; i++)
        {
            var alert = GroupAlert.Create(spaceId, groupId, $"Alert {i}", "body", AlertSeverity.Info, personId);
            alerts.Add(alert);
            System.Threading.Thread.Sleep(2); // ensure distinct timestamps
        }

        var ordered = alerts.OrderByDescending(a => a.CreatedAt).ToList();

        for (int i = 0; i < ordered.Count - 1; i++)
        {
            ordered[i].CreatedAt.Should().BeOnOrAfter(ordered[i + 1].CreatedAt);
        }
    }

    // ── Property 6: Tenant isolation ─────────────────────────────────────────
    // Validates: Requirements 5.4

    [Fact]
    public async Task Property6_GetGroupAlertsQuery_RespectsSpaceIsolation()
    {
        // Arrange
        var db = CreateDb();
        var spaceA = Guid.NewGuid();
        var spaceB = Guid.NewGuid();
        var groupId = Guid.NewGuid(); // same groupId in both spaces
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var personAId = await SeedPersonAndMembership(db, spaceA, groupId, userA, isOwner: true);
        var personBId = await SeedPersonAndMembership(db, spaceB, groupId, userB, isOwner: true);

        // Seed alerts in both spaces
        var alertA = GroupAlert.Create(spaceA, groupId, "Alert A", "body A", AlertSeverity.Info, personAId);
        var alertB = GroupAlert.Create(spaceB, groupId, "Alert B", "body B", AlertSeverity.Warning, personBId);
        db.GroupAlerts.AddRange(alertA, alertB);
        await db.SaveChangesAsync();

        var handler = new GetGroupAlertsQueryHandler(db);

        // Act
        var spaceAAlerts = await handler.Handle(new GetGroupAlertsQuery(spaceA, groupId, userA), CancellationToken.None);
        var spaceBAlerts = await handler.Handle(new GetGroupAlertsQuery(spaceB, groupId, userB), CancellationToken.None);

        // Assert — each space only sees its own alerts
        spaceAAlerts.Should().HaveCount(1);
        spaceAAlerts[0].Title.Should().Be("Alert A");

        spaceBAlerts.Should().HaveCount(1);
        spaceBAlerts[0].Title.Should().Be("Alert B");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Property6_TenantIsolation_MultipleAlertsPerSpace(int alertsPerSpace)
    {
        // Arrange
        var db = CreateDb();
        var spaceA = Guid.NewGuid();
        var spaceB = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var personAId = await SeedPersonAndMembership(db, spaceA, groupId, userA, isOwner: true);
        var personBId = await SeedPersonAndMembership(db, spaceB, groupId, userB, isOwner: true);

        for (int i = 0; i < alertsPerSpace; i++)
        {
            db.GroupAlerts.Add(GroupAlert.Create(spaceA, groupId, $"A-Alert-{i}", "body", AlertSeverity.Info, personAId));
            db.GroupAlerts.Add(GroupAlert.Create(spaceB, groupId, $"B-Alert-{i}", "body", AlertSeverity.Info, personBId));
        }
        await db.SaveChangesAsync();

        var handler = new GetGroupAlertsQueryHandler(db);

        // Act
        var spaceAAlerts = await handler.Handle(new GetGroupAlertsQuery(spaceA, groupId, userA), CancellationToken.None);
        var spaceBAlerts = await handler.Handle(new GetGroupAlertsQuery(spaceB, groupId, userB), CancellationToken.None);

        // Assert — no cross-space leakage
        spaceAAlerts.Should().HaveCount(alertsPerSpace);
        spaceAAlerts.Should().OnlyContain(a => a.Title.StartsWith("A-Alert-"));

        spaceBAlerts.Should().HaveCount(alertsPerSpace);
        spaceBAlerts.Should().OnlyContain(a => a.Title.StartsWith("B-Alert-"));
    }

    // ── Property 7: Non-members cannot read alerts ────────────────────────────
    // Validates: Requirements 5.3

    [Theory]
    [InlineData(0)]  // no members at all
    [InlineData(1)]  // one member, but querying as a different user
    [InlineData(5)]  // five members, but querying as a non-member
    public async Task Property7_GetGroupAlertsQuery_ThrowsForNonMembers(int memberCount)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var nonMemberUserId = Guid.NewGuid();

        // Seed some members (but NOT the querying user)
        for (int i = 0; i < memberCount; i++)
        {
            var memberId = Guid.NewGuid();
            await SeedPersonAndMembership(db, spaceId, groupId, memberId);
        }

        var handler = new GetGroupAlertsQueryHandler(db);

        // Act & Assert
        var act = async () => await handler.Handle(
            new GetGroupAlertsQuery(spaceId, groupId, nonMemberUserId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not a member*");
    }

    // ── Property 8: Alert delete removes own alerts ───────────────────────────
    // Validates: Requirements 6.1

    [Theory]
    [InlineData(1)]   // delete the only alert
    [InlineData(3)]   // delete one of three alerts
    [InlineData(10)]  // delete one of ten alerts
    public async Task Property8_DeleteGroupAlertCommand_RemovesOwnAlert(int totalAlerts)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var personId = await SeedPersonAndMembership(db, spaceId, groupId, userId, isOwner: true);

        // Seed multiple alerts, all by the same person
        var alertIds = new List<Guid>();
        for (int i = 0; i < totalAlerts; i++)
        {
            var a = GroupAlert.Create(spaceId, groupId, $"Alert {i}", "body", AlertSeverity.Info, personId);
            db.GroupAlerts.Add(a);
            alertIds.Add(a.Id);
        }
        await db.SaveChangesAsync();

        var deleteHandler = new DeleteGroupAlertCommandHandler(db, AllowAllPermissions());
        var getHandler = new GetGroupAlertsQueryHandler(db);

        // Act — delete the first alert
        var targetAlertId = alertIds[0];
        await deleteHandler.Handle(
            new DeleteGroupAlertCommand(spaceId, groupId, targetAlertId, userId),
            CancellationToken.None);

        // Assert — deleted alert is gone, others remain
        var remaining = await getHandler.Handle(
            new GetGroupAlertsQuery(spaceId, groupId, userId), CancellationToken.None);

        remaining.Should().HaveCount(totalAlerts - 1);
        remaining.Should().NotContain(a => a.Id == targetAlertId);
        (await db.GroupAlerts.FindAsync(targetAlertId)).Should().BeNull();
    }

    // ── Property 9: Any people.manage holder can delete any alert ────────────
    // Validates: Requirements 6.3 (ownership check removed — admin can delete any alert)
    // Feature: admin-management-and-scheduling, Property 9: any admin can delete any alert

    [Theory]
    [InlineData("info")]
    [InlineData("warning")]
    [InlineData("critical")]
    public async Task Property9_DeleteGroupAlertCommand_AllowsAnyAdminToDelete(string severity)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var creatorUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        // Both users are members; both hold people.manage via AllowAllPermissions
        var creatorPersonId = await SeedPersonAndMembership(db, spaceId, groupId, creatorUserId, isOwner: true);
        await SeedPersonAndMembership(db, spaceId, groupId, otherUserId, isOwner: false);

        Enum.TryParse<AlertSeverity>(severity, ignoreCase: true, out var sev);
        var alert = GroupAlert.Create(spaceId, groupId, "Alert", "body", sev, creatorPersonId);
        db.GroupAlerts.Add(alert);
        await db.SaveChangesAsync();

        var handler = new DeleteGroupAlertCommandHandler(db, AllowAllPermissions());

        // Act — a DIFFERENT user (not the creator) deletes the alert
        var act = async () => await handler.Handle(
            new DeleteGroupAlertCommand(spaceId, groupId, alert.Id, otherUserId),
            CancellationToken.None);

        // Assert — must NOT throw; any people.manage holder can delete any alert
        await act.Should().NotThrowAsync();

        // Alert must be gone
        db.GroupAlerts.Should().HaveCount(0);
        (await db.GroupAlerts.FindAsync(alert.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Property9_DeleteGroupAlertCommand_AllowsCreatorToDelete()
    {
        // Sanity check: creator CAN still delete their own alert
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var personId = await SeedPersonAndMembership(db, spaceId, groupId, userId, isOwner: true);

        var alert = GroupAlert.Create(spaceId, groupId, "Alert", "body", AlertSeverity.Info, personId);
        db.GroupAlerts.Add(alert);
        await db.SaveChangesAsync();

        var handler = new DeleteGroupAlertCommandHandler(db, AllowAllPermissions());

        var act = async () => await handler.Handle(
            new DeleteGroupAlertCommand(spaceId, groupId, alert.Id, userId),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
        db.GroupAlerts.Should().HaveCount(0);
    }
}
