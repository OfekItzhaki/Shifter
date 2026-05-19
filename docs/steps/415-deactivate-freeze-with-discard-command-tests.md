# Step 415 — Deactivate Freeze With Discard Command Unit Tests

## Phase

Feature: freeze-period-discard (Task 2.4)

## Purpose

Provides comprehensive unit test coverage for the `DeactivateFreezeWithDiscardCommand` handler, verifying all deactivation paths (with and without discard), permission enforcement, error handling, and audit logging.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/HomeLeave/DeactivateFreezeWithDiscardCommandTests.cs` | 9 unit tests covering all sub-tasks for the deactivate freeze with discard command |

## Key decisions

- Used NSubstitute for mocking `IPermissionService`, `IAuditLogger`, and `ICumulativeTracker` — consistent with existing test patterns.
- Used in-memory EF Core database for data setup — same approach as `ScheduleVersionDiscardPropertyTests`.
- Used reflection to set `FreezeStartedAt`, `PublishedAt`, and `CreatedAt` since these have private/protected setters.
- Tested atomic operation failure by injecting a failing `ICumulativeTracker` mock.
- Verified audit log calls using NSubstitute's `Received()` with argument matchers for JSON content.

## How it connects

- Tests validate the handler implemented in step 407 (`DeactivateFreezeWithDiscardCommandHandler`).
- Uses the same `NoOpCacheService` helper from `Jobuler.Tests/Helpers/`.
- Covers requirements 3.1–3.6, 4.1–4.4, 5.1–5.4, 6.4–6.6 from the freeze-period-discard spec.

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~DeactivateFreezeWithDiscardCommandTests"
```

All 9 tests should pass.

## What comes next

- Task 4.3: API endpoint permission enforcement tests
- Task 5.2: Audit log entry tests
- Task 7.4: Frontend deactivation dialog tests

## Git commit

```bash
git add -A && git commit -m "feat(freeze-period-discard): unit tests for deactivate freeze with discard command"
```
