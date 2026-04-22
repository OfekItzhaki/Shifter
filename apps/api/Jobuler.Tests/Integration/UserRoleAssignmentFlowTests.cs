using FluentAssertions;
using Jobuler.Application.Auth.Commands;
using Jobuler.Application.People.Commands;
using Jobuler.Application.People.Queries;
using Jobuler.Application.Spaces.Commands;
using Jobuler.Application.Spaces.Queries;
using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jobuler.Tests.Integration;

/// <summary>
/// Mimics a full admin user flow:
///   Register → Create space → Create person → Create role → Assign role →
///   Get person detail → Verify role appears → Remove role → Verify gone
/// </summary>
public class UserRoleAssignmentFlowTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task FullFlow_RegisterCreateSpaceAssignRole_RoleAppearsInPersonDetail()
    {
        var db = CreateDb();

        // 1. Register admin user
        var registerHandler = new RegisterCommandHandler(db);
        var adminId = await registerHandler.Handle(
            new RegisterCommand("admin@test.local", "Admin User", "Password1!", "en"),
            default);

        // 2. Create a space
        var createSpaceHandler = new CreateSpaceCommandHandler(db);
        var spaceId = await createSpaceHandler.Handle(
            new CreateSpaceCommand("Test Unit", null, "en", adminId),
            default);

        // 3. Create a person in the space
        var createPersonHandler = new CreatePersonCommandHandler(db);
        var personId = await createPersonHandler.Handle(
            new CreatePersonCommand(spaceId, "Bob Smith", "Bob", null, adminId),
            default);

        // 4. Create a role
        var createRoleHandler = new CreateSpaceRoleCommandHandler(db);
        var roleId = await createRoleHandler.Handle(
            new CreateSpaceRoleCommand(spaceId, "Medic", null, adminId),
            default);

        // 5. Assign role to person
        var assignHandler = new AssignRoleToPersonCommandHandler(db);
        await assignHandler.Handle(
            new AssignRoleToPersonCommand(spaceId, personId, roleId),
            default);

        // 6. Get person detail — role should appear
        var detailHandler = new GetPersonDetailQueryHandler(db);
        var detail = await detailHandler.Handle(
            new GetPersonDetailQuery(spaceId, personId, IncludeSensitive: false),
            default);

        detail.Should().NotBeNull();
        detail!.Roles.Should().HaveCount(1);
        detail.Roles[0].RoleId.Should().Be(roleId);
        detail.Roles[0].Name.Should().Be("Medic");
        detail.RoleNames.Should().Contain("Medic");

        // 7. Remove role
        var removeHandler = new RemoveRoleFromPersonCommandHandler(db);
        await removeHandler.Handle(
            new RemoveRoleFromPersonCommand(spaceId, personId, roleId),
            default);

        // 8. Get person detail again — role should be gone
        var detailAfter = await detailHandler.Handle(
            new GetPersonDetailQuery(spaceId, personId, IncludeSensitive: false),
            default);

        detailAfter!.Roles.Should().BeEmpty();
        detailAfter.RoleNames.Should().BeEmpty();
    }

    [Fact]
    public async Task FullFlow_MultipleRoles_AllAppearInDetail()
    {
        var db = CreateDb();
        var adminId = Guid.NewGuid();

        var spaceId = await new CreateSpaceCommandHandler(db).Handle(
            new CreateSpaceCommand("Unit B", null, "en", adminId), default);

        var personId = await new CreatePersonCommandHandler(db).Handle(
            new CreatePersonCommand(spaceId, "Carol", null, null, adminId), default);

        var roleId1 = await new CreateSpaceRoleCommandHandler(db).Handle(
            new CreateSpaceRoleCommand(spaceId, "Driver", null, adminId), default);
        var roleId2 = await new CreateSpaceRoleCommandHandler(db).Handle(
            new CreateSpaceRoleCommand(spaceId, "Cook", null, adminId), default);

        var assignHandler = new AssignRoleToPersonCommandHandler(db);
        await assignHandler.Handle(new AssignRoleToPersonCommand(spaceId, personId, roleId1), default);
        await assignHandler.Handle(new AssignRoleToPersonCommand(spaceId, personId, roleId2), default);

        var detail = await new GetPersonDetailQueryHandler(db).Handle(
            new GetPersonDetailQuery(spaceId, personId, false), default);

        detail!.Roles.Should().HaveCount(2);
        detail.RoleNames.Should().BeEquivalentTo(new[] { "Driver", "Cook" });
    }

    [Fact]
    public async Task FullFlow_TenantIsolation_PersonFromOtherSpaceNotVisible()
    {
        var db = CreateDb();
        var adminId = Guid.NewGuid();

        var spaceA = await new CreateSpaceCommandHandler(db).Handle(
            new CreateSpaceCommand("Space A", null, "en", adminId), default);
        var spaceB = await new CreateSpaceCommandHandler(db).Handle(
            new CreateSpaceCommand("Space B", null, "en", adminId), default);

        var personInA = await new CreatePersonCommandHandler(db).Handle(
            new CreatePersonCommand(spaceA, "Alice", null, null, adminId), default);

        // Query from spaceB — should return null
        var detail = await new GetPersonDetailQueryHandler(db).Handle(
            new GetPersonDetailQuery(spaceB, personInA, false), default);

        detail.Should().BeNull();
    }
}
