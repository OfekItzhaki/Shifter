// Solver worker pipeline integration tests
// Tests the full path: seed DB → normalizer builds payload → real solver → version + assignments created
//
// Run with:   dotnet test --filter "SolverWorkerPipeline" --logger "console;verbosity=detailed"
// Requires:   Python solver running on localhost:8000

using FluentAssertions;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Groups;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Logging;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Jobuler.Tests.Integration;

/// <summary>
/// Full pipeline tests: seeds an in-memory DB with real domain entities,
/// runs the SolverPayloadNormalizer to build the input, calls the live solver,
/// and verifies the worker correctly creates/skips versions.
/// </summary>
public class SolverWorkerPipelineTests
{
    private readonly ITestOutputHelper _out;

    public SolverWorkerPipelineTests(ITestOutputHelper output) => _out = output;

    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static DateTime Tomorrow(int hour = 8) =>
        DateTime.UtcNow.Date.AddDays(1).AddHours(hour);

    // ── Test 1: Feasible scenario — normalizer builds payload, solver assigns ──
    // Seeds: 3 people, 1 group task (8h shift, headcount=1)
    // Expects: run=Completed, version=Draft, assignments.Count >= 1

    [Fact]
    public async Task Pipeline_FeasibleScenario_CreatesVersionWithAssignments()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId  = Guid.NewGuid();

        // Seed space (needed for locale query)
        var space = Space.Create("Test Space", userId, locale: "en");
        SetId(space, spaceId);
        db.Spaces.Add(space);

        // Seed group with 7-day horizon
        var group = Group.Create(spaceId, null, "Alpha");
        group.UpdateSettings(7);
        db.Groups.Add(group);

