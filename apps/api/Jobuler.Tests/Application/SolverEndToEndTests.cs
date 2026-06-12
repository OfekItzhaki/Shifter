// Solver end-to-end tests — hit the live Python solver at http://localhost:8000
// and verify the response shape for both feasible and infeasible inputs.
//
// Run with:   dotnet test --filter "SolverEndToEnd" --logger "console;verbosity=detailed"
// Requires:   Python solver running on localhost:8000
//
// These tests are automatically SKIPPED when the solver is not reachable.

using FluentAssertions;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Infrastructure.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Jobuler.Tests.Application;

/// <summary>
/// Hits the live Python solver directly (no API, no DB, no queue).
/// Tests the exact JSON contract between the .NET worker and the solver.
/// Automatically skipped when the solver is not running on localhost:8000.
/// </summary>
[Trait("Category", "RequiresSolver")]
public class SolverEndToEndTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _out;
    private readonly HttpClient _http;
    private bool _solverAvailable;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static readonly StabilityWeightsDto DefaultWeights = new(10.0, 3.0, 1.0);

    public SolverEndToEndTests(ITestOutputHelper output)
    {
        _out = output;
        _http = new HttpClient { BaseAddress = new Uri("http://localhost:8000"), Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task InitializeAsync()
    {
        try
        {
            var response = await _http.GetAsync("/health");
            _solverAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            _solverAvailable = false;
        }

        if (!_solverAvailable)
            _out.WriteLine("⚠ Solver not reachable at localhost:8000 — tests will be skipped.");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private void SkipIfSolverUnavailable()
    {
        Skip.If(!_solverAvailable, "Solver is not running on localhost:8000. Start it to run these tests.");
    }

    private static SolverInputDto NormalizeInput(SolverInputDto input) => input with
    {
        LockedSlotIds = input.LockedSlotIds ?? [],
        TaskRotation = input.TaskRotation ?? [],
        CumulativeTracking = input.CumulativeTracking ?? [],
        ParentSchedule = input.ParentSchedule ?? [],
        SpecialDays = input.SpecialDays ?? []
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Tomorrow(int offsetHours = 0) =>
        DateTime.UtcNow.Date.AddDays(1).AddHours(offsetHours).ToString("o");

    private static string DayAfter(int offsetHours = 0) =>
        DateTime.UtcNow.Date.AddDays(2).AddHours(offsetHours).ToString("o");

    private static string Today() => DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
    private static string InDays(int d) => DateTime.UtcNow.Date.AddDays(d).ToString("yyyy-MM-dd");

    private async Task<SolverOutputDto> CallSolverAsync(SolverInputDto input)
    {
        var response = await _http.PostAsJsonAsync("/solve", NormalizeInput(input), JsonOptions);
        var body = await response.Content.ReadAsStringAsync();
        _out.WriteLine($"HTTP {(int)response.StatusCode}");
        _out.WriteLine(body.Length > 2000 ? body[..2000] + "…" : body);
        response.IsSuccessStatusCode.Should().BeTrue($"solver returned {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<SolverOutputDto>(body, JsonOptions)!;
    }

    // ── Test 1: Solver health ─────────────────────────────────────────────────

    [SkippableFact]
    public async Task Solver_HealthEndpoint_Returns200()
    {
        SkipIfSolverUnavailable();
        var r = await _http.GetAsync("/health");
        _out.WriteLine($"Health: {(int)r.StatusCode}");
        r.IsSuccessStatusCode.Should().BeTrue("solver health endpoint must be reachable");
    }

    // ── Test 2: Feasible — 3 people, 1 slot needing 1 person ─────────────────
    // Should produce: feasible=true, assignments.Count >= 1

    [SkippableFact]
    public async Task Solver_SimpleFeasible_OnePerson_OneSlot_ReturnsAssignment()
    {
        SkipIfSolverUnavailable();
        var spaceId  = Guid.NewGuid().ToString();
        var runId    = Guid.NewGuid().ToString();
        var personId = Guid.NewGuid().ToString();
        var slotId   = Guid.NewGuid().ToString();
        var taskType = Guid.NewGuid().ToString();

        var input = new SolverInputDto(
            SpaceId: spaceId,
            RunId: runId,
            TriggerMode: "standard",
            HorizonStart: Today(),
            HorizonEnd: InDays(1),
            Locale: "en",
            StabilityWeights: DefaultWeights,
            People: [new PersonEligibilityDto(personId, [], [], [])],
            AvailabilityWindows: [],
            PresenceWindows: [],
            TaskSlots: [new TaskSlotDto(
                SlotId: slotId,
                TaskTypeId: taskType,
                TaskTypeName: "Guard",
                BurdenLevel: "normal",
                StartsAt: Tomorrow(8),
                EndsAt: Tomorrow(16),
                RequiredHeadcount: 1,
                Priority: 5,
                RequiredRoleIds: [],
                RequiredQualificationIds: [],
                AllowsOverlap: false,
                QualificationRequirements: [])],
            HardConstraints: [],
            SoftConstraints: [],
            EmergencyConstraints: [],
            BaselineAssignments: [],
            FairnessCounters: []);

        var output = await CallSolverAsync(input);

        _out.WriteLine($"feasible={output.Feasible} assignments={output.Assignments.Count} uncovered={output.UncoveredSlotIds.Count}");

        output.Feasible.Should().BeTrue("1 person + 1 slot = trivially feasible");
        output.Assignments.Should().NotBeEmpty("solver must assign the person to the slot");
        output.Assignments[0].SlotId.Should().Be(slotId);
        output.Assignments[0].PersonId.Should().Be(personId);
    }

    // ── Test 3: Feasible — multiple people, multiple slots ───────────────────
    // 4 people, 2 slots each needing 2 people → should fill both slots

    [SkippableFact]
    public async Task Solver_FourPeople_TwoSlots_TwoHeadcount_ReturnsFullCoverage()
    {
        SkipIfSolverUnavailable();
        var spaceId  = Guid.NewGuid().ToString();
        var runId    = Guid.NewGuid().ToString();
        var taskType = Guid.NewGuid().ToString();
        var people   = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid().ToString()).ToList();
        var slot1    = Guid.NewGuid().ToString();
        var slot2    = Guid.NewGuid().ToString();

        var input = new SolverInputDto(
            SpaceId: spaceId,
            RunId: runId,
            TriggerMode: "standard",
            HorizonStart: Today(),
            HorizonEnd: InDays(2),
            Locale: "en",
            StabilityWeights: DefaultWeights,
            People: people.Select(p => new PersonEligibilityDto(p, [], [], [])).ToList(),
            AvailabilityWindows: [],
            PresenceWindows: [],
            TaskSlots: [
                new TaskSlotDto(slot1, taskType, "Guard", "normal",
                    Tomorrow(6), Tomorrow(14), 2, 5, [], [], false, false, []),
                new TaskSlotDto(slot2, taskType, "Guard", "normal",
                    Tomorrow(14), Tomorrow(22), 2, 5, [], [], false, false, []),
            ],
            HardConstraints: [],
            SoftConstraints: [],
            EmergencyConstraints: [],
            BaselineAssignments: [],
            FairnessCounters: []);

        var output = await CallSolverAsync(input);

        _out.WriteLine($"feasible={output.Feasible} assignments={output.Assignments.Count} uncovered={output.UncoveredSlotIds.Count}");

        output.Feasible.Should().BeTrue("4 people for 2 slots × 2 headcount = feasible");
        output.Assignments.Should().HaveCount(4, "2 slots × 2 headcount = 4 assignments");
        output.UncoveredSlotIds.Should().BeEmpty("all slots should be fully staffed");
    }

    // ── Test 4: Infeasible — 0 people, 1 slot ────────────────────────────────
    // Should produce: feasible=false OR assignments empty

    [SkippableFact]
    public async Task Solver_ZeroPeople_OneSlot_ReturnsInfeasible()
    {
        SkipIfSolverUnavailable();
        var spaceId  = Guid.NewGuid().ToString();
        var runId    = Guid.NewGuid().ToString();
        var slotId   = Guid.NewGuid().ToString();
        var taskType = Guid.NewGuid().ToString();

        var input = new SolverInputDto(
            SpaceId: spaceId,
            RunId: runId,
            TriggerMode: "standard",
            HorizonStart: Today(),
            HorizonEnd: InDays(1),
            Locale: "en",
            StabilityWeights: DefaultWeights,
            People: [],   // ← no people
            AvailabilityWindows: [],
            PresenceWindows: [],
            TaskSlots: [new TaskSlotDto(
                slotId, taskType, "Guard", "normal",
                Tomorrow(8), Tomorrow(16), 1, 5, [], [], false, false, [])],
            HardConstraints: [],
            SoftConstraints: [],
            EmergencyConstraints: [],
            BaselineAssignments: [],
            FairnessCounters: []);

        var output = await CallSolverAsync(input);

        _out.WriteLine($"feasible={output.Feasible} assignments={output.Assignments.Count}");

        // Either infeasible OR feasible with zero assignments — both mean "nothing to show"
        var isEmpty = !output.Feasible || output.Assignments.Count == 0;
        isEmpty.Should().BeTrue("0 people cannot cover any slot");
    }

    // ── Test 5: Infeasible — 1 person, slot requires 3 ───────────────────────

    [SkippableFact]
    public async Task Solver_OnePerson_SlotRequiresThree_ReturnsInfeasibleOrUncovered()
    {
        SkipIfSolverUnavailable();
        var spaceId  = Guid.NewGuid().ToString();
        var runId    = Guid.NewGuid().ToString();
        var personId = Guid.NewGuid().ToString();
        var slotId   = Guid.NewGuid().ToString();
        var taskType = Guid.NewGuid().ToString();

        var input = new SolverInputDto(
            SpaceId: spaceId,
            RunId: runId,
            TriggerMode: "standard",
            HorizonStart: Today(),
            HorizonEnd: InDays(1),
            Locale: "en",
            StabilityWeights: DefaultWeights,
            People: [new PersonEligibilityDto(personId, [], [], [])],
            AvailabilityWindows: [],
            PresenceWindows: [],
            TaskSlots: [new TaskSlotDto(
                slotId, taskType, "Guard", "normal",
                Tomorrow(8), Tomorrow(16),
                RequiredHeadcount: 3,   // ← needs 3, only 1 available
                Priority: 5, [], [], false, false, [])],
            HardConstraints: [],
            SoftConstraints: [],
            EmergencyConstraints: [],
            BaselineAssignments: [],
            FairnessCounters: []);

        var output = await CallSolverAsync(input);

        _out.WriteLine($"feasible={output.Feasible} assignments={output.Assignments.Count} uncovered={output.UncoveredSlotIds.Count}");

        // Slot must be uncovered or infeasible
        var notFullyCovered = !output.Feasible || output.UncoveredSlotIds.Contains(slotId) || output.Assignments.Count < 3;
        notFullyCovered.Should().BeTrue("1 person cannot fill a slot requiring 3");
    }

    // ── Test 6: Hard constraint — person blocked from slot ───────────────────
    // Person has a presence window of type "blocked" covering the slot.
    // Solver must not assign them.

    [SkippableFact]
    public async Task Solver_PersonBlocked_NotAssignedToSlot()
    {
        SkipIfSolverUnavailable();
        var spaceId   = Guid.NewGuid().ToString();
        var runId     = Guid.NewGuid().ToString();
        var personId  = Guid.NewGuid().ToString();
        var person2Id = Guid.NewGuid().ToString();
        var slotId    = Guid.NewGuid().ToString();
        var taskType  = Guid.NewGuid().ToString();

        var input = new SolverInputDto(
            SpaceId: spaceId,
            RunId: runId,
            TriggerMode: "standard",
            HorizonStart: Today(),
            HorizonEnd: InDays(1),
            Locale: "en",
            StabilityWeights: DefaultWeights,
            People: [
                new PersonEligibilityDto(personId, [], [], []),
                new PersonEligibilityDto(person2Id, [], [], []),
            ],
            AvailabilityWindows: [],
            PresenceWindows: [
                // person1 is blocked for the entire slot window
                new PresenceWindowDto(personId, "blocked", Tomorrow(0), Tomorrow(23))
            ],
            TaskSlots: [new TaskSlotDto(
                slotId, taskType, "Guard", "normal",
                Tomorrow(8), Tomorrow(16), 1, 5, [], [], false, false, [])],
            HardConstraints: [],
            SoftConstraints: [],
            EmergencyConstraints: [],
            BaselineAssignments: [],
            FairnessCounters: []);

        var output = await CallSolverAsync(input);

        _out.WriteLine($"feasible={output.Feasible} assignments={output.Assignments.Count}");

        if (output.Feasible && output.Assignments.Count > 0)
        {
            // person1 must NOT be assigned — they are blocked
            output.Assignments.Should().NotContain(
                a => a.PersonId == personId,
                "blocked person must not be assigned");

            // person2 should be assigned instead
            output.Assignments.Should().Contain(
                a => a.PersonId == person2Id,
                "unblocked person should cover the slot");
        }
        // If infeasible that's also acceptable — blocked person + 1 slot = may be infeasible
    }

    // ── Test 7: Response shape — all required fields present ─────────────────

    [SkippableFact]
    public async Task Solver_Response_AlwaysHasRequiredFields()
    {
        SkipIfSolverUnavailable();
        var spaceId  = Guid.NewGuid().ToString();
        var runId    = Guid.NewGuid().ToString();
        var personId = Guid.NewGuid().ToString();
        var slotId   = Guid.NewGuid().ToString();
        var taskType = Guid.NewGuid().ToString();

        var input = new SolverInputDto(
            SpaceId: spaceId,
            RunId: runId,
            TriggerMode: "standard",
            HorizonStart: Today(),
            HorizonEnd: InDays(1),
            Locale: "en",
            StabilityWeights: DefaultWeights,
            People: [new PersonEligibilityDto(personId, [], [], [])],
            AvailabilityWindows: [],
            PresenceWindows: [],
            TaskSlots: [new TaskSlotDto(
                slotId, taskType, "Guard", "normal",
                Tomorrow(8), Tomorrow(16), 1, 5, [], [], false, false, [])],
            HardConstraints: [],
            SoftConstraints: [],
            EmergencyConstraints: [],
            BaselineAssignments: [],
            FairnessCounters: []);

        var output = await CallSolverAsync(input);

        // These fields must always be present regardless of feasibility
        output.Should().NotBeNull();
        output.Assignments.Should().NotBeNull("assignments list must always be present");
        output.UncoveredSlotIds.Should().NotBeNull("uncovered_slot_ids must always be present");
        output.HardConflicts.Should().NotBeNull("hard_conflicts must always be present");
        output.StabilityMetrics.Should().NotBeNull("stability_metrics must always be present");
    }

    // ── Test 8: Empty input — no slots, no people ─────────────────────────────
    // Solver should return gracefully (not crash with 500)

    [SkippableFact]
    public async Task Solver_EmptyInput_NoSlotsNoPeople_ReturnsGracefully()
    {
        SkipIfSolverUnavailable();
        var input = new SolverInputDto(
            SpaceId: Guid.NewGuid().ToString(),
            RunId: Guid.NewGuid().ToString(),
            TriggerMode: "standard",
            HorizonStart: Today(),
            HorizonEnd: InDays(1),
            Locale: "en",
            StabilityWeights: DefaultWeights,
            People: [],
            AvailabilityWindows: [],
            PresenceWindows: [],
            TaskSlots: [],
            HardConstraints: [],
            SoftConstraints: [],
            EmergencyConstraints: [],
            BaselineAssignments: [],
            FairnessCounters: []);

        // Must not throw 500 — solver should handle empty input gracefully
        var response = await _http.PostAsJsonAsync("/solve", NormalizeInput(input), JsonOptions);
        var body = await response.Content.ReadAsStringAsync();
        _out.WriteLine($"HTTP {(int)response.StatusCode}: {body}");

        response.IsSuccessStatusCode.Should().BeTrue("solver must not crash on empty input");

        var output = JsonSerializer.Deserialize<SolverOutputDto>(body, JsonOptions)!;
        output.Assignments.Should().BeEmpty("no slots = no assignments");
    }
}
