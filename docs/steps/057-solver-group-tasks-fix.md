# Step 057 — Solver: Group Tasks Integration Fix

## Phase
Phase 8 — Production Hardening

## Purpose
The solver was always receiving zero task slots and producing empty schedules. Root cause: `SolverPayloadNormalizer` only queried the legacy `TaskSlots` table, but all tasks created via the UI are stored in `GroupTasks` (the newer flat model). The two tables were never connected.

Additionally, the solver horizon was hardcoded to 7 days, ignoring the per-group `SolverHorizonDays` setting.

## What was built

### `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs`

Two fixes:

**1. Group tasks included in solver payload**
After loading legacy `TaskSlots`, the normalizer now also queries `GroupTasks` for the same space and horizon window. Each active `GroupTask` is mapped to a `TaskSlotDto`:
- `SlotId` = `GroupTask.Id`
- `TaskTypeId` = `GroupTask.GroupId` (used as a logical grouping bucket)
- `TaskTypeName` = `GroupTask.Name`
- `BurdenLevel` = `GroupTask.BurdenLevel`
- `AllowsOverlap` = `GroupTask.AllowsOverlap`
- No role/qualification requirements (group tasks don't have them)

Both legacy slots and group task slots are merged into a single `slotsDto` list sent to the solver.

**2. Dynamic solver horizon**
Instead of hardcoding `today.AddDays(6)`, the normalizer now queries the maximum `SolverHorizonDays` across all active groups in the space. Falls back to 7 days if no groups exist.

## Key decisions
- No migration or data duplication — group tasks are projected to `TaskSlotDto` at query time
- `GroupId` as `TaskTypeId` is a reasonable proxy — the solver uses it for grouping/fairness, and group tasks are already scoped to a group
- Max horizon across groups (not per-group) — the solver runs once per space, so it needs to cover all groups' horizons in one pass

## How it connects
- User creates tasks in the group detail page → stored in `group_tasks` table
- Admin triggers solver → `TriggerSolverCommand` → Redis queue → `SolverWorkerService` → `SolverPayloadNormalizer.BuildAsync` → now includes group tasks → sent to Python solver → draft version created

## How to run / verify
1. Create a group, add members, create tasks with future dates
2. Trigger solver: `POST /spaces/{spaceId}/schedule-runs/trigger` with `{ "triggerMode": "standard" }`
3. Poll `GET /spaces/{spaceId}/schedule-runs/{runId}` until status = "Completed"
4. Check `GET /spaces/{spaceId}/schedule-versions?status=draft` — should have a new draft
5. Verify assignments exist in the draft version

## What comes next
- Wire the Python solver service (currently the API calls `http://localhost:8000/solve`)
- Add `AllowsDoubleShift` to the solver payload schema if the Python solver supports it

## Git commit
```bash
git add -A && git commit -m "fix(solver): include group tasks in solver payload, dynamic horizon from group settings"
```
