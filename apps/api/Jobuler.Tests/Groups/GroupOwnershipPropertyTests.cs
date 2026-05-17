// Feature: group-ownership
// Property tests P1–P10 for group ownership model.
// Validates: Tasks 19.1, 19.2 from group-ownership spec

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Groups.Commands;
using Jobuler.Application.Groups.Queries;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Identity;
using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Groups;

public class GroupOwnershipPropertyTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IEmailSender NoOpEmail()
    {
        var e = Substitute.For<IEmailSender>();
        e.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return e;
    }

    /// <summary>
    /// Seeds a user + linked person in the given space.
    /// Returns (db, spaceId, userId, personId).
    /// </summary>
    private static async Task<(AppDbContext db, Guid spaceId, Guid userId, Guid personId)> SetupAsync()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var user = User.Create("test@test.com", "Test User", "hash");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(user, userId);
        db.Users.Add(user);

        var person = Person.Create(spaceId, "Test User", linkedUserId: userId);
        db.People.Add(person);

        await db.SaveChangesAsync();
        return (db, spaceId, userId, person.Id);
    }

    private static async Task<Guid> CreateGroupAsync(AppDbContext db, Guid spaceId, Guid userId, string name = "Test Group")
    {
        var handler = new CreateGroupCommandHandler(db, Substitute.For<IPeriodManager>());
        return await handler.Handle(
            new CreateGroupCommand(spaceId, null, name, null, userId),
            CancellationToken.None);
    }

    // ── Property 1: Creator auto-membership ──────────────────────────────────
    // Feature: group-ownership, Property 1: creator auto-membership

    [Fact]
    public async Task Property1_CreateGroup_CreatorIsOwnerMember()
    {
        var (db, spaceId, userId, _) = await SetupAsync();

        var groupId = await CreateGroupAsync(db, spaceId, userId);

        var membership = await db.GroupMemberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.GroupId == groupId);

        membership.Should().NotBeNull("creator should be auto-added as member");
        membership!.IsOwner.Should().BeTrue("creator should be the owner");
    }

    // ── Property 2: Exactly one owner per group ───────────────────────────────
    // Feature: group-ownership, Property 2: exactly one owner per group

    [Fact]
    public async Task Property2_ExactlyOneOwnerPerGroup()
    {
        var (db, spaceId, userId, _) = await SetupAsync();

        var groupId = await CreateGroupAsync(db, spaceId, userId);

        var ownerCount = await db.GroupMemberships.AsNoTracking()
            .CountAsync(m => m.GroupId == groupId && m.IsOwner);

        ownerCount.Should().Be(1, "exactly one owner per group");
    }

    // ── Property 3: Owner removal rejected ───────────────────────────────────
    // Feature: group-ownership, Property 3: owner removal rejected

    [Fact]
    public async Task Property3_RemoveOwner_ThrowsInvalidOperation()
    {
        var (db, spaceId, userId, personId) = await SetupAsync();

        var groupId = await CreateGroupAsync(db, spaceId, userId);

        var handler = new RemovePersonFromGroupCommandHandler(db, new Helpers.NoOpCacheService());

        var act = () => handler.Handle(
            new RemovePersonFromGroupCommand(spaceId, groupId, personId),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*owner*");
    }

    // ── Property 4: Soft-deleted groups excluded from GetGroupsQuery ──────────
    // Feature: group-ownership, Property 4: soft-deleted groups excluded

    [Fact]
    public async Task Property4_SoftDeletedGroup_ExcludedFromQuery()
    {
        var (db, spaceId, userId, _) = await SetupAsync();

        var groupId = await CreateGroupAsync(db, spaceId, userId);

        var queryHandler = new GetGroupsQueryHandler(db);

        // Verify it appears before deletion
        var before = await queryHandler.Handle(new GetGroupsQuery(spaceId), CancellationToken.None);
        before.Should().Contain(g => g.Id == groupId);

        // Soft delete
        var deleteHandler = new SoftDeleteGroupCommandHandler(db);
        await deleteHandler.Handle(new SoftDeleteGroupCommand(spaceId, groupId, userId), CancellationToken.None);

        // Verify it's excluded after deletion
        var after = await queryHandler.Handle(new GetGroupsQuery(spaceId), CancellationToken.None);
        after.Should().NotContain(g => g.Id == groupId, "soft-deleted group should be excluded");
    }

    // ── Property 5: Soft-delete preserves membership rows ────────────────────
    // Feature: group-ownership, Property 5: soft-delete preserves memberships

    [Fact]
    public async Task Property5_SoftDelete_PreservesMembershipRows()
    {
        var (db, spaceId, userId, _) = await SetupAsync();

        var groupId = await CreateGroupAsync(db, spaceId, userId);

        var countBefore = await db.GroupMemberships.CountAsync(m => m.GroupId == groupId);

        var deleteHandler = new SoftDeleteGroupCommandHandler(db);
        await deleteHandler.Handle(new SoftDeleteGroupCommand(spaceId, groupId, userId), CancellationToken.None);

        var countAfter = await db.GroupMemberships.CountAsync(m => m.GroupId == groupId);

        countAfter.Should().Be(countBefore, "soft-delete must not remove membership rows");
    }

    // ── Property 6: Soft-delete / restore round trip ──────────────────────────
    // Feature: group-ownership, Property 6: soft-delete restore round trip

    [Fact]
    public async Task Property6_SoftDeleteRestore_GroupReappearsInQuery()
    {
        var (db, spaceId, userId, _) = await SetupAsync();

        var groupId = await CreateGroupAsync(db, spaceId, userId);

        var deleteHandler = new SoftDeleteGroupCommandHandler(db);
        var restoreHandler = new RestoreGroupCommandHandler(db, NoOpEmail());
        var queryHandler = new GetGroupsQueryHandler(db);

        await deleteHandler.Handle(new SoftDeleteGroupCommand(spaceId, groupId, userId), CancellationToken.None);
        await restoreHandler.Handle(new RestoreGroupCommand(spaceId, groupId, userId), CancellationToken.None);

        var groups = await queryHandler.Handle(new GetGroupsQuery(spaceId), CancellationToken.None);
        groups.Should().Contain(g => g.Id == groupId, "restored group should reappear");
    }

    // ── Property 9: Non-owner rejection ──────────────────────────────────────
    // Feature: group-ownership, Property 9: non-owner rejection

    [Theory]
    [InlineData("rename")]
    [InlineData("delete")]
    public async Task Property9_NonOwner_OwnerOnlyCommandsThrowUnauthorized(string command)
    {
        var (db, spaceId, userId, _) = await SetupAsync();

        var groupId = await CreateGroupAsync(db, spaceId, userId);

        // Create a second user who is NOT the owner
        var nonOwnerUserId = Guid.NewGuid();
        var nonOwnerUser = User.Create("other@test.com", "Other User", "hash");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(nonOwnerUser, nonOwnerUserId);
        db.Users.Add(nonOwnerUser);
        var nonOwnerPerson = Person.Create(spaceId, "Other User", linkedUserId: nonOwnerUserId);
        db.People.Add(nonOwnerPerson);
        await db.SaveChangesAsync();

        Func<Task> act = command switch
        {
            "rename" => () => new RenameGroupCommandHandler(db).Handle(
                new RenameGroupCommand(spaceId, groupId, nonOwnerUserId, "New Name"), CancellationToken.None),
            "delete" => () => new SoftDeleteGroupCommandHandler(db).Handle(
                new SoftDeleteGroupCommand(spaceId, groupId, nonOwnerUserId), CancellationToken.None),
            _ => throw new ArgumentException()
        };

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            $"{command} by non-owner should throw UnauthorizedAccessException");
    }

    // ── Property 10: Rename rejects blank or >100-char names ─────────────────
    // Feature: group-ownership, Property 10: rename rejects invalid names
    // Note: RenameGroupCommand uses FluentValidation — the validator rejects these.
    // In unit tests without the MediatR pipeline, we test the domain entity directly.

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Property10_Rename_RejectsBlankNames(string invalidName)
    {
        var (db, spaceId, userId, _) = await SetupAsync();
        var groupId = await CreateGroupAsync(db, spaceId, userId);

        var group = await db.Groups.FindAsync(groupId);
        var act = () => { group!.Rename(invalidName); return Task.CompletedTask; };

        await act.Should().ThrowAsync<Exception>("blank name should be rejected by domain entity");
    }

    [Fact]
    public async Task Property10_Rename_RejectsNameOver100Chars()
    {
        var (db, spaceId, userId, _) = await SetupAsync();
        var groupId = await CreateGroupAsync(db, spaceId, userId);

        var group = await db.Groups.FindAsync(groupId);
        var longName = new string('a', 101);
        var act = () => { group!.Rename(longName); return Task.CompletedTask; };

        await act.Should().ThrowAsync<Exception>("name over 100 chars should be rejected");
    }
}

