# 416 — Freeze Period Discard Audit Fixes

## Phase

Post-implementation quality audit — freeze-period-discard feature

## Purpose

Addresses 11 audit issues found in the freeze-period-discard feature, ranging from a critical null-crash bug to quality improvements and accessibility fixes.

## What was built

### Critical Bug Fix (Issue 1)

- **`apps/api/Jobuler.Api/Controllers/HomeLeaveConfigController.cs`** — Removed conditional branching that returned `Config: null` when discard was performed. The controller now always returns the config in the response. Made the `Config` property non-nullable in the `DeactivateFreezeResponse` record.

### Transaction Safety (Issue 2)

- **`apps/api/Jobuler.Application/HomeLeave/Commands/DeactivateFreezeWithDiscardCommand.cs`** — Wrapped the discard block (version creation + assignment copying) in an explicit database transaction. Skips transaction for in-memory DB (tests). Includes proper rollback on failure and disposal in finally block.

### Handler Decomposition (Issues 3 & 4)

- **`apps/api/Jobuler.Application/HomeLeave/Commands/DeactivateFreezeWithDiscardCommand.cs`** — Extracted the 120+ line `Handle` method into focused private methods:
  - `SetRlsSessionVariables()` — PostgreSQL RLS session setup
  - `PerformDiscardAsync()` — Full discard orchestration returning (performed, versionId, changeCount)
  - `CountFreezeChangesAsync()` — Extracted counting logic (shared with GetFreezePeriodChangesCountQuery)
  - `BuildResultConfig()` — DTO construction from domain entity

### Domain Constant (Issue 5)

- **`apps/api/Jobuler.Domain/Scheduling/AssignmentReasons.cs`** — New file with `AssignmentReasons.ManualOverride` constant replacing fragile string literals.
- **`apps/api/Jobuler.Application/HomeLeave/Queries/GetFreezePeriodChangesCountQuery.cs`** — Uses the constant.
- **`apps/api/Jobuler.Application/Scheduling/Commands/ApplyManualOverrideCommand.cs`** — Uses the constant.

### Frontend Permission Proxy (Issue 6)

- **`apps/web/components/home-leave/HomeLeaveConfigPanel.tsx`** — Added TODO comment explaining `isAdmin` is used as a proxy for `canRollback` until a proper permission query system exists.

### Parallel Recomputation (Issue 7)

- **`apps/api/Jobuler.Application/HomeLeave/Commands/DeactivateFreezeWithDiscardCommand.cs`** — Replaced sequential `foreach` loop with `Task.WhenAll` for cumulative recomputation.

### Memory Pressure Comment (Issue 8)

- **`apps/api/Jobuler.Application/HomeLeave/Commands/DeactivateFreezeWithDiscardCommand.cs`** — Added NOTE comment about batching for 10,000+ assignment schedules.

### Shared Test Helpers (Issue 9)

- **`apps/api/Jobuler.Tests/HomeLeave/Helpers/FreezeTestFixture.cs`** — New shared helper class with `CreateDb()`, `AllowAllPermissions()`, `DenyPermission()`, `AllowConstraintsManageOnly()`, `CreateAuditLogger()`, `CreateCumulativeTracker()`, and `SeedFrozenConfig()`.

### Test Coverage Comments (Issue 10)

- **`apps/api/Jobuler.Tests/HomeLeave/DeactivateFreezePermissionTests.cs`** — Added NOTE comment explaining intentional overlap with command tests.
- **`apps/api/Jobuler.Tests/HomeLeave/DeactivateFreezeAuditLogTests.cs`** — Added NOTE comment explaining intentional overlap with command tests.

### Accessibility (Issue 11)

- **`apps/web/components/home-leave/FreezeDeactivationDialog.tsx`** — Added:
  - Escape key handler to close dialog
  - Backdrop click to close (with `stopPropagation` on inner panel)
  - `useRef` + `autoFocus` via `tabIndex={-1}` on dialog container for focus management

## Key decisions

1. **Transaction wrapping** uses `_db.Database.IsRelational()` guard to skip transactions in in-memory test DB (which doesn't support them).
2. **Handler decomposition** keeps the main `Handle` method as a thin orchestrator. Private methods are instance methods (not static) since they need `_db` and other dependencies.
3. **AssignmentReasons constant** placed in Domain layer since it's a domain concept used across Application handlers.
4. **Shared test fixture** uses `Jobuler.Tests.HomeLeave` namespace (not a sub-namespace) to avoid breaking existing `Helpers.NoOpCacheService` references.
5. **Parallel recomputation** uses `Task.WhenAll` — safe because `ICumulativeTracker` implementations should be thread-safe for different person IDs.

## How it connects

- The null-config fix prevents runtime crashes in `HomeLeaveConfigPanel.tsx` when discard is performed.
- The transaction wrapping ensures no orphaned schedule versions on partial failure.
- The `AssignmentReasons` constant is used by both the freeze-period query and the manual override command.

## How to run / verify

```bash
cd apps/api
dotnet build          # Should succeed with 0 errors
dotnet test           # All 1335+ tests should pass (12 solver tests skipped without solver)

cd apps/web
npx tsc --noEmit      # Should succeed with 0 errors
```

## What comes next

- Migrate existing test files to use `FreezeTestFixture` shared helpers (reduces duplication further)
- Implement proper frontend permission query for `canRollback` (replacing `isAdmin` proxy)
- Consider batched bulk insert for large-scale schedules when scale demands it

## Git commit

```bash
git add -A && git commit -m "fix(freeze-discard): resolve null config crash and 10 audit issues"
```
