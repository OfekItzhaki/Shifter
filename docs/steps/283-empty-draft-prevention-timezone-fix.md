# 283 — Empty Draft Prevention & Timezone-Aware Date Comparison

## Phase
Bugfix — Production reliability

## Purpose
Two issues fixed:
1. **Empty Draft Prevention**: The solver worker created a `ScheduleVersion` object before validating the solver result. If the solver failed or returned zero assignments, the version number was consumed and unnecessary objects were allocated. Now the version is only created and persisted AFTER confirming the solver returned valid assignments.
2. **Timezone-Aware Date Matching**: The schedule tab's auto-navigation effect compared UTC ISO strings directly with local dates (e.g. `a.slotStartsAt.startsWith("2026-05-16")`). When a slot starts at `2026-05-15T21:00:00Z` (which is May 16 00:00 in Israel), the comparison failed. Fixed by converting UTC timestamps to local dates before comparing.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs` | Moved `ScheduleVersion.CreateDraft(...)`, version number computation, and assignment creation inside the `if (!shouldDiscard)` block. Version is now only instantiated and persisted after solver success is confirmed. |
| `apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx` | Replaced `a.slotStartsAt.startsWith(date)` with `new Date(a.slotStartsAt).toLocaleDateString("sv") === date` in the auto-navigation effect. |

## Key decisions
- Used `toLocaleDateString("sv")` (Swedish locale) which outputs `YYYY-MM-DD` format — consistent with how `today` and `weekDates` are formatted.
- The `overlapsDate` function in `ScheduleTaskTable.tsx` was already correct (it uses `new Date()` which converts UTC to local time). Only the auto-navigation effect in `ScheduleTab.tsx` had the raw UTC string comparison bug.
- In the solver worker, `parsedAssignmentDtos` is computed first (filtering unparseable slot IDs), then `shouldDiscard` is evaluated. Only if the result is valid do we allocate a version number and create the version + assignments.

## How it connects
- The solver worker is the only code path that creates draft schedule versions.
- The schedule tab auto-navigation is what users see when they open the schedule — it should land on the correct day regardless of timezone offset.

## How to run / verify
```bash
cd apps/api && dotnet build --no-restore -v q
cd apps/web && npx tsc --noEmit
```

Both pass cleanly.

## What comes next
- No follow-up needed — these are isolated bugfixes.

## Git commit
```bash
git add -A && git commit -m "fix(solver+schedule): prevent empty drafts, fix timezone date comparison"
```
