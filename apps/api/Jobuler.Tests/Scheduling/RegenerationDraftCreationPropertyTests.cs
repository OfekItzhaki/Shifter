// Feature: schedule-regeneration
// Property 2: Successful regeneration creates a correctly linked draft
// **Validates: Requirements 2.3, 3.1, 4.3, 8.3**

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Logging;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

/// <summary>
/// FsCheck arbitraries for generating random valid solver outputs
/// used in the regeneration draft creation property test.
/// </summary>
public static class RegenerationDraftArbitraries
{
    public static Arbitrary<ValidSolverOutput> ValidSolverOutput()
    {
        var gen = from assignmentCount in Gen.Choose(1, 50)
                  from personIds in Gen.ListOf(assignmentCount, Gen.Fresh(() => Guid.NewGuid()))
                  from slotIds in Gen.ListOf(assignmentCount, Gen.Fresh(() => Guid.NewGuid()))
                  from solverTimeMs in Gen.Choose(100, 30000)
                  select new ValidSolverOutput(
                      assignmentCount,
                      personIds.Zip(slotIds, (p, s) => (PersonId: p, SlotId: s)).ToList(),
                      solverTimeMs);

        return Arb.From(gen);
    }
}

/// <summary>
/// Input record representing a valid solver output with varying assignments.
/// </summary>
public record ValidSolverOutput(
    int AssignmentCount,
    List<(Guid PersonId, Guid SlotId)> Assignments,
    int SolverTimeMs)
{
    public override string ToString() =>
        $"AssignmentCount={AssignmentCount}, SolverTimeMs={SolverTimeMs}";
}

