using FluentAssertions;
using Jobuler.Application.HomeLeave.Queries;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jobuler.Tests.HomeLeave;

/// <summary>
/// Unit tests for GetFreezePeriodChangesCountQuery handler.
/// Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 7.2, 7.3
/// </summary>
public class GetFreezePeriodChangesCountQueryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Guid SpaceId, Guid GroupId)> SeedGroupWithFreeze(
        AppDbContext db, bool freezeActive, DateTime? freezeStartedAt)
    {
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var group = Group.Create(spaceId, null, "Test Group");
        // Override the Id to a known value
        var groupEntry = db.Groups.Add(group);
        groupEntry.Property("Id").CurrentValue = groupId;
        await db.SaveChangesAsync();

        var config = HomeLeaveConfig.Create(
            spaceId, groupId,
            minRestHours: 0,
            eligibilityThresholdHours: 168,
            leaveCapacity: 2,
            leaveDurationHours: 48);

        if (freezeActive)
        {
            config.ActivateEmergencyFreeze(useForScheduling: false);
        }

        var configEntry = db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        // Override FreezeStartedAt to a specific time if provided
        if (freezeStartedAt.HasValue)
        {
            configEntry.Property("FreezeStartedAt").CurrentValue = freezeStartedAt.Value;
            await db.SaveChangesAsync();
        }
        else if (freezeActive)
        {
            // If freeze is active but we want FreezeStartedAt to be null (edge case)
            configEntry.Property("FreezeStartedAt").CurrentValue = null;
            await db.SaveChangesAsync();
        }

        return (spaceId, groupId);
    }

    private static async Task<Guid> SeedDraftVersion(AppDbContext db, Guid spaceId, int versionNumber = 1)
    {
        var version = ScheduleVersion.CreateDraft(spaceId, versionNumber, null, null, Guid.NewGuid());
        db.ScheduleVersions.Add(version);
        await db.SaveChangesAsync();
        return version.Id;
    }

    private static async Task<Guid> SeedPublishedVersion(AppDbContext db, Guid spaceId, int versionNumber = 1)
    {
        var version = ScheduleVersion.CreateDraft(spaceId, versionNumber, null, null, Guid.NewGuid());
        version.Publish(Guid.NewGuid());
        db.ScheduleVersions.Add(version);
        await db.SaveChangesAsync();
        return version.Id;
    }

    private static async Task SeedAssignment(
        AppDbContext db, Guid spaceId, Guid versionId, AssignmentSource source,
        DateTime createdAt, Guid? taskSlotId = null, string? changeReasonSummary = null)
    {
        var assignment = Assignment.Create(
            spaceId, versionId,
            taskSlotId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            source,
            changeReasonSummary);

        var entry = db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        // Override CreatedAt to a specific time
        entry.Property("CreatedAt").CurrentValue = createdAt;
        await db.SaveChangesAsync();
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirement 7.3
    /// When freeze is not active, the query returns zeros for all categories.
    /// </summary>
    [Fact]
    public async Task ReturnsZeros_WhenFreezeIsNotActive()
    {
        // Arrange
        var db = CreateDb();
        var (spaceId, groupId) = await SeedGroupWithFreeze(db, freezeActive: false, freezeStartedAt: null);
        var handler = new GetFreezePeriodChangesCountQueryHandler(db);

        // Act
        var result = await handler.Handle(
            new GetFreezePeriodChangesCountQuery(spaceId, groupId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.OverrideCount.Should().Be(0);
        result.ManualAssignmentCount.Should().Be(0);
        result.SwapCount.Should().Be(0);
        result.TotalCount.Should().Be(0);
    }

    /// <summary>
    /// Validates: Requirement 2.5
    /// When FreezeStartedAt is null (even if freeze flag is active), returns zeros.
    /// </summary>
    [Fact]
    public async Task ReturnsZeros_WhenFreezeStartedAtIsNull()
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var group = Group.Create(spaceId, null, "Test Group");
        var groupEntry = db.Groups.Add(group);
        groupEntry.Property("Id").CurrentValue = groupId;
        await db.SaveChangesAsync();

        // Create config with freeze active but then null out FreezeStartedAt
        var config = HomeLeaveConfig.Create(
            spaceId, groupId,
            minRestHours: 0,
            eligibilityThresholdHours: 168,
            leaveCapacity: 2,
            leaveDurationHours: 48);
        config.ActivateEmergencyFreeze(useForScheduling: false);

        var configEntry = db.HomeLeaveConfigs.Add(config);
        await db.SaveChangesAsync();

        // Force FreezeStartedAt to null while keeping EmergencyFreezeActive = true
        configEntry.Property("FreezeStartedAt").CurrentValue = null;
        await db.SaveChangesAsync();

        var handler = new GetFreezePeriodChangesCountQueryHandler(db);

        // Act
        var result = await handler.Handle(
            new GetFreezePeriodChangesCountQuery(spaceId, groupId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.OverrideCount.Should().Be(0);
        result.ManualAssignmentCount.Should().Be(0);
        result.SwapCount.Should().Be(0);
        result.TotalCount.Should().Be(0);
    }

    /// <summary>
    /// Validates: Requirements 2.1, 2.4
    /// Correctly counts override assignments created during the freeze period.
    /// </summary>
    [Fact]
    public async Task CorrectlyCountsOverrides_CreatedDuringFreezePeriod()
    {
        // Arrange
        var db = CreateDb();
        var freezeStart = DateTime.UtcNow.AddHours(-2);
        var (spaceId, groupId) = await SeedGroupWithFreeze(db, freezeActive: true, freezeStartedAt: freezeStart);
        var versionId = await SeedDraftVersion(db, spaceId);

        // 3 overrides created DURING freeze (after freezeStart)
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Override, freezeStart.AddMinutes(10));
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Override, freezeStart.AddMinutes(30));
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Override, freezeStart.AddMinutes(60));

        // 1 override created BEFORE freeze (should not be counted)
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Override, freezeStart.AddHours(-1));

        // 1 solver assignment during freeze (should not be counted as override)
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Solver, freezeStart.AddMinutes(15));

        var handler = new GetFreezePeriodChangesCountQueryHandler(db);

        // Act
        var result = await handler.Handle(
            new GetFreezePeriodChangesCountQuery(spaceId, groupId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert — 3 overrides counted (each on a unique slot, so no swaps)
        result.OverrideCount.Should().Be(3);
        result.TotalCount.Should().Be(3);
    }

    /// <summary>
    /// Validates: Requirements 2.2, 2.3
    /// Correctly categorizes manual assignments (with "Manual override" reason) and swaps
    /// (paired overrides on the same slot in the same version).
    /// </summary>
    [Fact]
    public async Task CorrectlyCategorizes_ManualAssignmentsAndSwaps()
    {
        // Arrange
        var db = CreateDb();
        var freezeStart = DateTime.UtcNow.AddHours(-2);
        var (spaceId, groupId) = await SeedGroupWithFreeze(db, freezeActive: true, freezeStartedAt: freezeStart);
        var versionId = await SeedDraftVersion(db, spaceId);

        // Manual assignment: override with "Manual override" in ChangeReasonSummary
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Override,
            freezeStart.AddMinutes(10), changeReasonSummary: "Manual override by admin");

        // Another manual assignment
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Override,
            freezeStart.AddMinutes(20), changeReasonSummary: "Manual override");

        // Swap: two overrides on the SAME slot in the same version
        var swapSlotId = Guid.NewGuid();
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Override,
            freezeStart.AddMinutes(30), taskSlotId: swapSlotId);
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Override,
            freezeStart.AddMinutes(31), taskSlotId: swapSlotId);

        // Plain override (no manual reason, unique slot)
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Override,
            freezeStart.AddMinutes(40));

        var handler = new GetFreezePeriodChangesCountQueryHandler(db);

        // Act
        var result = await handler.Handle(
            new GetFreezePeriodChangesCountQuery(spaceId, groupId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.ManualAssignmentCount.Should().Be(2, "two overrides have 'Manual override' in reason");
        result.SwapCount.Should().Be(1, "one slot has 2+ overrides = one swap");
        result.OverrideCount.Should().Be(1, "one plain override without manual reason and not part of swap");
        result.TotalCount.Should().Be(4, "2 manual + 1 swap + 1 override = 4");
    }

    /// <summary>
    /// Validates: Requirements 2.1, 7.2
    /// Query scopes to the correct space and only counts assignments in draft versions.
    /// Assignments in published versions or other spaces are excluded.
    /// </summary>
    [Fact]
    public async Task ScopesQuery_ToCorrectSpaceAndDraftVersionsOnly()
    {
        // Arrange
        var db = CreateDb();
        var freezeStart = DateTime.UtcNow.AddHours(-2);
        var (spaceId, groupId) = await SeedGroupWithFreeze(db, freezeActive: true, freezeStartedAt: freezeStart);

        // Draft version in the correct space
        var draftVersionId = await SeedDraftVersion(db, spaceId);

        // Published version in the correct space (should NOT be counted)
        var publishedVersionId = await SeedPublishedVersion(db, spaceId, versionNumber: 2);

        // Draft version in a DIFFERENT space (should NOT be counted)
        var otherSpaceId = Guid.NewGuid();
        var otherVersionId = await SeedDraftVersion(db, otherSpaceId, versionNumber: 1);

        // Override in correct space, draft version — should be counted
        await SeedAssignment(db, spaceId, draftVersionId, AssignmentSource.Override, freezeStart.AddMinutes(10));

        // Override in correct space, published version — should NOT be counted
        await SeedAssignment(db, spaceId, publishedVersionId, AssignmentSource.Override, freezeStart.AddMinutes(10));

        // Override in different space, draft version — should NOT be counted
        await SeedAssignment(db, otherSpaceId, otherVersionId, AssignmentSource.Override, freezeStart.AddMinutes(10));

        var handler = new GetFreezePeriodChangesCountQueryHandler(db);

        // Act
        var result = await handler.Handle(
            new GetFreezePeriodChangesCountQuery(spaceId, groupId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert — only the one assignment in the correct space's draft version
        result.OverrideCount.Should().Be(1);
        result.TotalCount.Should().Be(1);
    }

    /// <summary>
    /// Validates: Requirement 7.3
    /// When freeze is active but there are no override assignments during the period,
    /// returns zeros.
    /// </summary>
    [Fact]
    public async Task ReturnsZeros_WhenFreezeActiveButNoOverridesDuringPeriod()
    {
        // Arrange
        var db = CreateDb();
        var freezeStart = DateTime.UtcNow.AddHours(-1);
        var (spaceId, groupId) = await SeedGroupWithFreeze(db, freezeActive: true, freezeStartedAt: freezeStart);
        var versionId = await SeedDraftVersion(db, spaceId);

        // Only solver assignments during freeze (not overrides)
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Solver, freezeStart.AddMinutes(10));

        // Override created BEFORE freeze
        await SeedAssignment(db, spaceId, versionId, AssignmentSource.Override, freezeStart.AddHours(-2));

        var handler = new GetFreezePeriodChangesCountQueryHandler(db);

        // Act
        var result = await handler.Handle(
            new GetFreezePeriodChangesCountQuery(spaceId, groupId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.OverrideCount.Should().Be(0);
        result.ManualAssignmentCount.Should().Be(0);
        result.SwapCount.Should().Be(0);
        result.TotalCount.Should().Be(0);
    }

    /// <summary>
    /// Validates: Requirement 7.2
    /// Throws KeyNotFoundException when the group does not exist.
    /// </summary>
    [Fact]
    public async Task ThrowsKeyNotFoundException_WhenGroupDoesNotExist()
    {
        // Arrange
        var db = CreateDb();
        var handler = new GetFreezePeriodChangesCountQueryHandler(db);

        // Act
        var act = async () => await handler.Handle(
            new GetFreezePeriodChangesCountQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