// ── Property 7: GetDeletedGroupsQuery respects 30-day window ─────────────────
// Feature: group-ownership, Property 7: deleted groups 30-day window

public class GroupOwnershipPropertyTests_P7_P15
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IEmailSender NoOpEmail()
    {
        var e = Substitute.For<IEmailSender>();
        e.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return e;
    }

    private static async Task<(AppDbContext db, Guid spaceId, Guid userId, Guid personId)> SetupAsync()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var user = User.Create("test@test.com", "Test User", "hash");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(user, userId);
        db.Users.Add(user);

        var person = Person.Create(spaceId, "Test User", linkedUserId: userId);
        db.People.Add(person);

        await db.SaveChangesAsync();
        return (db, spaceId, userId, person.Id);
    }

    private static async Task<Guid> CreateGroupAsync(AppDbContext db, Guid spaceId, Guid userId, string name = "Test Group")
    {
        var handler = new CreateGroupCommandHandler(db, Substitute.For<IPeriodManager>());
        return await handler.Handle(
            new CreateGroupCommand(spaceId, null, name, null, userId),
            CancellationToken.None);
    }

    // Feature: group-ownership, Property 7: GetDeletedGroupsQuery respects 30-day window

    [Fact]
    public async Task Property7_DeletedGroupOlderThan30Days_NotReturnedByQuery()
    {
        var (db, spaceId, userId, _) = await SetupAsync();
        var groupId = await CreateGroupAsync(db, spaceId, userId);

        // Soft-delete the group
        var deleteHandler = new SoftDeleteGroupCommandHandler(db);
        await deleteHandler.Handle(new SoftDeleteGroupCommand(spaceId, groupId, userId), CancellationToken.None);

        // Manually backdate DeletedAt to 31 days ago
        var group = await db.Groups.FindAsync(groupId);
        typeof(Group).GetProperty("DeletedAt")!.SetValue(group, DateTime.UtcNow.AddDays(-31));
        await db.SaveChangesAsync();

        var queryHandler = new GetDeletedGroupsQueryHandler(db);
        var result = await queryHandler.Handle(new GetDeletedGroupsQuery(spaceId, userId), CancellationToken.None);

        result.Should().NotContain(g => g.Id == groupId,
            "groups deleted more than 30 days ago should not appear in the deleted groups list");
    }

    [Fact]
    public async Task Property7_DeletedGroupWithin30Days_ReturnedByQuery()
    {
        var (db, spaceId, userId, _) = await SetupAsync();
        var groupId = await CreateGroupAsync(db, spaceId, userId);

        var deleteHandler = new SoftDeleteGroupCommandHandler(db);
        await deleteHandler.Handle(new SoftDeleteGroupCommand(spaceId, groupId, userId), CancellationToken.None);

        var queryHandler = new GetDeletedGroupsQueryHandler(db);
        var result = await queryHandler.Handle(new GetDeletedGroupsQuery(spaceId, userId), CancellationToken.None);

        result.Should().Contain(g => g.Id == groupId,
            "recently deleted group should appear in the deleted groups list");
    }

    // Feature: group-ownership, Property 8: restore triggers notifications for linked members

    [Fact]
    public async Task Property8_Restore_CreatesNotificationForEachLinkedMember()
    {
        var (db, spaceId, userId, _) = await SetupAsync();
        var groupId = await CreateGroupAsync(db, spaceId, userId);

        // Add a second linked member
        var user2Id = Guid.NewGuid();
        var user2 = User.Create("member2@test.com", "Member 2", "hash");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(user2, user2Id);
        db.Users.Add(user2);
        var person2 = Person.Create(spaceId, "Member 2", linkedUserId: user2Id);
        db.People.Add(person2);
        await db.SaveChangesAsync();

        var membership2 = GroupMembership.Create(spaceId, groupId, person2.Id, isOwner: false);
        db.GroupMemberships.Add(membership2);
        await db.SaveChangesAsync();

        // Soft-delete then restore
        var deleteHandler = new SoftDeleteGroupCommandHandler(db);
        await deleteHandler.Handle(new SoftDeleteGroupCommand(spaceId, groupId, userId), CancellationToken.None);

        var notificationsBefore = await db.Notifications.CountAsync();

        var restoreHandler = new RestoreGroupCommandHandler(db, NoOpEmail());
        await restoreHandler.Handle(new RestoreGroupCommand(spaceId, groupId, userId), CancellationToken.None);

        var notificationsAfter = await db.Notifications.CountAsync();

        // 2 linked members (owner + member2) should each get a notification
        (notificationsAfter - notificationsBefore).Should().Be(2,
            "restore should create one notification per linked member");
    }

    // Feature: group-ownership, Property 13: at most one pending transfer per group

    [Fact]
    public async Task Property13_SecondInitiateTransfer_ThrowsConflict()
    {
        var (db, spaceId, userId, personId) = await SetupAsync();
        var groupId = await CreateGroupAsync(db, spaceId, userId);

        // Add a second member to transfer to
        var user2Id = Guid.NewGuid();
        var user2 = User.Create("proposed@test.com", "Proposed", "hash");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(user2, user2Id);
        db.Users.Add(user2);
        var person2 = Person.Create(spaceId, "Proposed", linkedUserId: user2Id);
        db.People.Add(person2);
        await db.SaveChangesAsync();

        var membership2 = GroupMembership.Create(spaceId, groupId, person2.Id, isOwner: false);
        db.GroupMemberships.Add(membership2);
        await db.SaveChangesAsync();

        var handler = new InitiateOwnershipTransferCommandHandler(db, NoOpEmail());
        var cmd = new InitiateOwnershipTransferCommand(spaceId, groupId, userId, person2.Id);

        // First transfer — should succeed
        await handler.Handle(cmd, CancellationToken.None);

        // Second transfer — should throw ConflictException
        var act = () => handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>(
            "a second pending transfer for the same group should throw ConflictException");
    }

    // Feature: group-ownership, Property 14: expired tokens rejected

    [Fact]
    public async Task Property14_ExpiredToken_ThrowsInvalidOperation()
    {
        var (db, spaceId, userId, personId) = await SetupAsync();
        var groupId = await CreateGroupAsync(db, spaceId, userId);

        var user2Id = Guid.NewGuid();
        var user2 = User.Create("proposed2@test.com", "Proposed2", "hash");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(user2, user2Id);
        db.Users.Add(user2);
        var person2 = Person.Create(spaceId, "Proposed2", linkedUserId: user2Id);
        db.People.Add(person2);
        await db.SaveChangesAsync();

        var membership2 = GroupMembership.Create(spaceId, groupId, person2.Id, isOwner: false);
        db.GroupMemberships.Add(membership2);
        await db.SaveChangesAsync();

        // Create a transfer and manually expire it
        var transfer = PendingOwnershipTransfer.Create(spaceId, groupId, personId, person2.Id);
        typeof(PendingOwnershipTransfer).GetProperty("ExpiresAt")!.SetValue(transfer, DateTime.UtcNow.AddHours(-1));
        db.PendingOwnershipTransfers.Add(transfer);
        await db.SaveChangesAsync();

        var handler = new ConfirmOwnershipTransferCommandHandler(db);
        var act = () => handler.Handle(
            new ConfirmOwnershipTransferCommand(transfer.ConfirmationToken),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    // Feature: group-ownership, Property 15: atomic ownership swap

    [Fact]
    public async Task Property15_ConfirmTransfer_AtomicallySwapsOwnership()
    {
        var (db, spaceId, userId, personId) = await SetupAsync();
        var groupId = await CreateGroupAsync(db, spaceId, userId);

        var user2Id = Guid.NewGuid();
        var user2 = User.Create("newowner@test.com", "New Owner", "hash");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(user2, user2Id);
        db.Users.Add(user2);
        var person2 = Person.Create(spaceId, "New Owner", linkedUserId: user2Id);
        db.People.Add(person2);
        await db.SaveChangesAsync();

        var membership2 = GroupMembership.Create(spaceId, groupId, person2.Id, isOwner: false);
        db.GroupMemberships.Add(membership2);
        await db.SaveChangesAsync();

        // Initiate transfer
        var initiateHandler = new InitiateOwnershipTransferCommandHandler(db, NoOpEmail());
        await initiateHandler.Handle(
            new InitiateOwnershipTransferCommand(spaceId, groupId, userId, person2.Id),
            CancellationToken.None);

        var transfer = await db.PendingOwnershipTransfers.FirstAsync(t => t.GroupId == groupId);

        // Confirm transfer
        var confirmHandler = new ConfirmOwnershipTransferCommandHandler(db);
        await confirmHandler.Handle(
            new ConfirmOwnershipTransferCommand(transfer.ConfirmationToken),
            CancellationToken.None);

        // Verify: new owner has IsOwner=true, old owner has IsOwner=false
        var oldOwnerMembership = await db.GroupMemberships.AsNoTracking()
            .FirstAsync(m => m.GroupId == groupId && m.PersonId == personId);
        var newOwnerMembership = await db.GroupMemberships.AsNoTracking()
            .FirstAsync(m => m.GroupId == groupId && m.PersonId == person2.Id);
        var pendingTransferExists = await db.PendingOwnershipTransfers.AnyAsync(t => t.GroupId == groupId);

        oldOwnerMembership.IsOwner.Should().BeFalse("previous owner should no longer be owner");
        newOwnerMembership.IsOwner.Should().BeTrue("new owner should have IsOwner=true");
        pendingTransferExists.Should().BeFalse("pending transfer record should be deleted after confirmation");
    }
}