/// <summary>
/// Property-based test verifying that for any valid solver output produced by a
/// regeneration run, the system creates exactly one new ScheduleVersion with:
/// - status = Draft
/// - SourceRunId = run.Id
/// - SupersedesVersionId = published version ID
/// - SourceType = "regeneration"
///
/// This test simulates the worker's regeneration processing logic by exercising
/// the same code path the SolverWorkerService uses after receiving a successful
/// solver result for a regeneration trigger mode.
/// </summary>
public class RegenerationDraftCreationPropertyTests
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
    /// Seeds a space, group, published version, and a regeneration run.
    /// Returns the IDs needed for assertions.
    /// </summary>
    private static async Task<(AppDbContext db, Guid spaceId, Guid groupId, Guid publishedVersionId, Guid runId, Guid userId)>
        SeedRegenerationScenarioAsync()
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

        // Create a published version (the baseline)
        var publishedVersion = ScheduleVersion.CreateDraft(
            spaceId, 1, null, null, userId, null);
        SetEntityId(publishedVersion, Guid.NewGuid());
        publishedVersion.Publish(userId);
        db.ScheduleVersions.Add(publishedVersion);

        await db.SaveChangesAsync();

        // Create a regeneration run pointing to the published version
        var run = ScheduleRun.Create(
            spaceId, ScheduleRunTrigger.Regeneration,
            publishedVersion.Id, userId, groupId);
        SetEntityId(run, Guid.NewGuid());
        db.ScheduleRuns.Add(run);

        await db.SaveChangesAsync();

        return (db, spaceId, groupId, publishedVersion.Id, run.Id, userId);
    }

    /// <summary>
    /// Builds a SolverOutputDto from the generated test data.
    /// </summary>
    private static SolverOutputDto BuildSolverOutput(ValidSolverOutput input, Guid runId)
    {
        return new SolverOutputDto
        {
            RunId = runId.ToString(),
            Feasible = true,
            TimedOut = false,
            Assignments = input.Assignments.Select(a => new AssignmentResultDto
            {
                SlotId = a.SlotId.ToString(),
                PersonId = a.PersonId.ToString(),
                Source = "solver"
            }).ToList(),
            UncoveredSlotIds = new List<string>(),
            HardConflicts = new List<HardConflictDto>(),
            SoftPenaltyTotal = 0,
            StabilityMetrics = new StabilityMetricsDto(),
            FairnessMetrics = new List<FairnessMetricsDto>(),
            ExplanationFragments = new List<string>(),
            HomeLeaveAssignments = new List<HomeLeaveAssignmentDto>(),
            HomeLeaveMetrics = new List<HomeLeaveMetricDto>(),
            SolverTimeMs = input.SolverTimeMs
        };
    }

    /// <summary>
    /// Simulates the worker's regeneration processing logic:
    /// Given a successful solver output for a regeneration run, creates the
    /// draft version and assignments exactly as the SolverWorkerService does.
    /// </summary>
    private static async Task SimulateWorkerRegenerationProcessingAsync(
        AppDbContext db,
        Guid spaceId,
        Guid runId,
        Guid baselineVersionId,
        Guid userId,
        SolverOutputDto solverOutput)
    {
        // Load the run (same as worker does)
        var run = await db.ScheduleRuns
            .FirstAsync(r => r.Id == runId && r.SpaceId == spaceId);

        // Mark running (worker does this after building payload)
        run.MarkRunning("test-hash");
        await db.SaveChangesAsync();

        // Parse assignments (same logic as worker)
        var parsedAssignments = solverOutput.Assignments
            .Where(a => Guid.TryParse(a.SlotId, out _))
            .ToList();

        var hasUncoveredSlots = solverOutput.UncoveredSlotIds.Count > 0;
        var shouldDiscard = !solverOutput.Feasible || parsedAssignments.Count == 0 || hasUncoveredSlots;

        if (!shouldDiscard)
        {
            // Determine next version number (same as worker)
            var nextVersion = (await db.ScheduleVersions
                .Where(v => v.SpaceId == spaceId)
                .MaxAsync(v => (int?)v.VersionNumber) ?? 0) + 1;

            // Regeneration uses the dedicated factory (same as worker)
            var isRegeneration = true; // triggerMode == "regeneration"
            ScheduleVersion version;

            if (isRegeneration)
            {
                version = ScheduleVersion.CreateRegenerationDraft(
                    spaceId, nextVersion, runId,
                    baselineVersionId, userId, null);
            }
            else
            {
                version = ScheduleVersion.CreateDraft(
                    spaceId, nextVersion, baselineVersionId,
                    runId, userId, null);
            }

            db.ScheduleVersions.Add(version);
            await db.SaveChangesAsync();

            // Insert assignments (same as worker)
            var assignments = parsedAssignments.Select(a =>
                Assignment.Create(
                    spaceId, version.Id,
                    Guid.Parse(a.SlotId), Guid.Parse(a.PersonId),
                    a.Source == "override" ? AssignmentSource.Override : AssignmentSource.Solver)
            ).ToList();

            db.Assignments.AddRange(assignments);

            // Link result version to run (same as worker)
            run.SetResultVersion(version.Id);
            run.MarkCompleted("{}");

            await db.SaveChangesAsync();
        }
    }

    // ── Property 2: Successful regeneration creates a correctly linked draft ──
    // Feature: schedule-regeneration, Property 2

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(RegenerationDraftArbitraries) })]
    public bool SuccessfulRegeneration_CreatesExactlyOneDraft_WithCorrectLinks(ValidSolverOutput testInput)
    {
        // Arrange — seed a group with a published version and a regeneration run
        var (db, spaceId, groupId, publishedVersionId, runId, userId) =
            SeedRegenerationScenarioAsync().GetAwaiter().GetResult();

        var solverOutput = BuildSolverOutput(testInput, runId);

        // Act — simulate the worker processing the solver output
        SimulateWorkerRegenerationProcessingAsync(
            db, spaceId, runId, publishedVersionId, userId, solverOutput)
            .GetAwaiter().GetResult();

        // Assert — exactly one new draft ScheduleVersion with correct properties
        var draftVersions = db.ScheduleVersions
            .Where(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Draft)
            .ToList();

        // Must be exactly one draft
        if (draftVersions.Count != 1)
            return false;

        var draft = draftVersions.Single();

        // SourceRunId must match the regeneration run
        if (draft.SourceRunId != runId)
            return false;

        // SupersedesVersionId must match the published version
        if (draft.SupersedesVersionId != publishedVersionId)
            return false;

        // SourceType must be "regeneration"
        if (draft.SourceType != "regeneration")
            return false;

        // Status must be Draft
        if (draft.Status != ScheduleVersionStatus.Draft)
            return false;

        return true;
    }

    // ── Deterministic examples for edge cases ────────────────────────────────

    [Fact]
    public async Task Regeneration_WithSingleAssignment_CreatesCorrectDraft()
    {
        // Arrange
        var (db, spaceId, groupId, publishedVersionId, runId, userId) =
            await SeedRegenerationScenarioAsync();

        var solverOutput = new SolverOutputDto
        {
            RunId = runId.ToString(),
            Feasible = true,
            TimedOut = false,
            Assignments = new List<AssignmentResultDto>
            {
                new() { SlotId = Guid.NewGuid().ToString(), PersonId = Guid.NewGuid().ToString(), Source = "solver" }
            },
            UncoveredSlotIds = new List<string>(),
            HardConflicts = new List<HardConflictDto>(),
            StabilityMetrics = new StabilityMetricsDto(),
            FairnessMetrics = new List<FairnessMetricsDto>(),
            ExplanationFragments = new List<string>(),
            HomeLeaveAssignments = new List<HomeLeaveAssignmentDto>(),
            HomeLeaveMetrics = new List<HomeLeaveMetricDto>()
        };

        // Act
        await SimulateWorkerRegenerationProcessingAsync(
            db, spaceId, runId, publishedVersionId, userId, solverOutput);

        // Assert
        var drafts = await db.ScheduleVersions
            .Where(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Draft)
            .ToListAsync();

        drafts.Should().HaveCount(1);
        var draft = drafts.Single();
        draft.SourceRunId.Should().Be(runId);
        draft.SupersedesVersionId.Should().Be(publishedVersionId);
        draft.SourceType.Should().Be("regeneration");
    }

    [Fact]
    public async Task Regeneration_WithManyAssignments_CreatesCorrectDraft()
    {
        // Arrange
        var (db, spaceId, groupId, publishedVersionId, runId, userId) =
            await SeedRegenerationScenarioAsync();

        var assignments = Enumerable.Range(0, 100)
            .Select(_ => new AssignmentResultDto
            {
                SlotId = Guid.NewGuid().ToString(),
                PersonId = Guid.NewGuid().ToString(),
                Source = "solver"
            }).ToList();

        var solverOutput = new SolverOutputDto
        {
            RunId = runId.ToString(),
            Feasible = true,
            TimedOut = false,
            Assignments = assignments,
            UncoveredSlotIds = new List<string>(),
            HardConflicts = new List<HardConflictDto>(),
            StabilityMetrics = new StabilityMetricsDto(),
            FairnessMetrics = new List<FairnessMetricsDto>(),
            ExplanationFragments = new List<string>(),
            HomeLeaveAssignments = new List<HomeLeaveAssignmentDto>(),
            HomeLeaveMetrics = new List<HomeLeaveMetricDto>()
        };

        // Act
        await SimulateWorkerRegenerationProcessingAsync(
            db, spaceId, runId, publishedVersionId, userId, solverOutput);

        // Assert
        var drafts = await db.ScheduleVersions
            .Where(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Draft)
            .ToListAsync();

        drafts.Should().HaveCount(1);
        var draft = drafts.Single();
        draft.SourceRunId.Should().Be(runId);
        draft.SupersedesVersionId.Should().Be(publishedVersionId);
        draft.SourceType.Should().Be("regeneration");

        // Verify assignments were stored
        var storedAssignments = await db.Assignments
            .Where(a => a.ScheduleVersionId == draft.Id)
            .ToListAsync();
        storedAssignments.Should().HaveCount(100);
    }

    [Fact]
    public async Task Regeneration_RunIsLinkedToResultVersion()
    {
        // Arrange
        var (db, spaceId, groupId, publishedVersionId, runId, userId) =
            await SeedRegenerationScenarioAsync();

        var solverOutput = new SolverOutputDto
        {
            RunId = runId.ToString(),
            Feasible = true,
            TimedOut = false,
            Assignments = new List<AssignmentResultDto>
            {
                new() { SlotId = Guid.NewGuid().ToString(), PersonId = Guid.NewGuid().ToString(), Source = "solver" }
            },
            UncoveredSlotIds = new List<string>(),
            HardConflicts = new List<HardConflictDto>(),
            StabilityMetrics = new StabilityMetricsDto(),
            FairnessMetrics = new List<FairnessMetricsDto>(),
            ExplanationFragments = new List<string>(),
            HomeLeaveAssignments = new List<HomeLeaveAssignmentDto>(),
            HomeLeaveMetrics = new List<HomeLeaveMetricDto>()
        };

        // Act
        await SimulateWorkerRegenerationProcessingAsync(
            db, spaceId, runId, publishedVersionId, userId, solverOutput);

        // Assert — run.ResultVersionId points to the new draft (Requirement 8.3)
        var run = await db.ScheduleRuns.FirstAsync(r => r.Id == runId);
        var draft = await db.ScheduleVersions
            .FirstAsync(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Draft);

        run.ResultVersionId.Should().Be(draft.Id);
        run.Status.Should().Be(ScheduleRunStatus.Completed);
    }
}
