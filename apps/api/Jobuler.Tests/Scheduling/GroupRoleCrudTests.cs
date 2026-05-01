// Feature: schedule-table-autoschedule-role-constraints
// Tests for GroupRolesController and group role commands.
// Validates: Task 22.3

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Groups.Commands;
using Jobuler.Application.Groups.Queries;
using Jobuler.Domain.Groups;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class GroupRoleCrudTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IPermissionService AllowAll()
    {
        var svc = Substitute.For<IPermissionService>();
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return svc;
    }

    private static async Task<(AppDbContext db, Guid spaceId, Guid groupId)> SetupAsync()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        // Seed a group so the handler can verify it exists
        var group = Group.Create(spaceId, null, "Test Group");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        return (db, spaceId, groupId);
    }

    // ── Task 22.3: Create role → 201 with ID ─────────────────────────────────

    [Fact]
    public async Task CreateRole_ReturnsId()
    {
        var (db, spaceId, groupId) = await SetupAsync();
        var handler = new CreateGroupRoleCommandHandler(db, AllowAll());

        var id = await handler.Handle(
            new CreateGroupRoleCommand(spaceId, groupId, "Soldier", null, "view", Guid.NewGuid()),
            CancellationToken.None);

        id.Should().NotBeEmpty();
        var role = await db.SpaceRoles.FindAsync(id);
        role.Should().NotBeNull();
        role!.Name.Should().Be("Soldier");
        role.GroupId.Should().Be(groupId);
    }

    // ── Task 22.3: Duplicate name in same group → 409 ────────────────────────

    [Fact]
    public async Task CreateRole_DuplicateNameInSameGroup_ThrowsConflict()
    {
        var (db, spaceId, groupId) = await SetupAsync();
        var handler = new CreateGroupRoleCommandHandler(db, AllowAll());
        var cmd = new CreateGroupRoleCommand(spaceId, groupId, "Medic", null, "view", Guid.NewGuid());

        await handler.Handle(cmd, CancellationToken.None);

        var act = () => handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    // ── Task 22.3: Same name in different group → allowed ────────────────────

    [Fact]
    public async Task CreateRole_SameNameDifferentGroup_Succeeds()
    {
        var (db, spaceId, groupId1) = await SetupAsync();

        // Create a second group
        var groupId2 = Guid.NewGuid();
        var group2 = Group.Create(spaceId, null, "Group 2");
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group2, groupId2);
        db.Groups.Add(group2);
        await db.SaveChangesAsync();

        var handler = new CreateGroupRoleCommandHandler(db, AllowAll());

        var id1 = await handler.Handle(
            new CreateGroupRoleCommand(spaceId, groupId1, "Commander", null, "view", Guid.NewGuid()),
            CancellationToken.None);

        var id2 = await handler.Handle(
            new CreateGroupRoleCommand(spaceId, groupId2, "Commander", null, "view", Guid.NewGuid()),
            CancellationToken.None);

        id1.Should().NotBe(id2, "same name in different groups should create two separate roles");
    }

    // ── Task 22.3: Update role → GET returns updated values ──────────────────

    [Fact]
    public async Task UpdateRole_ReturnsUpdatedValues()
    {
        var (db, spaceId, groupId) = await SetupAsync();
        var createHandler = new CreateGroupRoleCommandHandler(db, AllowAll());
        var updateHandler = new UpdateGroupRoleCommandHandler(db, AllowAll());
        var queryHandler = new GetGroupRolesQueryHandler(db);

        var roleId = await createHandler.Handle(
            new CreateGroupRoleCommand(spaceId, groupId, "Old Name", "Old desc", "view", Guid.NewGuid()),
            CancellationToken.None);

        await updateHandler.Handle(
            new UpdateGroupRoleCommand(spaceId, groupId, roleId, "New Name", "New desc", "ViewAndEdit", Guid.NewGuid()),
            CancellationToken.None);

        var roles = await queryHandler.Handle(new GetGroupRolesQuery(spaceId, groupId), CancellationToken.None);
        var updated = roles.Single(r => r.Id == roleId);

        updated.Name.Should().Be("New Name");
        updated.Description.Should().Be("New desc");
        updated.PermissionLevel.Should().Be("ViewAndEdit");
    }

    // ── Task 22.3: Deactivate role → is_active = false ───────────────────────

    [Fact]
    public async Task DeactivateRole_SetsIsActiveFalse()
    {
        var (db, spaceId, groupId) = await SetupAsync();
        var createHandler = new CreateGroupRoleCommandHandler(db, AllowAll());
        var deactivateHandler = new DeactivateGroupRoleCommandHandler(db, AllowAll());

        var roleId = await createHandler.Handle(
            new CreateGroupRoleCommand(spaceId, groupId, "Temp Role", null, "view", Guid.NewGuid()),
            CancellationToken.None);

        await deactivateHandler.Handle(
            new DeactivateGroupRoleCommand(spaceId, groupId, roleId, Guid.NewGuid()),
            CancellationToken.None);

        var role = await db.SpaceRoles.FindAsync(roleId);
        role!.IsActive.Should().BeFalse();
    }

    // ── Task 22.3: Missing permission → 403 ──────────────────────────────────

    [Fact]
    public async Task CreateRole_MissingPermission_ThrowsUnauthorized()
    {
        var (db, spaceId, groupId) = await SetupAsync();

        var noPermissions = Substitute.For<IPermissionService>();
        noPermissions.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException("Permission denied.")));

        var handler = new CreateGroupRoleCommandHandler(db, noPermissions);

        var act = () => handler.Handle(
            new CreateGroupRoleCommand(spaceId, groupId, "Role", null, "view", Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Property 11: Group role creation is group-scoped ─────────────────────
    // Feature: schedule-table-autoschedule-role-constraints, Property 11: group role creation is group-scoped

    [Theory]
    [InlineData("Alpha")]
    [InlineData("Beta")]
    [InlineData("Gamma")]
    public async Task CreatedRole_HasCorrectGroupId(string roleName)
    {
        var (db, spaceId, groupId) = await SetupAsync();
        var handler = new CreateGroupRoleCommandHandler(db, AllowAll());

        var roleId = await handler.Handle(
            new CreateGroupRoleCommand(spaceId, groupId, roleName, null, "view", Guid.NewGuid()),
            CancellationToken.None);

        var role = await db.SpaceRoles.FindAsync(roleId);
        role!.GroupId.Should().Be(groupId, "role must be scoped to the correct group");
    }

    // ── Property 12: Role update round-trip ──────────────────────────────────
    // Feature: schedule-table-autoschedule-role-constraints, Property 12: role update round-trip

    [Theory]
    [InlineData("Name A", "Desc A", "View")]
    [InlineData("Name B", null, "ViewAndEdit")]
    [InlineData("Name C", "Desc C", "Owner")]
    public async Task UpdateRole_RoundTrip_ValuesMatch(string name, string? desc, string permLevel)
    {
        var (db, spaceId, groupId) = await SetupAsync();
        var createHandler = new CreateGroupRoleCommandHandler(db, AllowAll());
        var updateHandler = new UpdateGroupRoleCommandHandler(db, AllowAll());
        var queryHandler = new GetGroupRolesQueryHandler(db);

        var roleId = await createHandler.Handle(
            new CreateGroupRoleCommand(spaceId, groupId, "Initial", null, "view", Guid.NewGuid()),
            CancellationToken.None);

        await updateHandler.Handle(
            new UpdateGroupRoleCommand(spaceId, groupId, roleId, name, desc, permLevel, Guid.NewGuid()),
            CancellationToken.None);

        var roles = await queryHandler.Handle(new GetGroupRolesQuery(spaceId, groupId), CancellationToken.None);
        var updated = roles.Single(r => r.Id == roleId);

        updated.Name.Should().Be(name);
        updated.Description.Should().Be(desc);
        updated.PermissionLevel.Should().Be(permLevel);
    }
}
