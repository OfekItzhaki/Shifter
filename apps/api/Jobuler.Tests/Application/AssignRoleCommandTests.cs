using FluentAssertions;
using Jobuler.Application.People.Commands;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jobuler.Tests.Application;

public class AssignRoleCommandTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(AppDbContext db, Guid spaceId, Guid personId, Guid roleId)> SeedAsync()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();

        var person = Person.Create(spaceId, "Alice");
        var role   = SpaceRole.Create(spaceId, "Commander", Guid.NewGuid());

        db.People.Add(person);
        db.SpaceRoles.Add(role);
        await db.SaveChangesAsync();

        return (db, spaceId, person.Id, role.Id);
    }

    [Fact]
    public async Task AssignRole_WhenNotAssigned_CreatesAssignment()
    {
        var (db, spaceId, personId, roleId) = await SeedAsync();
        var handler = new AssignRoleToPersonCommandHandler(db);

        await handler.Handle(new AssignRoleToPersonCommand(spaceId, personId, roleId), default);

        var assignment = await db.PersonRoleAssignments
            .FirstOrDefaultAsync(a => a.PersonId == personId && a.RoleId == roleId);
        assignment.Should().NotBeNull();
        assignment!.SpaceId.Should().Be(spaceId);
    }

    [Fact]
    public async Task AssignRole_WhenAlreadyAssigned_IsIdempotent()
    {
        var (db, spaceId, personId, roleId) = await SeedAsync();
        var handler = new AssignRoleToPersonCommandHandler(db);
        var cmd = new AssignRoleToPersonCommand(spaceId, personId, roleId);

        await handler.Handle(cmd, default);
        await handler.Handle(cmd, default); // second call — should not throw or duplicate

        var count = await db.PersonRoleAssignments
            .CountAsync(a => a.PersonId == personId && a.RoleId == roleId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task AssignRole_WithUnknownPerson_ThrowsKeyNotFoundException()
    {
        var (db, spaceId, _, roleId) = await SeedAsync();
        var handler = new AssignRoleToPersonCommandHandler(db);

        var act = () => handler.Handle(
            new AssignRoleToPersonCommand(spaceId, Guid.NewGuid(), roleId), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AssignRole_WithUnknownRole_ThrowsKeyNotFoundException()
    {
        var (db, spaceId, personId, _) = await SeedAsync();
        var handler = new AssignRoleToPersonCommandHandler(db);

        var act = () => handler.Handle(
            new AssignRoleToPersonCommand(spaceId, personId, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AssignRole_CrossSpace_ThrowsKeyNotFoundException()
    {
        var (db, _, personId, roleId) = await SeedAsync();
        var handler = new AssignRoleToPersonCommandHandler(db);

        // Different spaceId — person and role exist but not in this space
        var act = () => handler.Handle(
            new AssignRoleToPersonCommand(Guid.NewGuid(), personId, roleId), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RemoveRole_WhenAssigned_RemovesAssignment()
    {
        var (db, spaceId, personId, roleId) = await SeedAsync();

        await new AssignRoleToPersonCommandHandler(db).Handle(
            new AssignRoleToPersonCommand(spaceId, personId, roleId), default);

        await new RemoveRoleFromPersonCommandHandler(db).Handle(
            new RemoveRoleFromPersonCommand(spaceId, personId, roleId), default);

        var assignment = await db.PersonRoleAssignments
            .FirstOrDefaultAsync(a => a.PersonId == personId && a.RoleId == roleId);
        assignment.Should().BeNull();
    }

    [Fact]
    public async Task RemoveRole_WhenNotAssigned_IsIdempotent()
    {
        var (db, spaceId, personId, roleId) = await SeedAsync();
        var handler = new RemoveRoleFromPersonCommandHandler(db);

        var act = () => handler.Handle(
            new RemoveRoleFromPersonCommand(spaceId, personId, roleId), default);

        await act.Should().NotThrowAsync();
    }
}
