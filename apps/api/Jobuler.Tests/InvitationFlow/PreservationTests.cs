// Feature: invitation-flow-fixes
// Preservation Property Tests — Property 4: No Duplicate SpaceMembership
// Validates: Requirements 3.1, 3.2
//
// These tests MUST PASS on unfixed code AND continue to pass after the fix.
// They guard against regressions by verifying that:
// 1. Users who already have a SpaceMembership don't get duplicates when joining a group
// 2. Users already in a group can re-join without creating duplicate GroupMembership records
// 3. AddPersonByEmail for a user who already has SpaceMembership doesn't create duplicates

using FluentAssertions;
using Jobuler.Application.Groups.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Identity;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jobuler.Tests.InvitationFlow;

public class PreservationTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Seeds a space, a group with a join code, a user who ALREADY has a SpaceMembership,
    /// and a Person linked to that user in the space.
    /// This represents the non-buggy path: user already belongs to the space.
    /// </summary>
    private static async Task<PreservationFixture> SetupWithExistingMembershipAsync()
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

        // Create the joining user who ALREADY has a SpaceMembership
        var joiningUserId = Guid.NewGuid();
        var joiningUser = User.Create("existing@test.com", "Existing User", "hash", phoneNumber: "+972509876543");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(joiningUser, joiningUserId);
        db.Users.Add(joiningUser);

        // Create SpaceMembership for the joining user (they already belong to this space)
        var existingMembership = SpaceMembership.Create(spaceId, joiningUserId);
        db.SpaceMemberships.Add(existingMembership);

        // Create a Person for the joining user in this space
        var joiningPerson = Person.Create(spaceId, "Existing User", linkedUserId: joiningUserId);
        db.People.Add(joiningPerson);
        await db.SaveChangesAsync();

        return new PreservationFixture(
            db, spaceId, group.Id, group.JoinCode!,
            ownerUserId, ownerPerson.Id,
            joiningUserId, joiningPerson.Id);
    }

    private record PreservationFixture(
        AppDbContext Db,
        Guid SpaceId,
        Guid GroupId,
        string JoinCode,
        Guid OwnerUserId,
        Guid OwnerPersonId,
        Guid JoiningUserId,
        Guid JoiningPersonId);

    // ── Preservation 3.1: No duplicate SpaceMembership on JoinGroupByCode ────
    // **Validates: Requirements 3.1**

    [Fact]
    public async Task JoinGroupByCode_WithExistingSpaceMembership_DoesNotCreateDuplicate()
    {
        // Arrange
        var fixture = await SetupWithExistingMembershipAsync();
        var handler = new JoinGroupByCodeCommandHandler(fixture.Db);
        var command = new JoinGroupByCodeCommand(fixture.JoinCode, fixture.JoiningUserId);

        // Count SpaceMemberships before
        var countBefore = await fixture.Db.SpaceMemberships
            .CountAsync(sm => sm.UserId == fixture.JoiningUserId && sm.SpaceId == fixture.SpaceId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — SpaceMembership count should stay at 1 (no duplicate created)
        var countAfter = await fixture.Db.SpaceMemberships
            .CountAsync(sm => sm.UserId == fixture.JoiningUserId && sm.SpaceId == fixture.SpaceId);

        countBefore.Should().Be(1, "precondition: user should have exactly 1 SpaceMembership before joining");
        countAfter.Should().Be(1, "no duplicate SpaceMembership should be created when user already has one");
        result.Should().NotBeNull("join should succeed");
        result.GroupId.Should().Be(fixture.GroupId);
    }

    // ── Preservation 3.2: Re-joining a group does not create duplicate GroupMembership ──
    // **Validates: Requirements 3.2**

    [Fact]
    public async Task JoinGroupByCode_AlreadyMember_ReturnsSuccessWithoutDuplicateMembership()
    {
        // Arrange
        var fixture = await SetupWithExistingMembershipAsync();

        // First, add the user to the group so they're already a member
        fixture.Db.GroupMemberships.Add(
            GroupMembership.Create(fixture.SpaceId, fixture.GroupId, fixture.JoiningPersonId));
        await fixture.Db.SaveChangesAsync();

        var handler = new JoinGroupByCodeCommandHandler(fixture.Db);
        var command = new JoinGroupByCodeCommand(fixture.JoinCode, fixture.JoiningUserId);

        // Count GroupMemberships before
        var countBefore = await fixture.Db.GroupMemberships
            .CountAsync(gm => gm.GroupId == fixture.GroupId && gm.PersonId == fixture.JoiningPersonId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — GroupMembership count should stay the same (no duplicate)
        var countAfter = await fixture.Db.GroupMemberships
            .CountAsync(gm => gm.GroupId == fixture.GroupId && gm.PersonId == fixture.JoiningPersonId);

        countBefore.Should().Be(1, "precondition: user should have exactly 1 GroupMembership before re-joining");
        countAfter.Should().Be(1, "no duplicate GroupMembership should be created when user is already a member");
        result.Should().NotBeNull("re-join should succeed");
        result.GroupId.Should().Be(fixture.GroupId);
    }

    // ── Preservation 3.1: AddPersonByEmail with existing SpaceMembership ─────
    // **Validates: Requirements 3.1**

    [Fact]
    public async Task AddPersonByEmail_WithExistingSpaceMembership_DoesNotCreateDuplicate()
    {
        // Arrange
        var fixture = await SetupWithExistingMembershipAsync();
        var handler = new AddPersonByEmailCommandHandler(fixture.Db, new NoOpPeakMemberTracker());
        var command = new AddPersonByEmailCommand(
            fixture.SpaceId,
            fixture.GroupId,
            "existing@test.com", // matches the joining user's email
            fixture.OwnerUserId);

        // Count SpaceMemberships before
        var countBefore = await fixture.Db.SpaceMemberships
            .CountAsync(sm => sm.UserId == fixture.JoiningUserId && sm.SpaceId == fixture.SpaceId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — SpaceMembership count should stay at 1 (no duplicate created)
        var countAfter = await fixture.Db.SpaceMemberships
            .CountAsync(sm => sm.UserId == fixture.JoiningUserId && sm.SpaceId == fixture.SpaceId);

        countBefore.Should().Be(1, "precondition: user should have exactly 1 SpaceMembership before being added by email");
        countAfter.Should().Be(1, "no duplicate SpaceMembership should be created when user already has one");
    }
}
