# 348 — Publish-Time Home Leave Protection

## Phase

Feature: Home Leave Protection

## Purpose

Hardens the `CreateHomeLeavePresenceWindowsAsync` method in `PublishVersionCommand` to ensure that publishing a new schedule version never silently revokes or modifies existing home leave windows. The stale-window removal is now scoped per-person to only the time ranges covered by that person's new entries, preventing accidental removal of derived AtHome windows that overlap with the global time range but not with the specific person's new assignments.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Scheduling/Commands/PublishVersionCommand.cs` | Refactored stale-window removal to iterate per-person with per-person time ranges instead of using a global `minStart`/`maxEnd` across all affected people. Added explicit `IsDerived == true` comparison and detailed protection comments. |

## Key decisions

1. **Per-person time scoping**: Instead of querying all derived AtHome windows for all affected people within the global `minStart`–`maxEnd` range, the removal now iterates each affected person and only removes their derived windows that overlap with *their specific* new entries' time range. This prevents a scenario where person A's derived window is accidentally removed because it overlaps with the global range driven by person B's entries.

2. **Explicit `IsDerived == true`**: Changed from `pw.IsDerived` to `pw.IsDerived == true` for clarity and to make the intent unmistakable in code review.

3. **No modification of existing windows**: The method only removes stale derived windows and creates new ones. It never calls `Truncate()` or modifies `StartsAt`/`EndsAt` on any existing window.

4. **Manual windows untouched**: The `IsDerived == true` filter guarantees manual AtHome windows (`IsDerived = false`) are never queried or deleted by the publish operation.

## How it connects

- Satisfies Requirements 2.1, 2.2, 2.3, 2.4, and 7.3 from the Home Leave Protection spec
- Works alongside the solver exclusion (task 1.1) which prevents task assignment to people on leave
- The CancelHomeLeaveCommand (task 4) is the only path to revoke home leave — publish never does it

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --no-restore
```

## What comes next

- Task 2.2: Property test for publish preserves non-target AtHome windows
- Task 2.3: Property test for manual windows preservation

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): harden publish-time protection of existing AtHome windows"
```
