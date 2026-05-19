# 404 — Freeze Period Changes Count Query

## Phase

Feature — Freeze Period Discard

## Purpose

Provides a query that counts schedule changes (overrides, manual assignments, swaps) made during an active emergency freeze period. This count is displayed in the deactivation dialog so admins can make an informed decision about whether to discard freeze-period changes.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Queries/GetFreezePeriodChangesCountQuery.cs` | MediatR query, result record, and handler that loads the HomeLeaveConfig, checks freeze state, and counts/categorizes override assignments created during the freeze period in draft versions |

## Key decisions

- **Return zeros early** when freeze is not active or `FreezeStartedAt` is null — avoids unnecessary DB queries.
- **Scope to draft versions only** — published versions are immutable and represent committed state; only draft versions contain pending freeze-period changes.
- **Categorization logic**:
  - **Swaps**: identified by 2+ override assignments on the same slot in the same version (paired reassignments).
  - **Manual assignments**: non-swap overrides with a `ChangeReasonSummary` containing "Manual override".
  - **Overrides**: remaining non-swap overrides without manual assignment markers.
- **Single DB round-trip** for assignments — fetches all relevant overrides in one query, then categorizes in-memory for performance.

## How it connects

- Used by the `HomeLeaveConfigController` (task 1.3) to expose a GET endpoint for the frontend.
- The frontend `FreezeDeactivationDialog` (task 7.2) calls this endpoint on dialog open to display categorized counts.
- Relies on `HomeLeaveConfig.EmergencyFreezeActive` and `FreezeStartedAt` from the domain entity.
- Queries `Assignments` and `ScheduleVersions` DbSets from `AppDbContext`.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application/Jobuler.Application.csproj
```

Unit tests will be added in task 1.4.

## What comes next

- Task 1.2: FluentValidation validator for the query
- Task 1.3: Controller endpoint exposing this query
- Task 1.4: Unit tests

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): add GetFreezePeriodChangesCountQuery and handler"
```
