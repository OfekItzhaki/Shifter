# Step 407 — Deactivate Freeze With Discard Command Handler

## Phase

Feature — Freeze Period Discard

## Purpose

Implements the `DeactivateFreezeWithDiscardCommandHandler` which orchestrates the full freeze deactivation flow: permission enforcement, optional discard of freeze-period changes via rollback version creation, cumulative hour recomputation, cache invalidation, and audit logging.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Commands/DeactivateFreezeWithDiscardCommand.cs` | Added `DeactivateFreezeWithDiscardCommandHandler` implementing the full deactivation + discard logic |

## Key decisions

- **Follows existing `RollbackVersionCommand` pattern** — Uses `ScheduleVersion.CreateRollback()` to create a new draft version from the pre-freeze baseline, copies assignments atomically, recomputes cumulative hours, and invalidates cache.
- **Dual permission model** — All deactivation requires `constraints.manage`; discard additionally requires `schedule.rollback`.
- **Zero-change optimization** — If no freeze-period changes exist, skips version creation entirely but still deactivates the freeze.
- **Separate audit entries** — Discard produces a `discard_freeze_changes` audit entry; non-discard deactivation produces a `deactivate_freeze` entry.
- **RLS session variables** — Sets PostgreSQL session variables before any DB access, matching the pattern in all other handlers.

## How it connects

- Consumes `IPermissionService`, `IAuditLogger`, `ICumulativeTracker`, `ICacheService` from the Application layer.
- Uses `ScheduleVersion.CreateRollback()` and `Assignment.Create()` domain factory methods.
- Calls `HomeLeaveConfig.DeactivateEmergencyFreeze()` to clear freeze state.
- Will be dispatched by the `HomeLeaveConfigController` deactivate-freeze endpoint (task 4.1).
- Returns `HomeLeaveConfigResult` matching the existing DTO used by the upsert handler.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --no-build --filter "FullyQualifiedName~HomeLeave" --verbosity minimal
```

## What comes next

- Task 2.3: `DeactivateFreezeWithDiscardCommandValidator` (FluentValidation)
- Task 2.4: Unit tests for the handler
- Task 4.1: Controller endpoint wiring

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): implement DeactivateFreezeWithDiscardCommandHandler"
```
