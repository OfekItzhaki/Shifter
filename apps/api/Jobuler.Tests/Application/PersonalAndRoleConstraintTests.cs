// Feature: personal-and-role-constraints
// Unit and property-based tests for CreateConstraintCommandHandler scope validation.
// Validates: Requirements 2.7, 3.7, 6.1–6.6, 8.2–8.4

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Constraints.Commands;
using Jobuler.Domain.Constraints;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class PersonalAndRoleConstraintTests
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

    private static CreateConstraintCommandHandler MakeHandler(AppDbContext db) =>
        new(db, AllowAllPermissions());

    private static CreateConstraintCommand MakeCmd(
        Guid spaceId, ConstraintScopeType scopeType, Guid? scopeId) =>
        new(spaceId, scopeType, scopeId,
            ConstraintSeverity.Hard, "min_rest_hours", "{\"hours\": 8}",
            null, null, Guid.NewGuid());

    // ── Task 8.1 — Person scope: null linked_user_id → DomainValidationException ──
    // Feature: personal-and-role-constraints, Property 7: unregistered person rejected with 422

    [Fact]
    public async Task Task8_1_PersonScope_NullLinkedUserId_ThrowsDomainValidationException()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        // Person with no linked user (unregistered)
        var person = Person.Create(spaceId, "Unregistered User", linkedUserId: null, invitationStatus: "accepted");
        db.People.Add(person);
        await db.SaveChangesAsync();

        var handler = MakeHandler(db);
        var cmd = MakeCmd(spaceId, ConstraintScopeType.Person, person.Id);

        // Act & Assert
        await Assert.ThrowsAsync<DomainValidationException>(
            () => handler.Handle(cmd, CancellationToken.None));
    }

    // ── Task 8.2 — Person scope: invitation_status = "pending" → DomainValidationException ──
    // Feature: personal-and-role-constraints, Property 7: unregistered person rejected with 422

    [Fact]
    public async Task Task8_2_PersonScope_PendingInvitation_ThrowsDomainValidationException()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        // Person with a linked user but still pending (edge case: should not happen in practice,
        // but the guard checks both conditions independently)
        var person = Person.Create(spaceId, "Pending User", linkedUserId: Guid.NewGuid(), invitationStatus: "pending");
        db.People.Add(person);
        await db.SaveChangesAsync();

        var handler = MakeHandler(db);
        var cmd = MakeCmd(spaceId, ConstraintScopeType.Person, person.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => handler.Handle(cmd, CancellationToken.None));
        ex.Message.Should().Contain("registered members");
    }

    // ── Task 8.3 — Person scope: non-existent person → KeyNotFoundException ──
    // Feature: personal-and-role-constraints, Property 9: non-existent person rejected with 404

    [Fact]
    public async Task Task8_3_PersonScope_NonExistentPerson_ThrowsKeyNotFoundException()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var nonExistentPersonId = Guid.NewGuid();

        var handler = MakeHandler(db);
        var cmd = MakeCmd(spaceId, ConstraintScopeType.Person, nonExistentPersonId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(cmd, CancellationToken.None));
        ex.Message.Should().Contain("Person not found");
    }

    // ── Task 8.4 — Role scope: inactive role → KeyNotFoundException ──────────
    // Feature: personal-and-role-constraints, Property 8: non-existent or inactive role rejected with 404

    [Fact]
    public async Task Task8_4_RoleScope_InactiveRole_ThrowsKeyNotFoundException()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var role = SpaceRole.Create(spaceId, "Inactive Role", Guid.NewGuid());
        role.Deactivate();
        db.SpaceRoles.Add(role);
        await db.SaveChangesAsync();

        var handler = MakeHandler(db);
        var cmd = MakeCmd(spaceId, ConstraintScopeType.Role, role.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(cmd, CancellationToken.None));
        ex.Message.Should().Contain("Role not found");
    }

    // ── Task 8.5 — Role scope: non-existent role → KeyNotFoundException ──────
    // Feature: personal-and-role-constraints, Property 8: non-existent or inactive role rejected with 404

    [Fact]
    public async Task Task8_5_RoleScope_NonExistentRole_ThrowsKeyNotFoundException()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var nonExistentRoleId = Guid.NewGuid();

        var handler = MakeHandler(db);
        var cmd = MakeCmd(spaceId, ConstraintScopeType.Role, nonExistentRoleId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(cmd, CancellationToken.None));
        ex.Message.Should().Contain("Role not found");
    }

    // ── Task 8.6 — Group scope: succeeds without person/role checks ───────────
    // Validates: Requirements 6.6

    [Fact]
    public async Task Task8_6_GroupScope_NoPersonOrRoleChecks_Succeeds()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var handler = MakeHandler(db);
        var cmd = MakeCmd(spaceId, ConstraintScopeType.Group, groupId);

        // Act — should not throw even though no person/role exists
        var id = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        id.Should().NotBeEmpty();
        db.ConstraintRules.Should().HaveCount(1);
    }

    // ── Property 7 (parameterised): any unregistered person → DomainValidationException ──
    // Feature: personal-and-role-constraints, Property 7: unregistered person rejected with 422
    // Validates: Requirements 8.2, 8.4

    [Theory]
    [InlineData(null, "accepted")]          // no linked user, accepted status
    [InlineData(null, "pending")]           // no linked user, pending status
    [InlineData("has-user", "pending")]     // has linked user but still pending
    public async Task Property7_UnregisteredPerson_AlwaysRejected(
        string? linkedUserIdMarker, string invitationStatus)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var linkedUserId = linkedUserIdMarker != null ? (Guid?)Guid.NewGuid() : null;
        var person = Person.Create(spaceId, "Test Person", linkedUserId: linkedUserId, invitationStatus: invitationStatus);
        db.People.Add(person);
        await db.SaveChangesAsync();

        var handler = MakeHandler(db);
        var cmd = MakeCmd(spaceId, ConstraintScopeType.Person, person.Id);

        // Act & Assert — DomainValidationException must be thrown for every unregistered person
        await Assert.ThrowsAsync<DomainValidationException>(
            () => handler.Handle(cmd, CancellationToken.None));
    }

    // ── Property 8 (parameterised): non-existent or inactive role → KeyNotFoundException ──
    // Feature: personal-and-role-constraints, Property 8: non-existent or inactive role rejected with 404
    // Validates: Requirements 3.7, 6.3, 6.4

    [Theory]
    [InlineData(false, false)]  // role does not exist in DB at all
    [InlineData(true, false)]   // role exists but is inactive
    [InlineData(true, true)]    // role exists in a DIFFERENT space (wrong space_id)
    public async Task Property8_NonExistentOrInactiveRole_AlwaysRejected(
        bool roleExists, bool activeButWrongSpace)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        Guid roleId;

        if (!roleExists)
        {
            roleId = Guid.NewGuid(); // random GUID not in DB
        }
        else if (activeButWrongSpace)
        {
            // Active role but in a different space
            var otherSpaceId = Guid.NewGuid();
            var role = SpaceRole.Create(otherSpaceId, "Other Space Role", Guid.NewGuid());
            db.SpaceRoles.Add(role);
            await db.SaveChangesAsync();
            roleId = role.Id;
        }
        else
        {
            // Role exists in correct space but is inactive
            var role = SpaceRole.Create(spaceId, "Inactive Role", Guid.NewGuid());
            role.Deactivate();
            db.SpaceRoles.Add(role);
            await db.SaveChangesAsync();
            roleId = role.Id;
        }

        var handler = MakeHandler(db);
        var cmd = MakeCmd(spaceId, ConstraintScopeType.Role, roleId);

        // Act & Assert — KeyNotFoundException must be thrown every time
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(cmd, CancellationToken.None));
    }

    // ── Property 9 (parameterised): non-existent person → KeyNotFoundException ──
    // Feature: personal-and-role-constraints, Property 9: non-existent person rejected with 404
    // Validates: Requirements 2.7, 6.1, 6.2

    [Theory]
    [InlineData(false)]  // person does not exist in DB at all
    [InlineData(true)]   // person exists but in a different space
    public async Task Property9_NonExistentPerson_AlwaysRejected(bool personInWrongSpace)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        Guid personId;

        if (personInWrongSpace)
        {
            var otherSpaceId = Guid.NewGuid();
            var person = Person.Create(otherSpaceId, "Wrong Space Person", linkedUserId: Guid.NewGuid(), invitationStatus: "accepted");
            db.People.Add(person);
            await db.SaveChangesAsync();
            personId = person.Id;
        }
        else
        {
            personId = Guid.NewGuid(); // random GUID not in DB
        }

        var handler = MakeHandler(db);
        var cmd = MakeCmd(spaceId, ConstraintScopeType.Person, personId);

        // Act & Assert — KeyNotFoundException must be thrown every time
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(cmd, CancellationToken.None));
    }

    // ── Registered person with active role → both succeed ────────────────────
    // Validates: Requirements 2.5, 3.5

    [Fact]
    public async Task RegisteredPerson_ActiveRole_BothSucceed()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();

        var person = Person.Create(spaceId, "Registered User", linkedUserId: Guid.NewGuid(), invitationStatus: "accepted");
        var role = SpaceRole.Create(spaceId, "Active Role", Guid.NewGuid());
        db.People.Add(person);
        db.SpaceRoles.Add(role);
        await db.SaveChangesAsync();

        var handler = MakeHandler(db);

        // Act — person constraint
        var personConstraintId = await handler.Handle(
            MakeCmd(spaceId, ConstraintScopeType.Person, person.Id),
            CancellationToken.None);

        // Act — role constraint
        var roleConstraintId = await handler.Handle(
            MakeCmd(spaceId, ConstraintScopeType.Role, role.Id),
            CancellationToken.None);

        // Assert
        personConstraintId.Should().NotBeEmpty();
        roleConstraintId.Should().NotBeEmpty();
        db.ConstraintRules.Should().HaveCount(2);
    }
}
