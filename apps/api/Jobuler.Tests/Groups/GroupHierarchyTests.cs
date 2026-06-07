using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Groups.Commands;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Spaces.Commands;
using Jobuler.Domain.Identity;
using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Groups;

public class GroupHierarchyTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<(AppDbContext Db, Guid SpaceId, Guid UserId)> SetupAsync()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var user = User.Create("owner@example.com", "Owner", "hash");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(user, userId);
        db.Users.Add(user);
        db.People.Add(Person.Create(spaceId, "Owner", linkedUserId: userId));

        await db.SaveChangesAsync();
        return (db, spaceId, userId);
    }

    private static async Task<Guid> CreateGroupAsync(
        AppDbContext db,
        Guid spaceId,
        Guid userId,
        string name,
        Guid? parentGroupId = null)
    {
        var handler = new CreateGroupCommandHandler(db, Substitute.For<IPeriodManager>());
        return await handler.Handle(
            new CreateGroupCommand(spaceId, null, name, null, userId, ParentGroupId: parentGroupId),
            CancellationToken.None);
    }

    private static LinkParentGroupCommandHandler CreateLinkHandler(AppDbContext db)
    {
        var permissions = Substitute.For<IPermissionService>();
        permissions.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return new LinkParentGroupCommandHandler(db, permissions);
    }

    [Fact]
    public async Task CreateGroup_WithParent_SavesParentGroupId()
    {
        var (db, spaceId, userId) = await SetupAsync();
        var restaurantId = await CreateGroupAsync(db, spaceId, userId, "Restaurant A");

        var kitchenId = await CreateGroupAsync(db, spaceId, userId, "Kitchen", restaurantId);

        var kitchen = await db.Groups.AsNoTracking().SingleAsync(g => g.Id == kitchenId);
        kitchen.ParentGroupId.Should().Be(restaurantId);
    }

    [Fact]
    public async Task LinkParent_AllowsDeepHierarchy_AndRejectsCycles()
    {
        var (db, spaceId, userId) = await SetupAsync();
        var restaurantId = await CreateGroupAsync(db, spaceId, userId, "Restaurant A");
        var kitchenId = await CreateGroupAsync(db, spaceId, userId, "Kitchen");
        var morningShiftId = await CreateGroupAsync(db, spaceId, userId, "Morning Shift");
        var handler = CreateLinkHandler(db);

        await handler.Handle(
            new LinkParentGroupCommand(spaceId, kitchenId, restaurantId, userId),
            CancellationToken.None);
        await handler.Handle(
            new LinkParentGroupCommand(spaceId, morningShiftId, kitchenId, userId),
            CancellationToken.None);

        var morningShift = await db.Groups.AsNoTracking().SingleAsync(g => g.Id == morningShiftId);
        morningShift.ParentGroupId.Should().Be(kitchenId);

        var act = () => handler.Handle(
            new LinkParentGroupCommand(spaceId, restaurantId, morningShiftId, userId),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*circular*");
    }
}
