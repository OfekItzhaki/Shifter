// Feature: space-management
// Property 4: Permission hierarchy enforcement
// **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.7**
//
// For any user at permission level L and any action requiring permission level L' > L,
// the IPermissionService SHALL reject the request. Conversely, for any action requiring
// level L' ≤ L, the request SHALL be permitted.

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Auth;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jobuler.Tests.Application;

[Trait("Feature", "space-management")]
[Trait("Property", "4")]
public class PermissionHierarchyEnforcementPropertyTests
{
    // ── Permission level → required level mapping ─────────────────────────────
    // Owner-only permissions require SpaceOwner (level 3)
    private static readonly string[] OwnerOnlyPermissionKeys =
    {
        Permissions.OwnershipTransfer,
        Permissions.BillingManage,
        Permissions.PermissionsManage,
    };

    // Admin permissions require Admin (level 1) or higher
    private static readonly string[] AdminPermissionKeys =
    {
        Permissions.SpaceView,
        Permissions.SpaceAdminMode,
        Permissions.PeopleManage,
        Permissions.ConstraintsManage,
        Permissions.TasksManage,
        Permissions.ScheduleRecalculate,
        Permissions.SchedulePublish,
        Permissions.ScheduleRollback,
        Permissions.LogsViewSensitive,
        Permissions.RestrictionsManageSensitive,
    };

