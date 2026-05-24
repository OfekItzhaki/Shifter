# 558 — Post-Solve Hard Constraint Validation

## Phase

Bugfix — Solver Constraints Fix (Task 3.4)

## Purpose

The CP-SAT solver guarantees feasibility only for constraints actually added to its model. If a constraint was missed during model construction (e.g., due to incorrect min-rest resolution), the solver may return a "feasible" result that violates business rules. This step adds a post-solve validation layer in `SolverWorkerService.cs` that catches hard constraint violations before creating a draft version, preventing broken schedules from reaching admins.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs` | Added `ValidateHardConstraints()`, `ResolveMinRestHours()`, and `TryParseSlotTimes()` static methods. Integrated validation call between solver output and draft creation. |

### Key additions in `SolverWorkerService.cs`:

1. **`ValidateHardConstraints(input, assignments)`** — Validates solver output assignments against input constraints. Checks:
   - Min-rest violations: For each non-emergency person, verifies all pairs of assigned slots have a gap ≥ min_rest_hours
   - Qualification mismatches: Each assignment's person must have the required qualifications for the slot
   - Role mismatches: Each assignment's person must have the required roles for the slot
   - Availability conflicts: Each assignment's person must be available during the slot time

2. **`ResolveMinRestHours(input)`** — Resolves effective min_rest_hours using the fallback chain: HomeLeaveConfig value → hard constraint rule → 8.0 default (for closed-base groups)

3. **`TryParseSlotTimes(slot, out start, out end)`** — Helper to parse ISO 8601 slot times

4. **Integration in `ProcessNextJobAsync`** — Validation runs after solver returns feasible result but before `shouldDiscard` decision. If violations found:
   - `shouldDiscard = true` (no draft created)
   - Run marked as failed with violation details
   - Admin notification includes specific constraint names, affected people, and actionable guidance

## Key decisions

- **Emergency-bypassed people excluded**: People with emergency constraints (scope type "person") are excluded from all validation checks, preserving the existing emergency bypass behavior.
- **Validation is additive**: The `shouldDiscard` flag now includes `hasPostSolveViolations` alongside existing checks (infeasible, zero assignments, uncovered slots). Existing behavior is unchanged when no violations are found.
- **Locale-aware notifications**: Violation notifications support Hebrew, Russian, and English, consistent with the rest of the worker.
- **Static methods**: Validation logic is implemented as static methods for testability and to avoid side effects.
- **Timed-out results unaffected**: Timed-out results with no violations continue creating drafts normally (the validation only triggers when `output.Feasible` is true and there are parsed assignments).

## How it connects

- **Upstream**: Receives `SolverInputDto` (constraints, people, slots) and `SolverOutputDto` (assignments) from the solver pipeline
- **Downstream**: Prevents draft version creation when violations are detected, notifies admins via `INotificationService`
- **Related**: Works alongside the Python solver's own constraint enforcement — this is a safety net for cases where constraints weren't added to the model
- **Spec**: Implements requirement 2.4 (infeasibility alert), preserves 3.3 (valid drafts) and 3.6 (timed-out partial results)

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --filter "FullyQualifiedName~Recommendation"
```

The solver end-to-end tests require the Python solver running on localhost:8000. When available:
```bash
dotnet test --filter "FullyQualifiedName~SolverEndToEnd"
```

## What comes next

- Task 3.5: Verify bug condition exploration test now passes (confirms post-solve validation catches violations)
- Task 3.6: Verify preservation tests still pass (confirms valid results still create drafts)
- Task 4: Full test suite checkpoint

## Git commit

```bash
git add -A && git commit -m "fix(solver): add post-solve hard constraint validation in SolverWorkerService"
```
