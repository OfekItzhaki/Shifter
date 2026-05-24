// Feature: schedule-regeneration
// Property 1: Published version immutability during regeneration lifecycle
// **Validates: Requirements 3.2, 3.3, 3.5, 5.4, 6.2**
//
// For any regeneration lifecycle event, the published version's status SHALL remain
// "Published" and assignment rows unchanged.

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

// ── Lifecycle event types ────────────────────────────────────────────────────

/// <summary>
/// Represents a regeneration lifecycle event that could potentially affect
/// the published version. Used as input for the property test.
/// </summary>
public abstract record RegenerationLifecycleEvent;

/// <summary>Run creation: a new regeneration run is queued.</summary>
public record RunCreationEvent(Guid RunId) : RegenerationLifecycleEvent;

/// <summary>Solver success: solver completes and a draft version is created.</summary>
public record SolverSuccessEvent(int AssignmentCount) : RegenerationLifecycleEvent;

/// <summary>Solver failure: solver fails (timeout, infeasibility, error).</summary>
public record SolverFailureEvent(string ErrorMessage) : RegenerationLifecycleEvent;

/// <summary>Draft discard: admin discards the regeneration draft.</summary>
public record DraftDiscardEvent() : RegenerationLifecycleEvent;

// ── Test input record ────────────────────────────────────────────────────────

/// <summary>
/// Input record for the property test representing a published version with
/// random assignments and a sequence of regeneration lifecycle events.
/// </summary>
public record PublishedVersionImmutabilityInput(
    int PublishedAssignmentCount,
    List<RegenerationLifecycleEvent> LifecycleEvents)
{
    public override string ToString() =>
        $"PublishedAssignments={PublishedAssignmentCount}, Events=[{string.Join(", ", LifecycleEvents.Select(e => e.GetType().Name))}]";
}

// ── Arbitraries ──────────────────────────────────────────────────────────────

/// <summary>
/// FsCheck arbitraries for generating random published version scenarios
/// and regeneration lifecycle event sequences.
/// </summary>
public static class PublishedVersionImmutabilityArbitraries
{
    public static Arbitrary<PublishedVersionImmutabilityInput> PublishedVersionImmutabilityInput()
    {
        var runCreationGen = Gen.Fresh(() => Guid.NewGuid())
            .Select(id => (RegenerationLifecycleEvent)new RunCreationEvent(id));

        var solverSuccessGen = Gen.Choose(1, 30)
            .Select(count => (RegenerationLifecycleEvent)new SolverSuccessEvent(count));

        var solverFailureGen = Gen.Elements(
                "Solver timed out after 120s",
                "Model is infeasible",
                "Internal solver error: memory limit exceeded",
                "No feasible solution found within constraints")
            .Select(msg => (RegenerationLifecycleEvent)new SolverFailureEvent(msg));

        var draftDiscardGen = Gen.Constant((RegenerationLifecycleEvent)new DraftDiscardEvent());

        var eventGen = Gen.OneOf(runCreationGen, solverSuccessGen, solverFailureGen, draftDiscardGen);

        var gen = from assignmentCount in Gen.Choose(1, 40)
                  from eventCount in Gen.Choose(1, 5)
                  from events in Gen.ListOf(eventCount, eventGen)
                  select new PublishedVersionImmutabilityInput(
                      assignmentCount,
                      events.ToList());

        return Arb.From(gen);
    }
}

// ── Property test class ──────────────────────────────────────────────────────

