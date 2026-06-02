// Feature: space-management
// Property-based tests for audit logging and home-leave config propagation (Task 10.3)
//
// Property 12: Audit logging completeness
// Property 13: Home-leave config propagates to solver payloads
//
// Validates: Requirements 1.5, 2.5, 3.7, 6.2, 6.3, 6.5

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Application.Spaces.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

/// <summary>
/// Property 12: Audit logging completeness.
/// For any auditable space management action (soft-delete, restore, ownership transfer,
/// role assign), the IAuditLogger is called with all required fields: actor user ID,
/// space ID, action name, and timestamp (implicit via call occurrence).
///
/// Property 13: Home-leave config propagates to solver payloads.
/// For any SpaceHomeLeaveConfig values, the solver payload normalizer uses space-level
/// values. Verified by testing the BuildHomeLeaveConfigDto helper produces correct output
/// from space-level config.
/// </summary>
[Trait("Feature", "space-management")]
public class AuditAndPropagationPropertyTests
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

    private static IAuditLogger CreateMockAudit()
    {
        var audit = Substitute.For<IAuditLogger>();
        audit.LogAsync(
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return audit;
    }

    private static SolverPayloadNormalizer CreateNormalizer(AppDbContext db)
    {
        var logger = Substitute.For<ILogger<SolverPayloadNormalizer>>();
        var cumulativeTracker = Substitute.For<ICumulativeTracker>();
        cumulativeTracker.GetForSolverPayloadAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<CumulativeTrackingDto>()));
        return new SolverPayloadNormalizer(db, logger, cumulativeTracker);
    }

    // ── Property 12: Audit logging completeness ──────────────────────────────
    // **Validates: Requirements 1.5, 2.5, 3.7**

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AuditActionArbitrary) })]
    public Property Property12_SoftDelete_ProducesAuditLogWithRequiredFields(AuditActionInput input)
    {
        return Prop.ForAll(Arb.From(Gen.Constant(input)), _ =>
        {
            // Arrange
            var db = CreateDb();
            var ownerId = Guid.NewGuid();
            var space = Space.Create(input.SpaceName, ownerId);
            db.Spaces.Add(space);

            // Add some groups for cascade
            for (int i = 0; i < input.GroupCount; i++)
            {
                var group = Group.Create(space.Id, null, $"Group {i}");
                db.Groups.Add(group);
            }
            db.SaveChanges();

            var audit = CreateMockAudit();
            var handler = new SoftDeleteSpaceCommandHandler(db, AllowAllPermissions(), audit);
            var command = new SoftDeleteSpaceCommand(space.Id, ownerId);

            // Act
            handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

            // Assert — IAuditLogger.LogAsync was called with required fields
            audit.Received(1).LogAsync(
                Arg.Is<Guid?>(s => s == space.Id),           // space ID
                Arg.Is<Guid?>(a => a == ownerId),            // actor user ID
                Arg.Is<string>(action => action == "space.soft_delete"), // action name
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
        });
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AuditActionArbitrary) })]
    public Property Property12_Restore_ProducesAuditLogWithRequiredFields(AuditActionInput input)
    {
        return Prop.ForAll(Arb.From(Gen.Constant(input)), _ =>
        {
            // Arrange
            var db = CreateDb();
            var ownerId = Guid.NewGuid();
            var space = Space.Create(input.SpaceName, ownerId);
            space.SoftDelete(); // Must be deleted to restore
            db.Spaces.Add(space);

            for (int i = 0; i < input.GroupCount; i++)
            {
                var group = Group.Create(space.Id, null, $"Group {i}");
                group.SoftDeleteBySpace();
                db.Groups.Add(group);
            }
            db.SaveChanges();

            var audit = CreateMockAudit();
            var handler = new RestoreSpaceCommandHandler(db, AllowAllPermissions(), audit);
            var command = new RestoreSpaceCommand(space.Id, ownerId);

            // Act
            handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

            // Assert — IAuditLogger.LogAsync was called with required fields
            audit.Received(1).LogAsync(
                Arg.Is<Guid?>(s => s == space.Id),           // space ID
                Arg.Is<Guid?>(a => a == ownerId),            // actor user ID
                Arg.Is<string>(action => action == "space.restore"), // action name
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
        });
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AuditTransferArbitrary) })]
    public Property Property12_Transfer_ProducesAuditLogWithRequiredFields(AuditTransferInput input)
    {
        return Prop.ForAll(Arb.From(Gen.Constant(input)), _ =>
        {
            // Arrange
            var db = CreateDb();
            var ownerId = Guid.NewGuid();
            var space = Space.Create(input.SpaceName, ownerId);
            db.Spaces.Add(space);

            // Owner membership
            var ownerMembership = SpaceMembership.Create(space.Id, ownerId);
            db.SpaceMemberships.Add(ownerMembership);

            // Target member
            var targetId = Guid.NewGuid();
            var targetMembership = SpaceMembership.Create(space.Id, targetId);
            db.SpaceMemberships.Add(targetMembership);

            db.SaveChanges();

            var audit = CreateMockAudit();
            var handler = new TransferOwnershipCommandHandler(db, AllowAllPermissions(), audit);
            var command = new TransferOwnershipCommand(space.Id, targetId, ownerId, input.Reason);

            // Act
            handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

            // Assert — IAuditLogger.LogAsync was called with required fields
            audit.Received(1).LogAsync(
                Arg.Is<Guid?>(s => s == space.Id),           // space ID
                Arg.Is<Guid?>(a => a == ownerId),            // actor user ID
                Arg.Is<string>(action => action == "space.ownership_transfer"), // action name
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
        });
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AuditRoleAssignArbitrary) })]
    public Property Property12_RoleAssign_ProducesAuditLogWithRequiredFields(AuditRoleAssignInput input)
    {
        return Prop.ForAll(Arb.From(Gen.Constant(input)), _ =>
        {
            // Arrange
            var db = CreateDb();
            var ownerId = Guid.NewGuid();
            var space = Space.Create("Test Space", ownerId);
            db.Spaces.Add(space);

            // Target member
            var targetId = Guid.NewGuid();
            var targetMembership = SpaceMembership.Create(space.Id, targetId);
            db.SpaceMemberships.Add(targetMembership);

            db.SaveChanges();

            var audit = CreateMockAudit();
            var handler = new AssignSpaceRoleCommandHandler(db, AllowAllPermissions(), audit);
            var command = new AssignSpaceRoleCommand(space.Id, targetId, input.Level, ownerId);

            // Act
            handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

            // Assert — IAuditLogger.LogAsync was called with required fields
            audit.Received(1).LogAsync(
                Arg.Is<Guid?>(s => s == space.Id),           // space ID
                Arg.Is<Guid?>(a => a == ownerId),            // actor user ID
                Arg.Is<string>(action => action == "space.role_assign"), // action name
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
        });
    }

    // ── Property 13: Home-leave config propagates to solver payloads ─────────
    // **Validates: Requirements 6.2, 6.3, 6.5**

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(HomeLeaveConfigArbitrary) })]
    public Property Property13_SpaceLevelConfig_OverridesGroupLevelInSolverPayload(HomeLeaveConfigInput input)
    {
        return Prop.ForAll(Arb.From(Gen.Constant(input)), _ =>
        {
            // Arrange
            var db = CreateDb();
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();

            // Create space
            var space = Space.Create("Test Space", ownerId);
            typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(space, spaceId);
            db.Spaces.Add(space);

            // Create a closed-base group
            var group = Group.Create(spaceId, null, "Closed Base Group", null, null);
            typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
            // Set IsClosedBase to true via reflection (private setter)
            typeof(Group).GetProperty("IsClosedBase")!.SetValue(group, true);
            db.Groups.Add(group);

            // Create space-level home-leave config
            var spaceHlConfig = SpaceHomeLeaveConfig.Create(
                spaceId,
                minRestHours: input.MinRestHours,
                eligibilityThresholdHours: input.EligibilityThresholdHours,
                leaveCapacity: input.LeaveCapacity,
                leaveDurationHours: input.LeaveDurationHours,
                balanceValue: input.BalanceValue,
                mode: input.Mode,
                baseDays: input.BaseDays,
                homeDays: input.HomeDays,
                minPeopleAtBase: input.MinPeopleAtBase);
            db.SpaceHomeLeaveConfigs.Add(spaceHlConfig);

            // Add some group members so leave_capacity can be derived
            for (int i = 0; i < input.MemberCount; i++)
            {
                var personId = Guid.NewGuid();
                var person = Person.Create(spaceId, $"Person{i}", $"Last{i}");
                typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(person, personId);
                db.People.Add(person);

                var gm = GroupMembership.Create(spaceId, groupId, personId);
                db.GroupMemberships.Add(gm);
            }

            // Create a group task so the normalizer has something to build
            var now = DateTime.UtcNow;
            var startsAt = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
            var endsAt = startsAt.AddDays(7);
            var task = GroupTask.Create(
                spaceId, groupId, "Test Task",
                startsAt, endsAt, 480,
                requiredHeadcount: 1,
                burdenLevel: TaskBurdenLevel.Normal,
                allowsDoubleShift: false,
                allowsOverlap: false,
                createdByUserId: ownerId);
            db.GroupTasks.Add(task);

            db.SaveChanges();

            var normalizer = CreateNormalizer(db);

            // Act
            var payload = normalizer.BuildAsync(
                spaceId,
                runId: Guid.NewGuid(),
                triggerMode: "standard",
                baselineVersionId: null,
                groupId: groupId,
                startTime: startsAt,
                ct: CancellationToken.None).GetAwaiter().GetResult();

            // Assert — home-leave config in payload uses space-level values
            var hlConfig = payload.HomeLeaveConfig;
            hlConfig.Should().NotBeNull("space-level home-leave config should propagate to solver payload");

            // Expected values based on the BuildHomeLeaveConfigDto logic:
            var expectedLeaveCapacity = Math.Max(1, input.MemberCount - input.MinPeopleAtBase);
            var expectedEligibilityThreshold = (double)(input.BaseDays * 24);
            var expectedBalanceValue = input.Mode == HomeLeaveMode.Manual ? 50 : input.BalanceValue;

            hlConfig!.Enabled.Should().BeTrue();
            hlConfig.LeaveDurationHours.Should().Be((double)input.LeaveDurationHours);
            hlConfig.LeaveCapacity.Should().Be(expectedLeaveCapacity);
            hlConfig.EligibilityThresholdHours.Should().Be(expectedEligibilityThreshold);
            hlConfig.BalanceValue.Should().Be(expectedBalanceValue);
            // MinRestHours is now populated from the group's MinRestBetweenShiftsHours (default 8)
            // rather than hardcoded to 0 — this ensures the solver enforces the correct rest constraint
            hlConfig.MinRestHours.Should().Be(8); // group default
        });
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(HomeLeaveEmergencyArbitrary) })]
    public Property Property13_EmergencyFreeze_UseForScheduling_ProducesCorrectPayload(HomeLeaveEmergencyInput input)
    {
        return Prop.ForAll(Arb.From(Gen.Constant(input)), _ =>
        {
            // Arrange
            var db = CreateDb();
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();

            var space = Space.Create("Test Space", ownerId);
            typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(space, spaceId);
            db.Spaces.Add(space);

            var group = Group.Create(spaceId, null, "Closed Base Group", null, null);
            typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(group, groupId);
            typeof(Group).GetProperty("IsClosedBase")!.SetValue(group, true);
            db.Groups.Add(group);

            // Create space-level config with emergency freeze active + use for scheduling
            var spaceHlConfig = SpaceHomeLeaveConfig.Create(
                spaceId,
                minRestHours: 0,
                eligibilityThresholdHours: 168,
                leaveCapacity: 1,
                leaveDurationHours: input.LeaveDurationHours,
                balanceValue: 50,
                mode: HomeLeaveMode.Automatic,
                baseDays: 7,
                homeDays: 2,
                minPeopleAtBase: input.MinPeopleAtBase);
            spaceHlConfig.ActivateEmergencyFreeze(useForScheduling: true);
            db.SpaceHomeLeaveConfigs.Add(spaceHlConfig);

            // Add members
            for (int i = 0; i < input.MemberCount; i++)
            {
                var personId = Guid.NewGuid();
                var person = Person.Create(spaceId, $"Person{i}", $"Last{i}");
                typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(person, personId);
                db.People.Add(person);

                var gm = GroupMembership.Create(spaceId, groupId, personId);
                db.GroupMemberships.Add(gm);
            }

            // Create a task
            var now = DateTime.UtcNow;
            var startsAt = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
            var endsAt = startsAt.AddDays(7);
            var task = GroupTask.Create(
                spaceId, groupId, "Test Task",
                startsAt, endsAt, 480,
                requiredHeadcount: 1,
                burdenLevel: Jobuler.Domain.Tasks.TaskBurdenLevel.Normal,
                allowsDoubleShift: false,
                allowsOverlap: false,
                createdByUserId: ownerId);
            db.GroupTasks.Add(task);

            db.SaveChanges();

            var normalizer = CreateNormalizer(db);

            // Act
            var payload = normalizer.BuildAsync(
                spaceId,
                runId: Guid.NewGuid(),
                triggerMode: "standard",
                baselineVersionId: null,
                groupId: groupId,
                startTime: startsAt,
                ct: CancellationToken.None).GetAwaiter().GetResult();

            // Assert — emergency freeze with use-for-scheduling produces balance=0, threshold=9999
            var hlConfig = payload.HomeLeaveConfig;
            hlConfig.Should().NotBeNull("emergency freeze with use-for-scheduling should produce config");

            var expectedLeaveCapacity = Math.Max(1, input.MemberCount - input.MinPeopleAtBase);

            hlConfig!.Enabled.Should().BeTrue();
            hlConfig.BalanceValue.Should().Be(0, "emergency freeze sets balance to 0");
            hlConfig.EligibilityThresholdHours.Should().Be(9999, "emergency freeze sets threshold to 9999");
            hlConfig.LeaveCapacity.Should().Be(expectedLeaveCapacity);
            hlConfig.LeaveDurationHours.Should().Be((double)input.LeaveDurationHours);
        });
    }
}

