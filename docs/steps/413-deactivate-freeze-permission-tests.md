# 413 — Deactivate Freeze Permission Enforcement Tests

## Phase

Feature: freeze-period-discard (Task 4.3)

## Purpose

Verifies that the `DeactivateFreezeWithDiscardCommandHandler` correctly enforces permission boundaries: requiring `schedule.rollback` for discard operations, allowing standard deactivation with only `constraints.manage`, recording denied attempts in the audit log, and rejecting requests when freeze is not active.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/HomeLeave/DeactivateFreezePermissionTests.cs` | Unit tests for API endpoint permission enforcement |

## Key decisions

- Tests exercise the handler directly (not the controller) since the `ExceptionHandlingMiddleware` maps `UnauthorizedAccessException` → 403 and `InvalidOperationException` → 400 at the API layer.
- Uses NSubstitute to selectively allow `constraints.manage` while denying `schedule.rollback`, matching the real permission split.
- Verifies audit log receives the denied attempt with correct action and permission metadata.

## How it connects

- Validates requirements 5.1, 5.2, 5.3, 5.4, 6.5 from the freeze-period-discard spec.
- Relies on the handler implemented in step 407 and the audit logging from step 408.
- Uses the same in-memory DB + NSubstitute patterns as other handler tests in the project.

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~Jobuler.Tests.HomeLeave.DeactivateFreezePermissionTests"
```

All 4 tests should pass.

## What comes next

- Task 5.2: Unit tests for audit log entries (discard and non-discard paths).

## Git commit

```bash
git add -A && git commit -m "test(freeze-discard): add permission enforcement unit tests for deactivate freeze handler"
```
