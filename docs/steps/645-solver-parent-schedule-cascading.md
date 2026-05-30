# 645 — Solver Integration: Parent Schedule Cascading

## Phase

Space-First Onboarding (Task 16)

## Purpose

When a child group's schedule is generated, the solver must avoid assigning people to shifts that conflict with their assignments in the parent group's published schedule. This ensures hierarchical schedule consistency — parent schedules take priority and child schedules work around them.

## What was built

All components were already implemented in prior steps. This task verified completeness:

### API Layer (C#)

- **`apps/api/Jobuler.Application/Scheduling/Models/SolverInputDto.cs`** — `ParentSchedule` field (`List<ParentAssignmentDto>?`) with `[JsonPropertyName("parent_schedule")]`. `ParentAssignmentDto` record with `person_id`, `starts_at`, `ends_at`.
- **`apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs`** — Parent schedule cascading section that:
  1. Checks if the group has a `ParentGroupId`
  2. Fetches the latest published `ScheduleVersion` for the space
  3. Loads parent group's active tasks
  4. Resolves assignments to time windows using `DeriveShiftGuid`
  5. Populates `parentScheduleDto` with matching assignments

### Solver Layer (Python)

- **`apps/solver/models/solver_input.py`** — `parent_schedule: Optional[list["ParentAssignment"]]` field on `SolverInput`. `ParentAssignment` model with `person_id`, `starts_at`, `ends_at`.
- **`apps/solver/solver/engine.py`** — `_add_parent_schedule_constraints()` function that blocks child assignments overlapping with parent assignments for the same person. Called in the main `solve()` function after availability constraints.

## Key decisions

1. **Space-scoped versions** — `ScheduleVersion` has no `GroupId`; filtering is done by matching assignment slot IDs to parent group task-derived GUIDs.
2. **Graceful degradation** — If the parent has no published version, the field is simply `None`/null and no constraints are added.
3. **Hard constraint** — Parent assignments are treated as immutable hard blocks (not soft penalties), ensuring zero conflicts.
4. **Stateless solver** — All parent schedule data is sent in the payload; the solver never queries external state.

## How it connects

- Depends on Task 3 (ParentGroupId on Group entity) and Task 7 (Link Parent Group API)
- The solver worker service calls `SolverPayloadNormalizer.BuildAsync()` which now includes parent schedule data when applicable
- The Python solver's `_add_parent_schedule_constraints` uses the same overlap-detection pattern as availability/presence constraints

## How to run / verify

```bash
# Build the API (should succeed with 0 errors)
cd apps/api && dotnet build

# Verify solver input model accepts parent_schedule
cd apps/solver && python -c "from models.solver_input import SolverInput, ParentAssignment; print('OK')"
```

Integration test: Create a parent group with a published schedule, link a child group, trigger a solver run for the child — the payload should include `parent_schedule` and the solver should not assign overlapping shifts.

## What comes next

This completes Task 16 and the space-first-onboarding spec. No further tasks depend on this.

## Git commit

```bash
git add -A && git commit -m "feat(spaces): verify solver parent schedule cascading (task 16)"
```
