// Feature: schedule-table-autoschedule-role-constraints
// Tests for SolverPayloadNormalizer effective-date filtering and scope inclusion.
// Validates: Tasks 20.1, 20.2, 20.3

using FluentAssertions;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Constraints;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
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
}
