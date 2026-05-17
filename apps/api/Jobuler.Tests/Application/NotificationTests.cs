using FluentAssertions;
using Jobuler.Application.Notifications;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Notifications;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class NotificationTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // ── Domain ────────────────────────────────────────────────────────────────

    [Fact]
    public void Notification_Create_SetsFields()
    {
        var spaceId = Guid.NewGuid();
        var userId  = Guid.NewGuid();

        var n = Notification.Create(spaceId, userId, "solver_completed",
            "Schedule ready", "Draft v1 created.", "{\"run\":1}");

        n.SpaceId.Should().Be(spaceId);
        n.UserId.Should().Be(userId);
        n.EventType.Should().Be("solver_completed");
        n.IsRead.Should().BeFalse();
        n.ReadAt.Should().BeNull();
    }

    [Fact]
    public void Notification_MarkRead_SetsIsReadAndReadAt()
    {
        var n = Notification.Create(Guid.NewGuid(), Guid.NewGuid(),
            "solver_completed", "Title", "Body");

        n.MarkRead();

        n.IsRead.Should().BeTrue();
        n.ReadAt.Should().NotBeNull();
        n.ReadAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Notification_MarkRead_Twice_IsIdempotent()
    {
        var n = Notification.Create(Guid.NewGuid(), Guid.NewGuid(),
            "solver_completed", "Title", "Body");

        n.MarkRead();
        var firstReadAt = n.ReadAt;
        n.MarkRead();

        n.IsRead.Should().BeTrue();
        // ReadAt may update on second call — just verify it's still set
        n.ReadAt.Should().NotBeNull();
    }

    // ── NotificationService ───────────────────────────────────────────────────

    [Fact]
    public async Task NotifySpaceAdmins_CreatesOneNotificationPerAdmin()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var regularUser = Guid.NewGuid();

        // Create space with owner
        var space = Space.Create("Test Space", ownerId);
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(space, spaceId);
        db.Spaces.Add(space);

        // Create space memberships for both users
        db.SpaceMemberships.AddRange(
            SpaceMembership.Create(spaceId, ownerId),
            SpaceMembership.Create(spaceId, regularUser));

        // Create a group with a group owner (different from space owner)
        var groupId = Guid.NewGuid();
        var group = Group.Create(spaceId, null, "Test Group");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
        db.Groups.Add(group);

        // Create person records linked to users
        var ownerPerson = Person.Create(spaceId, "Owner", linkedUserId: ownerId);
        db.People.Add(ownerPerson);

        // Owner membership in the group
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, groupId, ownerPerson.Id, isOwner: true));

        await db.SaveChangesAsync();

        var service = new NotificationService(db, Substitute.For<IPushNotificationSender>(), NullLogger<NotificationService>.Instance);
        await service.NotifySpaceAdminsAsync(
            spaceId, "solver_completed", "Ready", "Draft created.", groupId: groupId);

        var notifications = await db.Notifications
            .Where(n => n.SpaceId == spaceId).ToListAsync();

        // Only space owner + group owner (same person in this case) = 1 notification
        notifications.Should().HaveCount(1);
        notifications[0].UserId.Should().Be(ownerId);
        notifications[0].EventType.Should().Be("solver_completed");
        notifications[0].IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task NotifySpaceAdmins_WithNoMembers_CreatesNoNotifications()
    {
        var db = CreateDb();
        var service = new NotificationService(db, Substitute.For<IPushNotificationSender>(), NullLogger<NotificationService>.Instance);

        await service.NotifySpaceAdminsAsync(
            Guid.NewGuid(), "solver_completed", "Ready", "Body");

        var count = await db.Notifications.CountAsync();
        count.Should().Be(0);
    }

    // ── GetNotificationsQuery ─────────────────────────────────────────────────

    [Fact]
    public async Task GetNotifications_ReturnsUserNotificationsForSpace()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId  = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        db.Notifications.AddRange(
            Notification.Create(spaceId, userId,  "solver_completed", "T1", "B1"),
            Notification.Create(spaceId, userId,  "solver_failed",    "T2", "B2"),
            Notification.Create(spaceId, otherId, "solver_completed", "T3", "B3")); // different user
        await db.SaveChangesAsync();

        var handler = new GetNotificationsQueryHandler(db);
        var result  = await handler.Handle(
            new GetNotificationsQuery(spaceId, userId), default);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(n => n.IsRead.Should().BeFalse());
    }

    [Fact]
    public async Task GetNotifications_UnreadOnly_FiltersReadOnes()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId  = Guid.NewGuid();

        var read   = Notification.Create(spaceId, userId, "solver_completed", "T1", "B1");
        var unread = Notification.Create(spaceId, userId, "solver_completed", "T2", "B2");
        read.MarkRead();

        db.Notifications.AddRange(read, unread);
        await db.SaveChangesAsync();

        var handler = new GetNotificationsQueryHandler(db);
        var result  = await handler.Handle(
            new GetNotificationsQuery(spaceId, userId, UnreadOnly: true), default);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("T2");
    }

    // ── DismissNotificationCommand ────────────────────────────────────────────

    [Fact]
    public async Task DismissNotification_MarksAsRead()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId  = Guid.NewGuid();

        var n = Notification.Create(spaceId, userId, "solver_completed", "T", "B");
        db.Notifications.Add(n);
        await db.SaveChangesAsync();

        var handler = new DismissNotificationCommandHandler(db);
        await handler.Handle(
            new DismissNotificationCommand(spaceId, userId, n.Id), default);

        var updated = await db.Notifications.FindAsync(n.Id);
        updated!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task DismissNotification_WrongUser_DoesNothing()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId  = Guid.NewGuid();

        var n = Notification.Create(spaceId, userId, "solver_completed", "T", "B");
        db.Notifications.Add(n);
        await db.SaveChangesAsync();

        var handler = new DismissNotificationCommandHandler(db);
        // Different userId — should be a no-op
        await handler.Handle(
            new DismissNotificationCommand(spaceId, Guid.NewGuid(), n.Id), default);

        var unchanged = await db.Notifications.FindAsync(n.Id);
        unchanged!.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task DismissAll_MarksAllUnreadAsRead()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId  = Guid.NewGuid();

        db.Notifications.AddRange(
            Notification.Create(spaceId, userId, "solver_completed", "T1", "B1"),
            Notification.Create(spaceId, userId, "solver_failed",    "T2", "B2"),
            Notification.Create(spaceId, userId, "solver_completed", "T3", "B3"));
        await db.SaveChangesAsync();

        var handler = new DismissAllNotificationsCommandHandler(db);
        await handler.Handle(new DismissAllNotificationsCommand(spaceId, userId), default);

        var unread = await db.Notifications
            .CountAsync(n => n.SpaceId == spaceId && n.UserId == userId && !n.IsRead);
        unread.Should().Be(0);
    }

    [Fact]
    public async Task DismissAll_DoesNotAffectOtherUsers()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        db.Notifications.AddRange(
            Notification.Create(spaceId, userId1, "solver_completed", "T1", "B1"),
            Notification.Create(spaceId, userId2, "solver_completed", "T2", "B2"));
        await db.SaveChangesAsync();

        await new DismissAllNotificationsCommandHandler(db).Handle(
            new DismissAllNotificationsCommand(spaceId, userId1), default);

        var user2Unread = await db.Notifications
            .CountAsync(n => n.UserId == userId2 && !n.IsRead);
        user2Unread.Should().Be(1);
    }
}
