# 408 — Deactivate Freeze Controller Action

## Phase

Feature — Freeze Period Discard (Task 4.1)

## Purpose

Adds the `POST deactivate-freeze` API endpoint to `HomeLeaveConfigController`, giving the frontend a clear contract for deactivating the emergency freeze with an optional discard flag. The endpoint dispatches `DeactivateFreezeWithDiscardCommand` via MediatR and returns different response shapes depending on whether a discard was performed.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/HomeLeaveConfigController.cs` | Added `DeactivateFreeze` action (`[HttpPost("deactivate-freeze")]`), `DeactivateFreezeRequest` record (with `DiscardFreezeChanges` defaulting to `false`), and `DeactivateFreezeResponse` record |

## Key decisions

- **Controller only checks `constraints.manage`** — the command handler internally enforces `schedule.rollback` when discard is requested and throws `UnauthorizedAccessException` (mapped to 403 by `ExceptionHandlingMiddleware`).
- **`InvalidOperationException` → 400** — the handler throws this when freeze is not active or no pre-freeze baseline exists; the middleware maps it to 400.
- **Response shape varies by `DiscardPerformed`** — when discard is performed, `Config` is null and `DiscardVersionId`/`DiscardedChangeCount` are populated; when no discard, `Config` contains the updated home-leave configuration.
- **Request DTO uses default parameter** — `DeactivateFreezeRequest(bool DiscardFreezeChanges = false)` so the parameter is optional in the JSON body per requirement 6.1.

## How it connects

- Consumes `DeactivateFreezeWithDiscardCommand` (task 2.1/2.2) and its result type
- Permission enforcement (task 4.2) is handled inside the command handler
- Frontend (task 7.1) will call `POST /spaces/{spaceId}/groups/{groupId}/home-leave-config/deactivate-freeze`
- Error mapping relies on `ExceptionHandlingMiddleware` (existing infrastructure)

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with no new errors.

## What comes next

- Task 4.2: Server-side permission enforcement verification (already implemented in handler)
- Task 4.3: Unit tests for API endpoint permission enforcement
- Task 5: Audit logging entries (already wired in handler)

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): add DeactivateFreeze POST action to HomeLeaveConfigController"
```
