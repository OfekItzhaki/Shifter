// Feature: invitation-flow-fixes
// Bug Condition Exploration Tests — Property 1: Missing SpaceMembership on Group Join
// Validates: Requirements 1.1, 1.4, 2.1, 2.4
//
// These tests are EXPECTED TO FAIL on unfixed code.
// Failure confirms the bug exists: handlers create Person + GroupMembership
// but do NOT create SpaceMembership or SpacePermissionGrant.

using FluentAssertions;
using Jobuler.Application.Billing;
using Jobuler.Application.Groups.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Identity;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Tests;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.InvitationFlow;

public class BugConditionExplorationTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Seeds a space, a group with a join code, and a user (not yet a member).
    /// Also seeds a "requesting user" who is the group owner (needed for email/phone commands).
    /// </summary>
    private static async Task<TestFixture> SetupAsync()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();

        // Create the space owner / requesting user
        var ownerUserId = Guid.NewGuid();
        var ownerUser = User.Create("owner@test.com", "Space Owner", "hash");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(ownerUser, ownerUserId);
        db.Users.Add(ownerUser);

        var ownerPerson = Person.Create(spaceId, "Space Owner", linkedUserId: ownerUserId);
        db.People.Add(ownerPerson);

        // Create a group in the space
        var group = Group.Create(spaceId, null, "Test Group", createdByUserId: ownerUserId);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        // Make owner a group member (owner)
        var ownerMembership = GroupMembership.Create(spaceId, group.Id, ownerPerson.Id, isOwner: true);
        db.GroupMemberships.Add(ownerMembership);
        await db.SaveChangesAsync();

        // Create the joining user (no SpaceMembership, no Person in this space)
        var joiningUserId = Guid.NewGuid();
        var joiningUser = User.Create("joiner@test.com", "Joining User", "hash", phoneNumber: "+972501234567");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(joiningUser, joiningUserId);
        db.Users.Add(joiningUser);
        await db.SaveChangesAsync();

        return new TestFixture(db, spaceId, group.Id, group.JoinCode!, ownerUserId, ownerPerson.Id, joiningUserId);
    }

    private record TestFixture(
        AppDbContext Db,
        Guid SpaceId,
        Guid GroupId,
        string JoinCode,
        Guid OwnerUserId,
        Guid OwnerPersonId,
        Guid JoiningUserId);

    // ── Bug 1.1: JoinGroupByCode does NOT create SpaceMembership ─────────────
    // **Validates: Requirements 2.1**

    [Fact]
    public async Task JoinGroupByCode_ShouldCreateSpaceMembership_ButDoesNot()
    {
        // Arrange
        var fixture = await SetupAsync();
        var handler = new JoinGroupByCodeCommandHandler(fixture.Db);
        var command = new JoinGroupByCodeCommand(fixture.JoinCode, fixture.JoiningUserId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — This SHOULD pass but will FAIL on unfixed code (proving the bug)
        var hasSpaceMembership = await fixture.Db.SpaceMemberships
            .AnyAsync(sm => sm.UserId == fixture.JoiningUserId && sm.SpaceId == fixture.SpaceId);

        hasSpaceMembership.Should().BeTrue(
            "after joining a group by code, the user should have a SpaceMembership in the group's space");
    }

    [Fact]
    public async Task JoinGroupByCode_ShouldGrantSpaceViewPermission_ButDoesNot()
    {
        // Arrange
        var fixture = await SetupAsync();
        var handler = new JoinGroupByCodeCommandHandler(fixture.Db);
        var command = new JoinGroupByCodeCommand(fixture.JoinCode, fixture.JoiningUserId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — This SHOULD pass but will FAIL on unfixed code (proving the bug)
        var hasPermission = await fixture.Db.SpacePermissionGrants
            .AnyAsync(g => g.UserId == fixture.JoiningUserId
                && g.SpaceId == fixture.SpaceId
                && g.PermissionKey == Permissions.SpaceView);

        hasPermission.Should().BeTrue(
            "after joining a group by code, the user should have the space.view permission");
    }

    // ── Bug 1.4: AddPersonByEmail does NOT create SpaceMembership ────────────
    // **Validates: Requirements 2.4**

    [Fact]
    public async Task AddPersonByEmail_WithLinkedUser_ShouldCreateSpaceMembership_ButDoesNot()
    {
        // Arrange
        var fixture = await SetupAsync();
        var handler = new AddPersonByEmailCommandHandler(fixture.Db, Substitute.For<IPeakMemberTracker>(), TestContactLookupProtector.Create());
        var command = new AddPersonByEmailCommand(
            fixture.SpaceId,
            fixture.GroupId,
            "joiner@test.com", // matches the joining user's email
            fixture.OwnerUserId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — This SHOULD pass but will FAIL on unfixed code (proving the bug)
        var hasSpaceMembership = await fixture.Db.SpaceMemberships
            .AnyAsync(sm => sm.UserId == fixture.JoiningUserId && sm.SpaceId == fixture.SpaceId);

        hasSpaceMembership.Should().BeTrue(
            "after adding a person by email (with a linked user account), the user should have a SpaceMembership");
    }

    [Fact]
    public async Task AddPersonByEmail_WithLinkedUser_ShouldGrantSpaceViewPermission_ButDoesNot()
    {
        // Arrange
        var fixture = await SetupAsync();
        var handler = new AddPersonByEmailCommandHandler(fixture.Db, Substitute.For<IPeakMemberTracker>(), TestContactLookupProtector.Create());
        var command = new AddPersonByEmailCommand(
            fixture.SpaceId,
            fixture.GroupId,
            "joiner@test.com",
            fixture.OwnerUserId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — This SHOULD pass but will FAIL on unfixed code (proving the bug)
        var hasPermission = await fixture.Db.SpacePermissionGrants
            .AnyAsync(g => g.UserId == fixture.JoiningUserId
                && g.SpaceId == fixture.SpaceId
                && g.PermissionKey == Permissions.SpaceView);

        hasPermission.Should().BeTrue(
            "after adding a person by email (with a linked user), the user should have the space.view permission");
    }

    // ── Bug 1.4: AddPersonByPhone does NOT create SpaceMembership ────────────
    // **Validates: Requirements 2.4**

    [Fact]
    public async Task AddPersonByPhone_WithLinkedUser_ShouldCreateSpaceMembership_ButDoesNot()
    {
        // Arrange
        var fixture = await SetupAsync();
        var handler = new AddPersonByPhoneCommandHandler(fixture.Db, Substitute.For<IPeakMemberTracker>(), TestContactLookupProtector.Create());
        var command = new AddPersonByPhoneCommand(
            fixture.SpaceId,
            fixture.GroupId,
            "+972501234567", // matches the joining user's phone number
            fixture.OwnerUserId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — This SHOULD pass but will FAIL on unfixed code (proving the bug)
        var hasSpaceMembership = await fixture.Db.SpaceMemberships
            .AnyAsync(sm => sm.UserId == fixture.JoiningUserId && sm.SpaceId == fixture.SpaceId);

        hasSpaceMembership.Should().BeTrue(
            "after adding a person by phone (with a linked user account), the user should have a SpaceMembership");
    }

    [Fact]
    public async Task AddPersonByPhone_WithLinkedUser_ShouldGrantSpaceViewPermission_ButDoesNot()
    {
        // Arrange
        var fixture = await SetupAsync();
        var handler = new AddPersonByPhoneCommandHandler(fixture.Db, Substitute.For<IPeakMemberTracker>(), TestContactLookupProtector.Create());
        var command = new AddPersonByPhoneCommand(
            fixture.SpaceId,
            fixture.GroupId,
            "+972501234567",
            fixture.OwnerUserId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — This SHOULD pass but will FAIL on unfixed code (proving the bug)
        var hasPermission = await fixture.Db.SpacePermissionGrants
            .AnyAsync(g => g.UserId == fixture.JoiningUserId
                && g.SpaceId == fixture.SpaceId
                && g.PermissionKey == Permissions.SpaceView);

        hasPermission.Should().BeTrue(
            "after adding a person by phone (with a linked user), the user should have the space.view permission");
    }
}