// ── Input records ────────────────────────────────────────────────────────────

/// <summary>
/// Input for Property 12 audit tests (soft-delete and restore).
/// </summary>
public record AuditActionInput(
    string SpaceName,
    int GroupCount
);

/// <summary>
/// Input for Property 12 audit tests (ownership transfer).
/// </summary>
public record AuditTransferInput(
    string SpaceName,
    string? Reason
);

/// <summary>
/// Input for Property 12 audit tests (role assignment).
/// </summary>
public record AuditRoleAssignInput(
    SpacePermissionLevel Level
);

/// <summary>
/// Input for Property 13 home-leave config propagation tests (normal operation).
/// </summary>
public record HomeLeaveConfigInput(
    HomeLeaveMode Mode,
    int BalanceValue,
    int BaseDays,
    int HomeDays,
    int MinPeopleAtBase,
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours,
    int MemberCount
);

/// <summary>
/// Input for Property 13 emergency freeze tests.
/// </summary>
public record HomeLeaveEmergencyInput(
    decimal LeaveDurationHours,
    int MinPeopleAtBase,
    int MemberCount
);

// ── Arbitraries ──────────────────────────────────────────────────────────────

public class AuditActionArbitrary
{
    public static Arbitrary<AuditActionInput> Generate()
    {
        var gen = from name in Gen.Elements("Alpha Space", "Beta Space", "Gamma Space", "Delta Space")
                  from groupCount in Gen.Choose(0, 5)
                  select new AuditActionInput(name, groupCount);

        return Arb.From(gen);
    }
}

