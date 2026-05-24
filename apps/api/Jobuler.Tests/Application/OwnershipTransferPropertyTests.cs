// Feature: space-management
// Property-based tests for ownership transfer (Task 6.2)
//
// Property 5: Transfer updates owner and records history
// Property 6: Transfer grants all permissions to new owner
// Property 7: Transfer rejects non-members
//
// Validates: Requirements 3.1, 3.2, 3.3, 3.5, 3.6

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Common;
using Jobuler.Application.Spaces.Commands;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

/// <summary>
/// Property 5: Ownership transfer updates owner and records history.
/// For any space and any active member (not the current owner), transferring ownership
/// SHALL update Space.OwnerUserId to the target user AND create an OwnershipTransferHistory
/// record containing the previous owner, new owner, requesting user, and timestamp.
///
/// Property 6: Ownership transfer grants all permissions to new owner.
/// For any completed ownership transfer, the new owner SHALL have all defined permission
/// keys granted in SpacePermissionGrant.
///
/// Property 7: Transfer rejects non-members.
/// For any space and any user who is NOT an active member of that space, attempting
/// ownership transfer to that user SHALL be rejected with an InvalidOperationException.
/// </summary>
[Trait("Feature", "space-management")]
public class OwnershipTransferPropertyTests
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

    private static IAuditLogger NoOpAudit()
    {
        var audit = Substitute.For<IAuditLogger>();
        audit.LogAsync(
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return audit;
    }

    private static readonly string[] AllPermissionKeys = new[]
    {
        Permissions.SpaceView,
        Permissions.SpaceAdminMode,
        Permissions.PeopleManage,
        Permissions.ConstraintsManage,
        Permissions.RestrictionsManageSensitive,
        Permissions.TasksManage,
        Permissions.ScheduleRecalculate,
        Permissions.SchedulePublish,
        Permissions.ScheduleRollback,
        Permissions.PermissionsManage,
        Permissions.OwnershipTransfer,
        Permissions.LogsViewSensitive,
        Permissions.BillingManage
    };

    /// <summary>
    /// Seeds a space with an owner and a set of active members.
    /// Returns the space, owner ID, and list of member user IDs.
    /// </summary>
    private static async Task<(Space space, Guid ownerId, List<Guid> memberIds)> SeedSpaceWithMembers(
        AppDbContext db, int memberCount)
    {
        var ownerId = Guid.NewGuid();
        var space = Space.Create("Test Space", ownerId);
        db.Spaces.Add(space);

        // Owner is also a member
        var ownerMembership = SpaceMembership.Create(space.Id, ownerId);
        db.SpaceMemberships.Add(ownerMembership);

        var memberIds = new List<Guid>();
        for (int i = 0; i < memberCount; i++)
        {
            var memberId = Guid.NewGuid();
            memberIds.Add(memberId);
            var membership = SpaceMembership.Create(space.Id, memberId);
            db.SpaceMemberships.Add(membership);
        }

        await db.SaveChangesAsync();
        return (space, ownerId, memberIds);
    }

    // ── Property 5: Transfer updates owner and records history ────────────────
    // **Validates: Requirements 3.1, 3.5**

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(OwnershipTransferArbitrary) })]
    public Property Property5_TransferUpdatesOwnerAndRecordsHistory(OwnershipTransferInput input)
    {
        return Prop.ForAll(Arb.From(Gen.Constant(input)), _ =>
        {
            // Arrange
            var db = CreateDb();
            var (space, ownerId, memberIds) = SeedSpaceWithMembers(db, input.MemberCount)
                .GetAwaiter().GetResult();
            var targetUserId = memberIds[input.TargetMemberIndex % memberIds.Count];

            var handler = new TransferOwnershipCommandHandler(db, AllowAllPermissions(), NoOpAudit());

            var command = new TransferOwnershipCommand(
                space.Id, targetUserId, ownerId, input.Reason);

            // Act
            handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

            // Assert — OwnerUserId updated
            var updatedSpace = db.Spaces.Find(space.Id);
            updatedSpace!.OwnerUserId.Should().Be(targetUserId,
                "transfer should update OwnerUserId to the target user");

            // Assert — OwnershipTransferHistory record created
            var history = db.OwnershipTransferHistory
                .FirstOrDefault(h => h.SpaceId == space.Id && h.NewOwnerId == targetUserId);

            history.Should().NotBeNull("transfer should create a history record");
            history!.PreviousOwnerId.Should().Be(ownerId);
            history.NewOwnerId.Should().Be(targetUserId);
            history.TransferredByUserId.Should().Be(ownerId);
            history.Reason.Should().Be(input.Reason);
        });
    }

    // ── Property 6: Transfer grants all permissions to new owner ──────────────
    // **Validates: Requirements 3.6**

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(OwnershipTransferArbitrary) })]
    public Property Property6_TransferGrantsAllPermissionsToNewOwner(OwnershipTransferInput input)
    {
        return Prop.ForAll(Arb.From(Gen.Constant(input)), _ =>
        {
            // Arrange
            var db = CreateDb();
            var (space, ownerId, memberIds) = SeedSpaceWithMembers(db, input.MemberCount)
                .GetAwaiter().GetResult();
            var targetUserId = memberIds[input.TargetMemberIndex % memberIds.Count];

            var handler = new TransferOwnershipCommandHandler(db, AllowAllPermissions(), NoOpAudit());

            var command = new TransferOwnershipCommand(
                space.Id, targetUserId, ownerId, input.Reason);

            // Act
            handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

            // Assert — new owner has all permission keys
            var grants = db.SpacePermissionGrants
                .Where(g => g.SpaceId == space.Id &&
                            g.UserId == targetUserId &&
                            g.RevokedAt == null)
                .Select(g => g.PermissionKey)
                .ToList();

            foreach (var key in AllPermissionKeys)
            {
                grants.Should().Contain(key,
                    $"new owner should have permission '{key}' granted after transfer");
            }
        });
    }

    // ── Property 7: Transfer rejects non-members ─────────────────────────────
    // **Validates: Requirements 3.2, 3.3**

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(NonMemberTransferArbitrary) })]
    public Property Property7_TransferRejectsNonMembers(NonMemberTransferInput input)
    {
        return Prop.ForAll(Arb.From(Gen.Constant(input)), _ =>
        {
            // Arrange
            var db = CreateDb();
            var ownerId = Guid.NewGuid();
            var space = Space.Create("Test Space", ownerId);
            db.Spaces.Add(space);

            // Owner is a member
            var ownerMembership = SpaceMembership.Create(space.Id, ownerId);
            db.SpaceMemberships.Add(ownerMembership);

            // Add some active members (but NOT the target)
            for (int i = 0; i < input.ExistingMemberCount; i++)
            {
                var membership = SpaceMembership.Create(space.Id, Guid.NewGuid());
                db.SpaceMemberships.Add(membership);
            }

            // If testing inactive member, add the target as inactive
            if (input.TargetIsInactiveMember)
            {
                var inactiveMembership = SpaceMembership.Create(space.Id, input.NonMemberUserId);
                inactiveMembership.Deactivate();
                db.SpaceMemberships.Add(inactiveMembership);
            }

            db.SaveChanges();

            var handler = new TransferOwnershipCommandHandler(db, AllowAllPermissions(), NoOpAudit());

            var command = new TransferOwnershipCommand(
                space.Id, input.NonMemberUserId, ownerId, "test reason");

            // Act & Assert — must throw InvalidOperationException
            var act = () => handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*not an active member*");
        });
    }
}

