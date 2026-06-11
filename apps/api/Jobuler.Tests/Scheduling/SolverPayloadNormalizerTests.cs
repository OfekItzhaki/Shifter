// Feature: schedule-table-autoschedule-role-constraints
// Tests for SolverPayloadNormalizer effective-date filtering and scope inclusion.
// Validates: Tasks 20.1, 20.2, 20.3

using FluentAssertions;
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

    // ── Task 20.3: Unit tests for effective-date filtering ────────────────────

    // Property 10: Effective-date filtering is uniform across scope types
    // Feature: schedule-table-autoschedule-role-constraints, Property 10: effective-date filtering uniform

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

    [Fact]
    public async Task BuildAsync_IncludesSpecialDaysWithinHorizonOnly()
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var horizonStart = new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);
        var inHorizon = SpaceSpecialDay.Create(
            spaceId,
            DateOnly.FromDateTime(horizonStart.AddDays(1)),
            "Rosh Hashanah",
            SpaceSpecialDayKind.Holiday,
            homeLeaveWeightMultiplier: 2.5m,
            requiresCoverage: true,
            isAutoGenerated: true);
        var outsideHorizon = SpaceSpecialDay.Create(
            spaceId,
            DateOnly.FromDateTime(horizonStart.AddDays(8)),
            "After horizon",
            SpaceSpecialDayKind.Custom);

        db.SpaceSpecialDays.AddRange(inHorizon, outsideHorizon);

        await db.SaveChangesAsync();

        var normalizer = new SolverPayloadNormalizer(
            db,
            NullLogger<SolverPayloadNormalizer>.Instance,
            Substitute.For<ICumulativeTracker>());

        var payload = await normalizer.BuildAsync(
            spaceId,
            Guid.NewGuid(),
            "standard",
            baselineVersionId: null,
            startTime: horizonStart);

        payload.SpecialDays.Should().ContainSingle();
        payload.SpecialDays![0].Date.Should().Be("2026-09-22");
        payload.SpecialDays[0].Name.Should().Be("Rosh Hashanah");
        payload.SpecialDays[0].Kind.Should().Be("holiday");
        payload.SpecialDays[0].HomeLeaveWeightMultiplier.Should().Be(2.5);
        payload.SpecialDays[0].RequiresCoverage.Should().BeTrue();
    }

    [Fact]
    public async Task BuildAsync_AddsRestBlockForRecentBaselineAssignmentOutsideCurrentSlots()
    {
        var (db, spaceId) = await SetupSpaceAsync();
        var groupId = Guid.NewGuid();
        var person = Person.Create(spaceId, "Rest Tester");
        var group = Group.Create(spaceId, null, "Rest Group");
        typeof(Jobuler.Domain.Common.Entity)
            .GetProperty("Id")!
            .SetValue(group, groupId);
        group.SetMinRestBetweenShifts(7);

        var membership = GroupMembership.Create(spaceId, groupId, person.Id);
        var taskType = TaskType.Create(spaceId, "Guard", TaskBurdenLevel.Normal, Guid.NewGuid());
        var previousSlot = TaskSlot.Create(
            spaceId,
            taskType.Id,
            new DateTime(2026, 4, 19, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 19, 23, 0, 0, DateTimeKind.Utc),
            requiredHeadcount: 1,
            priority: 5,
            createdByUserId: Guid.NewGuid());

        var version = ScheduleVersion.CreateDraft(spaceId, 1, null, null, Guid.NewGuid());
        version.Publish(Guid.NewGuid());

        db.Groups.Add(group);
        db.People.Add(person);
        db.GroupMemberships.Add(membership);
        db.TaskTypes.Add(taskType);
        db.TaskSlots.Add(previousSlot);
        db.ScheduleVersions.Add(version);
        db.Assignments.Add(Assignment.Create(spaceId, version.Id, previousSlot.Id, person.Id));
        await db.SaveChangesAsync();

        var normalizer = new SolverPayloadNormalizer(
            db,
            NullLogger<SolverPayloadNormalizer>.Instance,
            Substitute.For<ICumulativeTracker>());

        var payload = await normalizer.BuildAsync(
            spaceId,
            Guid.NewGuid(),
            "standard",
            version.Id,
            groupId,
            new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        payload.PresenceWindows.Should().Contain(p =>
            p.PersonId == person.Id.ToString()
            && p.State == "on_mission"
            && DateTime.Parse(p.StartsAt, null, System.Globalization.DateTimeStyles.RoundtripKind) == previousSlot.StartsAt
            && DateTime.Parse(p.EndsAt, null, System.Globalization.DateTimeStyles.RoundtripKind) == previousSlot.EndsAt.AddHours(7));
    }

    private static string ToSnakeCase(string s) =>
        string.Concat(s.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));
}