public class AuditTransferArbitrary
{
    public static Arbitrary<AuditTransferInput> Generate()
    {
        var gen = from name in Gen.Elements("Alpha Space", "Beta Space", "Gamma Space")
                  from hasReason in Arb.Generate<bool>()
                  from reasonText in Gen.Elements("Leaving", "Promotion", "Handoff", "Retirement")
                  let reason = hasReason ? reasonText : null
                  select new AuditTransferInput(name, reason);

        return Arb.From(gen);
    }
}

public class AuditRoleAssignArbitrary
{
    public static Arbitrary<AuditRoleAssignInput> Generate()
    {
        var gen = from level in Gen.Elements(
                      SpacePermissionLevel.Member,
                      SpacePermissionLevel.Admin,
                      SpacePermissionLevel.GroupOwner)
                  select new AuditRoleAssignInput(level);

        return Arb.From(gen);
    }
}

public class HomeLeaveConfigArbitrary
{
    public static Arbitrary<HomeLeaveConfigInput> Generate()
    {
        var gen = from mode in Gen.Elements(HomeLeaveMode.Automatic, HomeLeaveMode.Manual)
                  from balanceValue in Gen.Choose(0, 100)
                  from baseDays in Gen.Choose(1, 14)
                  from homeDays in Gen.Choose(1, 7)
                  from minPeopleAtBase in Gen.Choose(1, 10)
                  from minRestHoursInt in Gen.Choose(0, 16)
                  from eligibilityInt in Gen.Choose(24, 336)
                  from leaveCapacity in Gen.Choose(1, 5)
                  from leaveDurationInt in Gen.Choose(12, 168)
                  from memberCount in Gen.Choose(5, 20)
                  select new HomeLeaveConfigInput(
                      mode,
                      balanceValue,
                      baseDays,
                      homeDays,
                      minPeopleAtBase,
                      (decimal)minRestHoursInt,
                      (decimal)eligibilityInt,
                      leaveCapacity,
                      (decimal)leaveDurationInt,
                      memberCount);

        return Arb.From(gen);
    }
}

public class HomeLeaveEmergencyArbitrary
{
    public static Arbitrary<HomeLeaveEmergencyInput> Generate()
    {
        var gen = from leaveDurationInt in Gen.Choose(12, 168)
                  from minPeopleAtBase in Gen.Choose(1, 8)
                  from memberCount in Gen.Choose(5, 20)
                  select new HomeLeaveEmergencyInput(
                      (decimal)leaveDurationInt,
                      minPeopleAtBase,
                      memberCount);

        return Arb.From(gen);
    }
}