    /// <summary>
    /// Returns the minimum permission level required for a given permission key.
    /// </summary>
    private static SpacePermissionLevel RequiredLevelFor(string permissionKey)
    {
        if (OwnerOnlyPermissionKeys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase))
            return SpacePermissionLevel.SpaceOwner;
        if (AdminPermissionKeys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase))
            return SpacePermissionLevel.Admin;
        // All other permissions require explicit grants (Member level baseline)
        return SpacePermissionLevel.Member;
    }

    /// <summary>
    /// All known permission keys in the system.
    /// </summary>
    private static readonly string[] AllPermissionKeys = OwnerOnlyPermissionKeys
        .Concat(AdminPermissionKeys)
        .ToArray();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>
    /// Seeds a space with a user at the given permission level.
    /// For SpaceOwner, the user is set as Space.OwnerUserId.
    /// For GroupOwner, a group membership with IsOwner=true is created.
    /// For Admin/Member, a SpaceMembership with the appropriate level is created.
    /// </summary>
    private static async Task<(AppDbContext db, Guid spaceId, Guid userId)> SeedAsync(
        SpacePermissionLevel level)
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();

        // Create the space — owner is either this user (if SpaceOwner) or a different user
        var ownerUserId = level == SpacePermissionLevel.SpaceOwner ? userId : Guid.NewGuid();
        var space = Space.Create("Test Space", ownerUserId);
        db.Spaces.Add(space);
        await db.SaveChangesAsync();

        var spaceId = space.Id;

        // Create space membership
        var membership = SpaceMembership.Create(spaceId, userId);
        membership.SetPermissionLevel(level);
        db.SpaceMemberships.Add(membership);

        // For GroupOwner level, we need to set up group ownership
        // The PermissionService checks GroupMemberships joined with People
        if (level == SpacePermissionLevel.GroupOwner)
        {
            var person = Person.Create(spaceId, "Test Person", linkedUserId: userId);
            db.People.Add(person);
            await db.SaveChangesAsync();

            var group = Group.Create(spaceId, null, "Test Group");
            db.Groups.Add(group);
            await db.SaveChangesAsync();

            var groupMembership = GroupMembership.Create(spaceId, group.Id, person.Id, isOwner: true);
            db.GroupMemberships.Add(groupMembership);
        }

        await db.SaveChangesAsync();
        return (db, spaceId, userId);
    }

    // ── FsCheck Generators ────────────────────────────────────────────────────

    /// <summary>
    /// Generates a random SpacePermissionLevel.
    /// </summary>
    private static Gen<SpacePermissionLevel> GenPermissionLevel() =>
        Gen.Elements(
            SpacePermissionLevel.Member,
            SpacePermissionLevel.Admin,
            SpacePermissionLevel.GroupOwner,
            SpacePermissionLevel.SpaceOwner);

    /// <summary>
    /// Generates a random permission key from the owner-only and admin sets.
    /// These are the keys with well-defined hierarchy behavior.
    /// </summary>
    private static Gen<string> GenPermissionKey() =>
        Gen.Elements(AllPermissionKeys);

    // ── Property Tests ────────────────────────────────────────────────────────

    /// <summary>
    /// Property 4: For any user at level L and action requiring level L' > L,
    /// the PermissionService SHALL deny access.
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property User_Below_Required_Level_Is_Denied()
    {
        // Generate combinations where user level < required level
        var gen = from userLevel in GenPermissionLevel()
                  from permKey in GenPermissionKey()
                  let requiredLevel = RequiredLevelFor(permKey)
                  where userLevel < requiredLevel
                  select (userLevel, permKey, requiredLevel);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (userLevel, permKey, _) = tuple;

            // Arrange
            var (db, spaceId, userId) = SeedAsync(userLevel).GetAwaiter().GetResult();
            var svc = new PermissionService(db);

            // Act
            var result = svc.HasPermissionAsync(userId, spaceId, permKey)
                .GetAwaiter().GetResult();

            // Assert: access denied
            result.Should().BeFalse(
                $"user at level {userLevel} should be denied permission '{permKey}' " +
                $"which requires level {RequiredLevelFor(permKey)}");

            db.Dispose();
        });
    }

    /// <summary>
    /// Property 4: For any user at level L and action requiring level L' ≤ L,
    /// the PermissionService SHALL permit access.
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property User_At_Or_Above_Required_Level_Is_Permitted()
    {
        // Generate combinations where user level >= required level
        var gen = from userLevel in GenPermissionLevel()
                  from permKey in GenPermissionKey()
                  let requiredLevel = RequiredLevelFor(permKey)
                  where userLevel >= requiredLevel
                  select (userLevel, permKey, requiredLevel);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (userLevel, permKey, _) = tuple;

            // Arrange
            var (db, spaceId, userId) = SeedAsync(userLevel).GetAwaiter().GetResult();
            var svc = new PermissionService(db);

            // Act
            var result = svc.HasPermissionAsync(userId, spaceId, permKey)
                .GetAwaiter().GetResult();

            // Assert: access granted
            result.Should().BeTrue(
                $"user at level {userLevel} should be granted permission '{permKey}' " +
                $"which requires level {RequiredLevelFor(permKey)}");

            db.Dispose();
        });
    }

    /// <summary>
    /// Property 4: SpaceOwner implicitly has ALL permissions — no explicit grants needed.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpaceOwner_Has_All_Permissions()
    {
        var gen = GenPermissionKey();

        return Prop.ForAll(Arb.From(gen), permKey =>
        {
            // Arrange: user is the space owner
            var (db, spaceId, userId) = SeedAsync(SpacePermissionLevel.SpaceOwner)
                .GetAwaiter().GetResult();
            var svc = new PermissionService(db);

            // Act
            var result = svc.HasPermissionAsync(userId, spaceId, permKey)
                .GetAwaiter().GetResult();

            // Assert: always granted
            result.Should().BeTrue(
                $"SpaceOwner should have permission '{permKey}' implicitly");

            db.Dispose();
        });
    }

    /// <summary>
    /// Property 4: RequirePermissionAsync throws UnauthorizedAccessException
    /// when user lacks the required permission level.
    /// **Validates: Requirements 4.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RequirePermission_Throws_When_Denied()
    {
        // Generate combinations where user level < required level
        var gen = from userLevel in GenPermissionLevel()
                  from permKey in GenPermissionKey()
                  let requiredLevel = RequiredLevelFor(permKey)
                  where userLevel < requiredLevel
                  select (userLevel, permKey);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (userLevel, permKey) = tuple;

            // Arrange
            var (db, spaceId, userId) = SeedAsync(userLevel).GetAwaiter().GetResult();
            var svc = new PermissionService(db);

            // Act & Assert: RequirePermissionAsync should throw
            var act = () => svc.RequirePermissionAsync(userId, spaceId, permKey)
                .GetAwaiter().GetResult();

            act.Should().Throw<UnauthorizedAccessException>();

            db.Dispose();
        });
    }
}