// ── Input records ────────────────────────────────────────────────────────────

/// <summary>
/// Input for Property 5 and 6: valid ownership transfer scenarios.
/// </summary>
public record OwnershipTransferInput(
    int MemberCount,
    int TargetMemberIndex,
    string? Reason
);

/// <summary>
/// Input for Property 7: transfer to non-member scenarios.
/// </summary>
public record NonMemberTransferInput(
    Guid NonMemberUserId,
    int ExistingMemberCount,
    bool TargetIsInactiveMember
);

// ── Arbitraries ──────────────────────────────────────────────────────────────

/// <summary>
/// FsCheck arbitrary for generating valid OwnershipTransferInput values.
/// Generates 1-5 members and a valid target index.
/// </summary>
public class OwnershipTransferArbitrary
{
    public static Arbitrary<OwnershipTransferInput> Generate()
    {
        var gen = from memberCount in Gen.Choose(1, 5)
                  from targetIndex in Gen.Choose(0, 100)
                  from hasReason in Arb.Generate<bool>()
                  from reasonText in Gen.Elements("Leaving company", "Promotion", "Temporary handoff", "Retirement")
                  let reason = hasReason ? reasonText : null
                  select new OwnershipTransferInput(memberCount, targetIndex, reason);

        return Arb.From(gen);
    }
}

/// <summary>
/// FsCheck arbitrary for generating NonMemberTransferInput values.
/// The target user is never an active member of the space.
/// </summary>
public class NonMemberTransferArbitrary
{
    public static Arbitrary<NonMemberTransferInput> Generate()
    {
        var gen = from nonMemberId in Gen.Fresh(() => Guid.NewGuid())
                  from existingMemberCount in Gen.Choose(0, 5)
                  from isInactive in Arb.Generate<bool>()
                  select new NonMemberTransferInput(nonMemberId, existingMemberCount, isInactive);

        return Arb.From(gen);
    }
}