/// <summary>
/// Property-based test verifying that for any regeneration lifecycle event
/// (run creation, solver success, solver failure, draft discard), the published
/// version's status remains "Published" and its assignment rows remain identical
/// in count and content.
///
/// This validates the immutability guarantee: regeneration never mutates the
/// currently published version regardless of what happens during the lifecycle.
/// </summary>
public class PublishedVersionImmutabilityPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static void SetEntityId(object entity, Guid id)
    {
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(entity, id);
    }

    /// <summary>
    /// Seeds a space, group, and a published version with the specified number
    /// of random assignments. Returns all IDs and the assignment snapshot.
    /// </summary>
    private static (AppDbContext db, Guid spaceId, Guid groupId, Guid publishedVersionId, Guid userId, List<(Guid SlotId, Guid PersonId)> originalAssignments)
        SeedPublishedVersion(int assignmentCount)
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Create space
        var space = Space.Create("Test Space", userId);
        SetEntityId(space, spaceId);
        db.Spaces.Add(space);

        // Create group
        var group = Group.Create(spaceId, null, "Test Group");
        SetEntityId(group, groupId);
        db.Groups.Add(group);

        // Create published version
        var publishedVersion = ScheduleVersion.CreateDraft(
            spaceId, 1, null, null, userId, null);
        publishedVersion.Publish(userId);
        db.ScheduleVersions.Add(publishedVersion);
        db.SaveChanges();

        // Create assignments for the published version
        var originalAssignments = new List<(Guid SlotId, Guid PersonId)>();
        for (int i = 0; i < assignmentCount; i++)
        {
            var slotId = Guid.NewGuid();
            var personId = Guid.NewGuid();
            originalAssignments.Add((slotId, personId));

            var assignment = Assignment.Create(
                spaceId, publishedVersion.Id, slotId, personId, AssignmentSource.Solver);
            db.Assignments.Add(assignment);
        }

        db.SaveChanges();

        return (db, spaceId, groupId, publishedVersion.Id, userId, originalAssignments);
    }

    /// <summary>
    /// Applies a regeneration lifecycle event to the database, simulating
    /// the system's behavior during each phase of the regeneration lifecycle.
    /// </summary>
    private static void ApplyLifecycleEvent(
        AppDbContext db,
        Guid spaceId,
        Guid groupId,
        Guid publishedVersionId,
        Guid userId,
        RegenerationLifecycleEvent lifecycleEvent)
    {
        switch (lifecycleEvent)
        {
            case RunCreationEvent runCreation:
                // Simulate: a new regeneration run is created and queued
                var run = ScheduleRun.Create(
                    spaceId, ScheduleRunTrigger.Regeneration,
                    publishedVersionId, userId, groupId);
                SetEntityId(run, runCreation.RunId);
                db.ScheduleRuns.Add(run);
                db.SaveChanges();
                break;

            case SolverSuccessEvent solverSuccess:
                // Simulate: solver completes, worker creates a draft version with assignments
                var successRun = ScheduleRun.Create(
                    spaceId, ScheduleRunTrigger.Regeneration,
                    publishedVersionId, userId, groupId);
                db.ScheduleRuns.Add(successRun);
                db.SaveChanges();

                successRun.MarkRunning("test-hash");
                db.SaveChanges();

                // Create regeneration draft version
                var nextVersionNum = db.ScheduleVersions
                    .Where(v => v.SpaceId == spaceId)
                    .Max(v => v.VersionNumber) + 1;

                var draftVersion = ScheduleVersion.CreateRegenerationDraft(
                    spaceId, nextVersionNum, successRun.Id,
                    publishedVersionId, userId, null);
                db.ScheduleVersions.Add(draftVersion);
                db.SaveChanges();

                // Insert draft assignments (different from published assignments)
                for (int i = 0; i < solverSuccess.AssignmentCount; i++)
                {
                    var draftAssignment = Assignment.Create(
                        spaceId, draftVersion.Id,
                        Guid.NewGuid(), Guid.NewGuid(), AssignmentSource.Solver);
                    db.Assignments.Add(draftAssignment);
                }

                successRun.SetResultVersion(draftVersion.Id);
                successRun.MarkCompleted("{}");
                db.SaveChanges();
                break;

            case SolverFailureEvent solverFailure:
                // Simulate: solver fails, run is marked failed, no version created
                var failedRun = ScheduleRun.Create(
                    spaceId, ScheduleRunTrigger.Regeneration,
                    publishedVersionId, userId, groupId);
                db.ScheduleRuns.Add(failedRun);
                db.SaveChanges();

                failedRun.MarkRunning("test-hash");
                db.SaveChanges();

                failedRun.MarkFailed(solverFailure.ErrorMessage);
                db.SaveChanges();
                break;

            case DraftDiscardEvent:
                // Simulate: admin discards a regeneration draft
                // First, create a draft to discard (if one doesn't exist)
                var discardRun = ScheduleRun.Create(
                    spaceId, ScheduleRunTrigger.Regeneration,
                    publishedVersionId, userId, groupId);
                db.ScheduleRuns.Add(discardRun);
                db.SaveChanges();

                var discardVersionNum = db.ScheduleVersions
                    .Where(v => v.SpaceId == spaceId)
                    .Max(v => v.VersionNumber) + 1;

                var discardDraft = ScheduleVersion.CreateRegenerationDraft(
                    spaceId, discardVersionNum, discardRun.Id,
                    publishedVersionId, userId, null);
                db.ScheduleVersions.Add(discardDraft);
                db.SaveChanges();

                // Discard the draft (same as DiscardVersionCommand does)
                discardDraft.Discard();
                db.SaveChanges();
                break;
        }
    }

    /// <summary>
    /// Verifies that the published version's status and assignments are unchanged.
    /// Returns true if the invariant holds.
    /// </summary>
    private static bool VerifyPublishedVersionImmutability(
        AppDbContext db,
        Guid publishedVersionId,
        List<(Guid SlotId, Guid PersonId)> originalAssignments)
    {
        // Reload the published version from the database
        var publishedVersion = db.ScheduleVersions.Find(publishedVersionId);
        if (publishedVersion == null)
            return false;

        // Status must still be Published
        if (publishedVersion.Status != ScheduleVersionStatus.Published)
            return false;

        // Assignment count must be unchanged
        var currentAssignments = db.Assignments
            .Where(a => a.ScheduleVersionId == publishedVersionId)
            .Select(a => new { a.TaskSlotId, a.PersonId })
            .ToList();

        if (currentAssignments.Count != originalAssignments.Count)
            return false;

        // Assignment content must be unchanged (same slot-person pairs)
        var originalSet = originalAssignments
            .Select(a => (a.SlotId, a.PersonId))
            .ToHashSet();

        var currentSet = currentAssignments
            .Select(a => (a.TaskSlotId, a.PersonId))
            .ToHashSet();

        return originalSet.SetEquals(currentSet);
    }

    // ── Property 1: Published version immutability during regeneration lifecycle ──
    // Feature: schedule-regeneration, Property 1
    // **Validates: Requirements 3.2, 3.3, 3.5, 5.4, 6.2**

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PublishedVersionImmutabilityArbitraries) })]
    public bool PublishedVersion_RemainsImmutable_ThroughoutRegenerationLifecycle(
        PublishedVersionImmutabilityInput input)
    {
        // Arrange — seed a published version with random assignments
        var (db, spaceId, groupId, publishedVersionId, userId, originalAssignments) =
            SeedPublishedVersion(input.PublishedAssignmentCount);

        // Act & Assert — apply each lifecycle event and verify immutability after each
        foreach (var lifecycleEvent in input.LifecycleEvents)
        {
            ApplyLifecycleEvent(db, spaceId, groupId, publishedVersionId, userId, lifecycleEvent);

            // After each event, the published version must remain unchanged
            if (!VerifyPublishedVersionImmutability(db, publishedVersionId, originalAssignments))
                return false;
        }

        return true;
    }

    // ── Deterministic examples ────────────────────────────────────────────────

    [Fact]
    public void PublishedVersion_UnchangedAfterRunCreation()
    {
        // Arrange
        var (db, spaceId, groupId, publishedVersionId, userId, originalAssignments) =
            SeedPublishedVersion(10);

        // Act — create a regeneration run
        ApplyLifecycleEvent(db, spaceId, groupId, publishedVersionId, userId,
            new RunCreationEvent(Guid.NewGuid()));

        // Assert
        var published = db.ScheduleVersions.Find(publishedVersionId)!;
        published.Status.Should().Be(ScheduleVersionStatus.Published);

        var assignments = db.Assignments
            .Where(a => a.ScheduleVersionId == publishedVersionId)
            .ToList();
        assignments.Should().HaveCount(10);
    }

    [Fact]
    public void PublishedVersion_UnchangedAfterSolverSuccess()
    {
        // Arrange
        var (db, spaceId, groupId, publishedVersionId, userId, originalAssignments) =
            SeedPublishedVersion(15);

        // Act — solver succeeds and creates a draft with different assignments
        ApplyLifecycleEvent(db, spaceId, groupId, publishedVersionId, userId,
            new SolverSuccessEvent(20));

        // Assert — published version unchanged
        var published = db.ScheduleVersions.Find(publishedVersionId)!;
        published.Status.Should().Be(ScheduleVersionStatus.Published);

        var publishedAssignments = db.Assignments
            .Where(a => a.ScheduleVersionId == publishedVersionId)
            .ToList();
        publishedAssignments.Should().HaveCount(15);

        // Draft version exists separately
        var drafts = db.ScheduleVersions
            .Where(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Draft)
            .ToList();
        drafts.Should().HaveCount(1);
        drafts.Single().SupersedesVersionId.Should().Be(publishedVersionId);
    }

    [Fact]
    public void PublishedVersion_UnchangedAfterSolverFailure()
    {
        // Arrange
        var (db, spaceId, groupId, publishedVersionId, userId, originalAssignments) =
            SeedPublishedVersion(8);

        // Act — solver fails
        ApplyLifecycleEvent(db, spaceId, groupId, publishedVersionId, userId,
            new SolverFailureEvent("Solver timed out after 120s"));

        // Assert — published version unchanged, no new versions created
        var published = db.ScheduleVersions.Find(publishedVersionId)!;
        published.Status.Should().Be(ScheduleVersionStatus.Published);

        var publishedAssignments = db.Assignments
            .Where(a => a.ScheduleVersionId == publishedVersionId)
            .ToList();
        publishedAssignments.Should().HaveCount(8);

        // No draft versions should exist
        var drafts = db.ScheduleVersions
            .Where(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Draft)
            .ToList();
        drafts.Should().BeEmpty();
    }

    [Fact]
    public void PublishedVersion_UnchangedAfterDraftDiscard()
    {
        // Arrange
        var (db, spaceId, groupId, publishedVersionId, userId, originalAssignments) =
            SeedPublishedVersion(12);

        // Act — create and discard a regeneration draft
        ApplyLifecycleEvent(db, spaceId, groupId, publishedVersionId, userId,
            new DraftDiscardEvent());

        // Assert — published version unchanged
        var published = db.ScheduleVersions.Find(publishedVersionId)!;
        published.Status.Should().Be(ScheduleVersionStatus.Published);

        var publishedAssignments = db.Assignments
            .Where(a => a.ScheduleVersionId == publishedVersionId)
            .ToList();
        publishedAssignments.Should().HaveCount(12);
    }

    [Fact]
    public void PublishedVersion_UnchangedAfterFullLifecycle_SuccessThenDiscard()
    {
        // Arrange — full lifecycle: run creation → solver success → draft discard
        var (db, spaceId, groupId, publishedVersionId, userId, originalAssignments) =
            SeedPublishedVersion(20);

        // Act — apply full lifecycle sequence
        ApplyLifecycleEvent(db, spaceId, groupId, publishedVersionId, userId,
            new RunCreationEvent(Guid.NewGuid()));
        VerifyPublishedVersionImmutability(db, publishedVersionId, originalAssignments).Should().BeTrue();

        ApplyLifecycleEvent(db, spaceId, groupId, publishedVersionId, userId,
            new SolverSuccessEvent(25));
        VerifyPublishedVersionImmutability(db, publishedVersionId, originalAssignments).Should().BeTrue();

        ApplyLifecycleEvent(db, spaceId, groupId, publishedVersionId, userId,
            new DraftDiscardEvent());
        VerifyPublishedVersionImmutability(db, publishedVersionId, originalAssignments).Should().BeTrue();

        // Assert — final state
        var published = db.ScheduleVersions.Find(publishedVersionId)!;
        published.Status.Should().Be(ScheduleVersionStatus.Published);

        var publishedAssignments = db.Assignments
            .Where(a => a.ScheduleVersionId == publishedVersionId)
            .ToList();
        publishedAssignments.Should().HaveCount(20);
    }

    [Fact]
    public void PublishedVersion_UnchangedAfterMultipleFailures()
    {
        // Arrange — multiple solver failures in sequence
        var (db, spaceId, groupId, publishedVersionId, userId, originalAssignments) =
            SeedPublishedVersion(5);

        // Act — multiple failures
        ApplyLifecycleEvent(db, spaceId, groupId, publishedVersionId, userId,
            new SolverFailureEvent("Model is infeasible"));
        ApplyLifecycleEvent(db, spaceId, groupId, publishedVersionId, userId,
            new SolverFailureEvent("Solver timed out after 120s"));
        ApplyLifecycleEvent(db, spaceId, groupId, publishedVersionId, userId,
            new SolverFailureEvent("Internal solver error: memory limit exceeded"));

        // Assert — published version unchanged after all failures
        var published = db.ScheduleVersions.Find(publishedVersionId)!;
        published.Status.Should().Be(ScheduleVersionStatus.Published);

        var publishedAssignments = db.Assignments
            .Where(a => a.ScheduleVersionId == publishedVersionId)
            .ToList();
        publishedAssignments.Should().HaveCount(5);

        // Verify original assignment content is preserved
        var currentSlotPersonPairs = publishedAssignments
            .Select(a => (a.TaskSlotId, a.PersonId))
            .ToHashSet();
        var originalSet = originalAssignments
            .Select(a => (a.SlotId, a.PersonId))
            .ToHashSet();
        currentSlotPersonPairs.Should().BeEquivalentTo(originalSet);
    }
}
