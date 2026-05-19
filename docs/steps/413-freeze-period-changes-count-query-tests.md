# Step 413 — Freeze Period Changes Count Query Unit Tests

## Phase

Feature: freeze-period-discard (Task 1.4)

## Purpose

Validates the `GetFreezePeriodChangesCountQueryHandler` logic with unit tests covering all key scenarios: inactive freeze, null `FreezeStartedAt`, correct override counting, categorization of manual assignments vs swaps, and proper scoping to space + draft versions only.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/HomeLeave/GetFreezePeriodChangesCountQueryTests.cs` | xUnit test class with 7 tests covering the query handler |

## Key decisions

- Used the in-memory EF Core database pattern consistent with other tests in the project
- Used EF entry property manipulation to set `CreatedAt` and `FreezeStartedAt` to deterministic timestamps (same pattern as `GroupAlertPropertyTests`)
- Tests cover all sub-task scenarios: freeze not active, `FreezeStartedAt` null, override counting, manual/swap categorization, and space+draft scoping
- Added a test for `KeyNotFoundException` when group doesn't exist (requirement 7.2)

## How it connects

- Tests validate the handler implemented in task 1.1 (`GetFreezePeriodChangesCountQuery`)
- Covers requirements 2.1, 2.2, 2.3, 2.4, 2.5, 7.2, 7.3
- Ensures the preview endpoint returns correct data before the admin confirms discard

## How to run / verify

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~Jobuler.Tests.HomeLeave.GetFreezePeriodChangesCountQueryTests"
```

All 7 tests should pass.

## What comes next

- Task 2.4: Unit tests for the `DeactivateFreezeWithDiscardCommand` handler
- Task 4.3: Unit tests for API endpoint permission enforcement

## Git commit

```bash
git add -A && git commit -m "test(freeze-period-discard): unit tests for freeze-period change count query"
```