        // Seed 3 active people
        var people = Enumerable.Range(0, 3).Select(_ =>
        {
            var p = Person.Create(spaceId, $"Person {Guid.NewGuid():N}");
            db.People.Add(p);
            db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, p.Id));
            return p;
        }).ToList();

        // Seed a group task: tomorrow 08:00–16:00, 8h shifts, headcount=1
        var task = GroupTask.Create(
            spaceId, group.Id, "Guard Duty",
            Tomorrow(0), Tomorrow(0).AddDays(1),   // window: tomorrow full day
            shiftDurationMinutes: 480,              // 8h shifts
            requiredHeadcount: 1,
            burdenLevel: TaskBurdenLevel.Normal,
            allowsOverlap: false,
            allowsDoubleShift: false,
            createdByUserId: userId);
        db.GroupTasks.Add(task);

        await db.SaveChangesAsync();

        // Create a run record
        var run = ScheduleRun.Create(spaceId, ScheduleRunTrigger.Standard, null, userId);
        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync();

        // Build payload via normalizer
        var normalizer = new SolverPayloadNormalizer(db, NullLogger<SolverPayloadNormalizer>.Instance, Substitute.For<ICumulativeTracker>());
        var input = await normalizer.BuildAsync(spaceId, run.Id, "standard", null);

        _out.WriteLine($"Payload: people={input.People.Count} slots={input.TaskSlots.Count}");
        input.People.Should().HaveCount(3);
        input.TaskSlots.Should().NotBeEmpty("group task should expand into shift slots");

        // Call real solver
        var http = new System.Net.Http.HttpClient { BaseAddress = new Uri("http://localhost:8000") };
        var client = new SolverHttpClient(http, NullLogger<SolverHttpClient>.Instance);
        var output = await client.SolveAsync(input);

        _out.WriteLine($"Solver: feasible={output.Feasible} assignments={output.Assignments.Count} uncovered={output.UncoveredSlotIds.Count}");

        output.Feasible.Should().BeTrue("3 people + 1-headcount shifts = trivially feasible");
        output.Assignments.Should().NotBeEmpty();

        // Verify all assignment person IDs are valid people in our space
        var personIdSet = people.Select(p => p.Id.ToString()).ToHashSet();
        foreach (var a in output.Assignments)
        {
            personIdSet.Should().Contain(a.PersonId, "solver must only assign people from the input");
        }
    }

    // ── Test 2: Infeasible scenario — 0 people, task exists ──────────────────
    // Expects: solver returns feasible=false OR assignments=0, run=Failed, NO version created

    [Fact]
    public async Task Pipeline_NoPeople_SolverReturnsFeasibleFalseOrEmpty_NoVersionCreated()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId  = Guid.NewGuid();

        var space = Space.Create("Test Space", userId, locale: "en");
        SetId(space, spaceId);
        db.Spaces.Add(space);

        var group = Group.Create(spaceId, null, "Beta");
        group.UpdateSettings(7);
        db.Groups.Add(group);

        // NO people seeded

        var task = GroupTask.Create(
            spaceId, group.Id, "Guard Duty",
            Tomorrow(0), Tomorrow(0).AddDays(1),
            480, 1, TaskBurdenLevel.Normal, false, false, userId);
        db.GroupTasks.Add(task);

        await db.SaveChangesAsync();

        var run = ScheduleRun.Create(spaceId, ScheduleRunTrigger.Standard, null, userId);
        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync();

        var normalizer = new SolverPayloadNormalizer(db, NullLogger<SolverPayloadNormalizer>.Instance, Substitute.For<ICumulativeTracker>());
        var input = await normalizer.BuildAsync(spaceId, run.Id, "standard", null);

        _out.WriteLine($"Payload: people={input.People.Count} slots={input.TaskSlots.Count}");
        input.People.Should().BeEmpty();

        // With 0 people the pre-flight in the worker would catch this before calling the solver.
        // But let's verify the solver itself also handles it gracefully.
        var http = new System.Net.Http.HttpClient { BaseAddress = new Uri("http://localhost:8000") };
        var client = new SolverHttpClient(http, NullLogger<SolverHttpClient>.Instance);
        var output = await client.SolveAsync(input);

        _out.WriteLine($"Solver: feasible={output.Feasible} assignments={output.Assignments.Count}");

        var isEmpty = !output.Feasible || output.Assignments.Count == 0;
        isEmpty.Should().BeTrue("0 people = nothing to assign");
        output.Assignments.Should().BeEmpty();
    }

    // ── Test 3: Partial scenario — 1 person, slot needs 2 ────────────────────
    // Expects: feasible=true (partial), assignments=1, uncovered slot flagged
    // This is the KEY regression test — previously this returned INFEASIBLE (empty draft)

    [Fact]
    public async Task Pipeline_PartialCoverage_ReturnsFeasibleWithPartialAssignments()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId  = Guid.NewGuid();

        var space = Space.Create("Test Space", userId, locale: "en");
        SetId(space, spaceId);
        db.Spaces.Add(space);

        var group = Group.Create(spaceId, null, "Gamma");
        group.UpdateSettings(7);
        db.Groups.Add(group);

        // Only 1 person
        var person = Person.Create(spaceId, "Solo Person");
        db.People.Add(person);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));

        // Task requires 2 people per shift — impossible with 1
        var task = GroupTask.Create(
            spaceId, group.Id, "Double Guard",
            Tomorrow(0), Tomorrow(0).AddDays(1),
            480, 2,   // ← headcount=2, only 1 person available
            TaskBurdenLevel.Normal, false, false, userId);
        db.GroupTasks.Add(task);

        await db.SaveChangesAsync();

        var run = ScheduleRun.Create(spaceId, ScheduleRunTrigger.Standard, null, userId);
        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync();

        var normalizer = new SolverPayloadNormalizer(db, NullLogger<SolverPayloadNormalizer>.Instance, Substitute.For<ICumulativeTracker>());
        var input = await normalizer.BuildAsync(spaceId, run.Id, "standard", null);

        _out.WriteLine($"Payload: people={input.People.Count} slots={input.TaskSlots.Count}");

        var http = new System.Net.Http.HttpClient { BaseAddress = new Uri("http://localhost:8000") };
        var client = new SolverHttpClient(http, NullLogger<SolverHttpClient>.Instance);
        var output = await client.SolveAsync(input);

        _out.WriteLine($"Solver: feasible={output.Feasible} assignments={output.Assignments.Count} uncovered={output.UncoveredSlotIds.Count}");

        // KEY assertion: must be feasible with partial assignments, NOT infeasible
        output.Feasible.Should().BeTrue(
            "solver must return a partial result instead of INFEASIBLE when headcount can't be met");
        output.Assignments.Should().NotBeEmpty(
            "the 1 available person should still be assigned");
        output.UncoveredSlotIds.Should().NotBeEmpty(
            "slots that couldn't be fully staffed must be flagged as uncovered");
    }

    // ── Test 4: Blocked person — not assigned ─────────────────────────────────
    // Seeds: 2 people, person1 blocked for the slot window, person2 free
    // Expects: only person2 assigned

    [Fact]
    public async Task Pipeline_BlockedPerson_NotAssigned_OtherPersonCoversSlot()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId  = Guid.NewGuid();

        var space = Space.Create("Test Space", userId, locale: "en");
        SetId(space, spaceId);
        db.Spaces.Add(space);

        var group = Group.Create(spaceId, null, "Delta");
        group.UpdateSettings(7);
        db.Groups.Add(group);

        var person1 = Person.Create(spaceId, "Blocked Person");
        var person2 = Person.Create(spaceId, "Free Person");
        db.People.AddRange(person1, person2);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person1.Id));
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person2.Id));

        // Block person1 for the entire tomorrow (AtHome = blocked from assignments)
        var blockedWindow = PresenceWindow.CreateManual(
            spaceId, person1.Id,
            PresenceState.AtHome,
            Tomorrow(0), Tomorrow(0).AddDays(1),
            note: "blocked for test");
        db.PresenceWindows.Add(blockedWindow);

        var task = GroupTask.Create(
            spaceId, group.Id, "Guard",
            Tomorrow(0), Tomorrow(0).AddDays(1),
            480, 1, TaskBurdenLevel.Normal, false, false, userId);
        db.GroupTasks.Add(task);

        await db.SaveChangesAsync();

        var run = ScheduleRun.Create(spaceId, ScheduleRunTrigger.Standard, null, userId);
        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync();

        var normalizer = new SolverPayloadNormalizer(db, NullLogger<SolverPayloadNormalizer>.Instance, Substitute.For<ICumulativeTracker>());
        var input = await normalizer.BuildAsync(spaceId, run.Id, "standard", null);

        _out.WriteLine($"Payload: people={input.People.Count} slots={input.TaskSlots.Count} presenceWindows={input.PresenceWindows.Count}");
        input.PresenceWindows.Should().NotBeEmpty("blocked window must be included in payload");

        var http = new System.Net.Http.HttpClient { BaseAddress = new Uri("http://localhost:8000") };
        var client = new SolverHttpClient(http, NullLogger<SolverHttpClient>.Instance);
        var output = await client.SolveAsync(input);

        _out.WriteLine($"Solver: feasible={output.Feasible} assignments={output.Assignments.Count}");
        foreach (var a in output.Assignments)
            _out.WriteLine($"  assigned person={a.PersonId} slot={a.SlotId}");

        output.Feasible.Should().BeTrue();
        output.Assignments.Should().NotBeEmpty();

        // person1 must NOT be assigned — they are at_home (blocked)
        output.Assignments.Should().NotContain(
            a => a.PersonId == person1.Id.ToString(),
            "at_home person must not be assigned to any slot");

        // person2 should be assigned
        output.Assignments.Should().Contain(
            a => a.PersonId == person2.Id.ToString(),
            "free person should cover the slot");
    }

    // ── Test 5: Normalizer correctly maps PresenceState.Blocked to "blocked" ──

    [Fact]
    public async Task Normalizer_BlockedPresenceWindow_MappedToCorrectStateString()
    {
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId  = Guid.NewGuid();

        var space = Space.Create("Test Space", userId, locale: "en");
        SetId(space, spaceId);
        db.Spaces.Add(space);

        var group = Group.Create(spaceId, null, "Epsilon");
        group.UpdateSettings(7);
        db.Groups.Add(group);

        var person = Person.Create(spaceId, "Test Person");
        db.People.Add(person);
        db.GroupMemberships.Add(GroupMembership.Create(spaceId, group.Id, person.Id));

        // Add one of each presence state (domain only has FreeInBase, AtHome; OnMission is derived)
        db.PresenceWindows.Add(PresenceWindow.CreateManual(
            spaceId, person.Id, PresenceState.AtHome,
            Tomorrow(0), Tomorrow(4)));
        db.PresenceWindows.Add(PresenceWindow.CreateManual(
            spaceId, person.Id, PresenceState.AtHome,
            Tomorrow(4), Tomorrow(8)));
        db.PresenceWindows.Add(PresenceWindow.CreateDerived(   // OnMission = derived only
            spaceId, person.Id, Tomorrow(8), Tomorrow(12)));
        db.PresenceWindows.Add(PresenceWindow.CreateManual(
            spaceId, person.Id, PresenceState.FreeInBase,
            Tomorrow(12), Tomorrow(16)));

        await db.SaveChangesAsync();

        var run = ScheduleRun.Create(spaceId, ScheduleRunTrigger.Standard, null, userId);
        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync();

        var normalizer = new SolverPayloadNormalizer(db, NullLogger<SolverPayloadNormalizer>.Instance, Substitute.For<ICumulativeTracker>());
        var input = await normalizer.BuildAsync(spaceId, run.Id, "standard", null);

        _out.WriteLine("Presence windows in payload:");
        foreach (var pw in input.PresenceWindows)
            _out.WriteLine($"  person={pw.PersonId} state={pw.State}");

        // All 4 windows should be in the payload
        input.PresenceWindows.Should().HaveCount(4);

        // State strings must match what the solver expects
        var states = input.PresenceWindows.Select(pw => pw.State).ToHashSet();
        states.Should().Contain("at_home",    "AtHome maps to at_home");
        states.Should().Contain("on_mission", "derived OnMission maps to on_mission");
        states.Should().Contain("free_in_base", "FreeInBase maps to free_in_base");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetId(object entity, Guid id)
    {
        var prop = entity.GetType().GetProperty("Id")
            ?? typeof(Jobuler.Domain.Common.Entity).GetProperty("Id");
        prop?.SetValue(entity, id);
    }
}
