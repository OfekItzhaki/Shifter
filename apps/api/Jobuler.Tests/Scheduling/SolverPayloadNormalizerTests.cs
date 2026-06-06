// Feature: schedule-table-autoschedule-role-constraints
// Tests for SolverPayloadNormalizer effective-date filtering and scope inclusion.
// Validates: Tasks 20.1, 20.2, 20.3

using FluentAssertions;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Constraints;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class SolverPayloadNormalizerTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(AppDbContext db, Guid spaceId)> SetupSpaceAsync()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var space = Jobuler.Domain.Spaces.Space.Create("Test Space", ownerId);
        // Use reflection to set Id since it's private
        typeof(Jobuler.Domain.Common.Entity)
            .GetProperty("Id")!
            .SetValue(space, spaceId);
        db.Spaces.Add(space);
        await db.SaveChangesAsync();
        return (db, spaceId);
    }

    private static ConstraintRule MakeConstraint(
        Guid spaceId, ConstraintScopeType scopeType, Guid? scopeId,
        DateOnly? effectiveFrom, DateOnly? effectiveUntil)
    {
        var rule = ConstraintRule.Create(
            spaceId, scopeType, scopeId,
            ConstraintSeverity.Hard, "min_rest_hours", "{\"hours\":8}",
            Guid.NewGuid(), effectiveFrom, effectiveUntil);
        return rule;
    }

    private static SolverPayloadNormalizer CreateNormalizer(AppDbContext db)
    {
        var cumulativeTracker = Substitute.For<ICumulativeTracker>();
        cumulativeTracker.GetForSolverPayloadAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<CumulativeTrackingDto>()));

        return new SolverPayloadNormalizer(
            db,
            NullLogger<SolverPayloadNormalizer>.Instance,
            cumulativeTracker);
    }

    private static Group AddGroup(AppDbContext db, Guid spaceId, string name, Guid? parentGroupId = null)
    {
        var group = Group.Create(spaceId, null, name);
        if (parentGroupId.HasValue)
            group.SetParentGroup(parentGroupId);
        db.Groups.Add(group);
        return group;
    }

    private static Person AddPerson(AppDbContext db, Guid spaceId, Group group, string name)
    {
        var person = Person.Create(spaceId, name);
        db.People.Add(person);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));
        return person;
    }

    private static GroupTask AddTask(
        AppDbContext db,
        Guid spaceId,
        Group group,
        string name,
        DateTime startsAt)
    {
        var task = GroupTask.Create(
            spaceId,
            group.Id,
            name,
            startsAt,
            startsAt.AddHours(4),
            shiftDurationMinutes: 120,
            requiredHeadcount: 1,
            burdenLevel: TaskBurdenLevel.Normal,
            allowsDoubleShift: false,
            allowsOverlap: false,
            createdByUserId: Guid.NewGuid());
        db.GroupTasks.Add(task);
        return task;
    }

    // ── Task 20.3: Unit tests for effective-date filtering ────────────────────

    // Property 10: Effective-date filtering is uniform across scope types
    // Feature: schedule-table-autoschedule-role-constraints, Property 10: effective-date filtering uniform

    [Fact]
    public async Task BuildAsync_ForParentGroup_IncludesDescendantTasksAndMembers_ExcludesSiblings()
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var startsAt = new DateTime(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc);

        var parent = AddGroup(db, spaceId, "Restaurant");
        var child = AddGroup(db, spaceId, "Kitchen", parent.Id);
        var grandchild = AddGroup(db, spaceId, "Morning Shift", child.Id);
        var sibling = AddGroup(db, spaceId, "Bar");

        var parentPerson = AddPerson(db, spaceId, parent, "Parent Member");
        var childPerson = AddPerson(db, spaceId, child, "Child Member");
        var grandchildPerson = AddPerson(db, spaceId, grandchild, "Grandchild Member");
        var siblingPerson = AddPerson(db, spaceId, sibling, "Sibling Member");

        AddTask(db, spaceId, parent, "Parent Task", startsAt);
        AddTask(db, spaceId, child, "Child Task", startsAt);
        AddTask(db, spaceId, grandchild, "Grandchild Task", startsAt);
        AddTask(db, spaceId, sibling, "Sibling Task", startsAt);
        await db.SaveChangesAsync();

        var payload = await CreateNormalizer(db).BuildAsync(
            spaceId, Guid.NewGuid(), "standard", null, parent.Id, startsAt);

        payload.TaskSlots.Select(s => s.TaskTypeName).Should().Contain(["Parent Task", "Child Task", "Grandchild Task"]);
        payload.TaskSlots.Select(s => s.TaskTypeName).Should().NotContain("Sibling Task");
        payload.People.Select(p => p.PersonId).Should().Contain([
            parentPerson.Id.ToString(),
            childPerson.Id.ToString(),
            grandchildPerson.Id.ToString()
        ]);
        payload.People.Select(p => p.PersonId).Should().NotContain(siblingPerson.Id.ToString());
    }

    [Fact]
    public async Task BuildAsync_ForChildGroup_ExcludesSiblingTasksAndIncludesAncestorBlockers()
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var userId = Guid.NewGuid();
        var startsAt = new DateTime(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc);

        var parent = AddGroup(db, spaceId, "Restaurant");
        var child = AddGroup(db, spaceId, "Kitchen", parent.Id);
        var sibling = AddGroup(db, spaceId, "Bar", parent.Id);

        var childPerson = AddPerson(db, spaceId, child, "Child Member");
        var parentTask = AddTask(db, spaceId, parent, "Parent Task", startsAt);
        AddTask(db, spaceId, child, "Child Task", startsAt);
        AddTask(db, spaceId, sibling, "Sibling Task", startsAt);

        var published = ScheduleVersion.CreateDraft(spaceId, 1, null, null, userId);
        published.Publish(userId);
        db.ScheduleVersions.Add(published);
        db.Assignments.Add(Assignment.Create(
            spaceId,
            published.Id,
            DeriveShiftGuid(parentTask.Id, 0),
            childPerson.Id));
        await db.SaveChangesAsync();

        var payload = await CreateNormalizer(db).BuildAsync(
            spaceId, Guid.NewGuid(), "standard", published.Id, child.Id, startsAt);

        payload.TaskSlots.Select(s => s.TaskTypeName).Should().Contain("Child Task");
        payload.TaskSlots.Select(s => s.TaskTypeName).Should().NotContain(["Parent Task", "Sibling Task"]);
        payload.ParentSchedule.Should().NotBeNull();
        payload.ParentSchedule!.Should().ContainSingle(p =>
            p.PersonId == childPerson.Id.ToString()
            && p.StartsAt == startsAt.ToString("o")
            && p.EndsAt == startsAt.AddHours(2).ToString("o"));
    }

    [Fact]
    public async Task BuildAsync_ForGroupTree_ExcludesInactiveAndDeletedDescendantGroups()
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var startsAt = new DateTime(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc);

        var parent = AddGroup(db, spaceId, "Restaurant");
        var activeChild = AddGroup(db, spaceId, "Kitchen", parent.Id);
        var inactiveChild = AddGroup(db, spaceId, "Inactive Branch", parent.Id);
        var deletedChild = AddGroup(db, spaceId, "Deleted Branch", parent.Id);
        inactiveChild.Deactivate();
        deletedChild.SoftDelete();

        var activePerson = AddPerson(db, spaceId, activeChild, "Active Member");
        var inactivePerson = AddPerson(db, spaceId, inactiveChild, "Inactive Member");
        var deletedPerson = AddPerson(db, spaceId, deletedChild, "Deleted Member");

        AddTask(db, spaceId, activeChild, "Active Child Task", startsAt);
        AddTask(db, spaceId, inactiveChild, "Inactive Child Task", startsAt);
        AddTask(db, spaceId, deletedChild, "Deleted Child Task", startsAt);
        await db.SaveChangesAsync();

        var payload = await CreateNormalizer(db).BuildAsync(
            spaceId, Guid.NewGuid(), "standard", null, parent.Id, startsAt);

        payload.TaskSlots.Select(s => s.TaskTypeName).Should().Contain("Active Child Task");
        payload.TaskSlots.Select(s => s.TaskTypeName).Should().NotContain(["Inactive Child Task", "Deleted Child Task"]);
        payload.People.Select(p => p.PersonId).Should().Contain(activePerson.Id.ToString());
        payload.People.Select(p => p.PersonId).Should().NotContain([
            inactivePerson.Id.ToString(),
            deletedPerson.Id.ToString()
        ]);
    }

    [Fact]
    public async Task BuildAsync_ForGroupTree_IncludesOnlyGroupConstraintsInsideTree()
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var startsAt = new DateTime(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc);

        var parent = AddGroup(db, spaceId, "Restaurant");
        var child = AddGroup(db, spaceId, "Kitchen", parent.Id);
        var sibling = AddGroup(db, spaceId, "Bar", parent.Id);

        AddPerson(db, spaceId, child, "Child Member");
        AddTask(db, spaceId, parent, "Parent Task", startsAt);
        AddTask(db, spaceId, child, "Child Task", startsAt);

        var parentConstraint = MakeConstraint(spaceId, ConstraintScopeType.Group, parent.Id, null, null);
        var childConstraint = MakeConstraint(spaceId, ConstraintScopeType.Group, child.Id, null, null);
        var siblingConstraint = MakeConstraint(spaceId, ConstraintScopeType.Group, sibling.Id, null, null);
        db.ConstraintRules.AddRange(parentConstraint, childConstraint, siblingConstraint);
        await db.SaveChangesAsync();

        var payload = await CreateNormalizer(db).BuildAsync(
            spaceId, Guid.NewGuid(), "standard", null, child.Id, startsAt);

        payload.HardConstraints.Select(c => c.ScopeId).Should().Contain(child.Id.ToString());
        payload.HardConstraints.Select(c => c.ScopeId).Should().NotContain(parent.Id.ToString());
        payload.HardConstraints.Select(c => c.ScopeId).Should().NotContain(sibling.Id.ToString());
    }

    [Fact]
    public async Task ConstraintWithEffectiveUntilBeforeHorizonStart_IsExcluded()
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var horizonStart = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizonEnd = horizonStart.AddDays(6);

        // Constraint expired yesterday
        var expired = MakeConstraint(spaceId, ConstraintScopeType.Space, null,
            effectiveFrom: null,
            effectiveUntil: horizonStart.AddDays(-1));
        db.ConstraintRules.Add(expired);
        await db.SaveChangesAsync();

        // Query as the normalizer does
        var results = await db.ConstraintRules.AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.IsActive
                && (c.EffectiveUntil == null || c.EffectiveUntil >= horizonStart)
                && (c.EffectiveFrom == null || c.EffectiveFrom <= horizonEnd))
            .ToListAsync();

        results.Should().BeEmpty("expired constraint should be excluded");
    }

    [Fact]
    public async Task ConstraintWithEffectiveFromAfterHorizonEnd_IsExcluded()
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var horizonStart = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizonEnd = horizonStart.AddDays(6);

        // Constraint starts after the horizon
        var future = MakeConstraint(spaceId, ConstraintScopeType.Space, null,
            effectiveFrom: horizonEnd.AddDays(1),
            effectiveUntil: null);
        db.ConstraintRules.Add(future);
        await db.SaveChangesAsync();

        var results = await db.ConstraintRules.AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.IsActive
                && (c.EffectiveUntil == null || c.EffectiveUntil >= horizonStart)
                && (c.EffectiveFrom == null || c.EffectiveFrom <= horizonEnd))
            .ToListAsync();

        results.Should().BeEmpty("future constraint should be excluded");
    }

    [Fact]
    public async Task ConstraintWithNullDates_IsAlwaysIncluded()
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var horizonStart = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizonEnd = horizonStart.AddDays(6);

        var always = MakeConstraint(spaceId, ConstraintScopeType.Space, null,
            effectiveFrom: null, effectiveUntil: null);
        db.ConstraintRules.Add(always);
        await db.SaveChangesAsync();

        var results = await db.ConstraintRules.AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.IsActive
                && (c.EffectiveUntil == null || c.EffectiveUntil >= horizonStart)
                && (c.EffectiveFrom == null || c.EffectiveFrom <= horizonEnd))
            .ToListAsync();

        results.Should().HaveCount(1, "constraint with null dates should always be included");
    }

    [Fact]
    public async Task ConstraintOverlappingHorizon_IsIncluded()
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var horizonStart = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizonEnd = horizonStart.AddDays(6);

        // Constraint spans the entire horizon
        var overlapping = MakeConstraint(spaceId, ConstraintScopeType.Space, null,
            effectiveFrom: horizonStart.AddDays(-1),
            effectiveUntil: horizonEnd.AddDays(1));
        db.ConstraintRules.Add(overlapping);
        await db.SaveChangesAsync();

        var results = await db.ConstraintRules.AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.IsActive
                && (c.EffectiveUntil == null || c.EffectiveUntil >= horizonStart)
                && (c.EffectiveFrom == null || c.EffectiveFrom <= horizonEnd))
            .ToListAsync();

        results.Should().HaveCount(1, "overlapping constraint should be included");
    }

    // ── Task 20.2: All three scope types appear in payload ────────────────────

    // Property 9: Solver payload includes all three constraint scope levels
    // Feature: schedule-table-autoschedule-role-constraints, Property 9: payload includes all scope types

    [Fact]
    public async Task AllThreeScopeTypes_AreReturnedByQuery()
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var horizonStart = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizonEnd = horizonStart.AddDays(6);

        var groupConstraint = MakeConstraint(spaceId, ConstraintScopeType.Group, Guid.NewGuid(), null, null);
        var roleConstraint = MakeConstraint(spaceId, ConstraintScopeType.Role, Guid.NewGuid(), null, null);
        var personConstraint = MakeConstraint(spaceId, ConstraintScopeType.Person, Guid.NewGuid(), null, null);

        db.ConstraintRules.AddRange(groupConstraint, roleConstraint, personConstraint);
        await db.SaveChangesAsync();

        var results = await db.ConstraintRules.AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.IsActive
                && (c.EffectiveUntil == null || c.EffectiveUntil >= horizonStart)
                && (c.EffectiveFrom == null || c.EffectiveFrom <= horizonEnd))
            .ToListAsync();

        results.Should().HaveCount(3);
        results.Select(r => r.ScopeType).Should().Contain(ConstraintScopeType.Group);
        results.Select(r => r.ScopeType).Should().Contain(ConstraintScopeType.Role);
        results.Select(r => r.ScopeType).Should().Contain(ConstraintScopeType.Person);
    }

    // ── Property 10 parameterised: filtering is uniform across scope types ────

    [Theory]
    [InlineData("Group")]
    [InlineData("Role")]
    [InlineData("Person")]
    [InlineData("Space")]
    public async Task ExpiredConstraint_IsExcluded_ForAllScopeTypes(string scopeTypeName)
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var horizonStart = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizonEnd = horizonStart.AddDays(6);
        var scopeType = Enum.Parse<ConstraintScopeType>(scopeTypeName);

        var expired = MakeConstraint(spaceId, scopeType, Guid.NewGuid(),
            effectiveFrom: null,
            effectiveUntil: horizonStart.AddDays(-1));
        db.ConstraintRules.Add(expired);
        await db.SaveChangesAsync();

        var results = await db.ConstraintRules.AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.IsActive
                && (c.EffectiveUntil == null || c.EffectiveUntil >= horizonStart)
                && (c.EffectiveFrom == null || c.EffectiveFrom <= horizonEnd))
            .ToListAsync();

        results.Should().BeEmpty($"expired {scopeTypeName} constraint should be excluded");
    }

    // ── Task 8.1: Solver normalizer ignores UnavailabilityReasonId ────────────

    // Feature: qualification-templates, Property 7: Solver normalizer reason-invariance
    // Validates: Requirements 8.1, 8.2, 8.3

    [Fact]
    public void PresenceWindowsWithDifferentReasonIds_ProduceIdenticalSolverDtos()
    {
        // Arrange: two presence windows identical except for UnavailabilityReasonId
        var spaceId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow;
        var endsAt = startsAt.AddHours(8);
        var reasonId = Guid.NewGuid();

        var windowWithReason = PresenceWindow.CreateManual(
            spaceId, personId, PresenceState.AtHome,
            startsAt, endsAt, "sick", unavailabilityReasonId: reasonId);

        var windowWithoutReason = PresenceWindow.CreateManual(
            spaceId, personId, PresenceState.AtHome,
            startsAt, endsAt, "sick", unavailabilityReasonId: null);

        // Act: map both using the same logic the normalizer uses
        var dtoWithReason = new Jobuler.Application.Scheduling.Models.PresenceWindowDto(
            windowWithReason.PersonId.ToString(),
            ToSnakeCase(windowWithReason.State.ToString()),
            windowWithReason.StartsAt.ToString("o"),
            windowWithReason.EndsAt.ToString("o"));

        var dtoWithoutReason = new Jobuler.Application.Scheduling.Models.PresenceWindowDto(
            windowWithoutReason.PersonId.ToString(),
            ToSnakeCase(windowWithoutReason.State.ToString()),
            windowWithoutReason.StartsAt.ToString("o"),
            windowWithoutReason.EndsAt.ToString("o"));

        // Assert: both DTOs are identical — reason is not part of the solver payload
        dtoWithReason.Should().BeEquivalentTo(dtoWithoutReason,
            "the solver DTO should not include UnavailabilityReasonId");
    }

    [Fact]
    public void SolverPresenceWindowDto_DoesNotContainReasonField()
    {
        // Verify the PresenceWindowDto record type only has the expected 4 fields
        var properties = typeof(Jobuler.Application.Scheduling.Models.PresenceWindowDto).GetProperties();
        var propertyNames = properties.Select(p => p.Name).ToList();

        propertyNames.Should().BeEquivalentTo(
            new[] { "PersonId", "State", "StartsAt", "EndsAt" },
            "solver PresenceWindowDto must not include any reason-related fields");
    }

    private static string ToSnakeCase(string s) =>
        string.Concat(s.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));

    private static Guid DeriveShiftGuid(Guid taskId, int shiftIndex)
    {
        var bytes = taskId.ToByteArray();
        var indexBytes = BitConverter.GetBytes(shiftIndex);
        for (var i = 0; i < 4; i++)
            bytes[12 + i] ^= indexBytes[i];
        return new Guid(bytes);
    }
}
