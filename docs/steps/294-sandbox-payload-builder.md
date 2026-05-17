# 294 — Sandbox Payload Builder

## Phase
Feature: Draft Simulation Sandbox

## Purpose
Implements the `buildOverridePayload` pure function that merges a baseline `SolverInputDto` with all sandbox overrides (tasks, constraints, member exclusions, settings). This is the core logic that constructs the override payload sent to the simulation endpoint.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/store/sandboxPayloadBuilder.ts` | Pure function `buildOverridePayload` that merges baseline with overrides |

### Key logic:
- **Task overrides**: "add" appends new slots, "edit" replaces matching slot fields, "remove" filters out the slot
- **Constraint overrides**: Same add/edit/remove pattern applied to both `hardConstraints` and `softConstraints` arrays. Hard vs soft is distinguished by the presence of a `weight` field.
- **Member exclusions**: Filters out people whose `personId` is in the exclusion set
- **Settings — minRestBetweenShiftsHours**: Finds/creates a hard constraint with `ruleType: "min_rest_between_assignments"` and sets `payload.min_hours`
- **Settings — home-leave params**: Updates `homeLeaveConfig` fields (eligibility_threshold_hours, leave_duration_hours, leave_capacity, balance_value)

## Key decisions

1. **Processing order**: Task overrides → constraint overrides → member exclusions → settings overrides (settings applied on top of already-processed constraints)
2. **No mutation**: Every step produces new arrays/objects via spread. The baseline is never mutated.
3. **Hard vs soft constraint distinction**: Uses the presence of `weight` field to determine if an added constraint is hard or soft.
4. **Min rest constraint ID**: Uses `"sandbox-min-rest"` as the constraintId when creating a new min_rest_between_assignments constraint.
5. **Home-leave config creation**: If no baseline config exists but home-leave settings are overridden, creates a default enabled config.

## How it connects

- Consumed by the simulation run trigger (task 7.2) to build the payload before calling `POST /simulate`
- Depends on types from `sandboxStore.ts` (task 4.1)
- Property tests (tasks 4.3–4.10) will validate this function's correctness

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit  # TypeScript compilation check
```

## What comes next

- Property-based tests for the payload builder (tasks 4.3–4.10)
- Simulation run trigger wiring (task 7.2)

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): implement buildOverridePayload pure function"
```
