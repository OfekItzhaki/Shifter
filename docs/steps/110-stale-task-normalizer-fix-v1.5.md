# Step 110 — Stale Task Normalizer Fix + v1.5.0

## Phase
Phase 9 — Polish & Hardening

## Purpose
The solver was generating schedules for missions that had already ended (e.g. April tasks in May). The `TriggerSolverCommand` guard added in step 108 correctly blocked new runs when all tasks were in the past, but an existing draft could still be published, and the normalizer itself had a bug that silently extended any past-ended task to the full horizon.

## Root cause
`SolverPayloadNormalizer.BuildAsync` contained this logic:

```csharp
var effectiveEnd = task.EndsAt <= horizonStartDt
    ? horizonEndDt   // ← extended ANY past task to the full horizon
    : task.EndsAt;
```

This was originally intended for "open-ended recurring tasks" but it also caught tasks with a real, expired `EndsAt` date — meaning the solver received shift slots for April missions even when running in May.

## What was fixed

### `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs`
- Added `t.EndsAt > horizonStartDt` to the `GroupTasks` DB query — expired tasks are now excluded at the query level, before any expansion logic runs
- Removed the `effectiveEnd` auto-extension entirely — `windowEnd` is now simply `Min(task.EndsAt, horizonEndDt)`, respecting the task's own end date
- The `windowStart` clamping is unchanged — tasks that start before `now` but end in the future are still included from `now` onwards

## Version bump
- `apps/web/package.json`: `1.4.0` → `1.5.0`
- `apps/web/components/shell/AppShell.tsx`: fallback version string updated to `1.5.0`

## How to verify
1. Create a group task with `EndsAt` in the past (e.g. April)
2. Trigger the solver for that group — expect HTTP 400 "all tasks end in the past"
3. Create a task with `EndsAt` in the future — solver runs and only generates shifts within the task's actual window
4. No shifts appear for expired tasks in the draft

## Git commit

```bash
git add -A && git commit -m "fix(solver): exclude expired tasks from payload; never extend past EndsAt; bump v1.5.0"
```
