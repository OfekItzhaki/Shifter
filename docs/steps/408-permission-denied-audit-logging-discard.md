# 408 — Permission Denied Audit Logging for Freeze Discard

## Phase

Freeze Period Discard — Permission Enforcement

## Purpose

When a user attempts to discard freeze-period changes without the required `schedule.rollback` permission, the system must record the denied attempt in the audit log before returning a 403 response. This ensures security-sensitive access denials are traceable and auditable.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/HomeLeave/Commands/DeactivateFreezeWithDiscardCommand.cs` | Wrapped the `schedule.rollback` permission check in a try-catch that logs a `permission_denied` audit entry before re-throwing `UnauthorizedAccessException` |

## Key decisions

1. **Audit logging in the handler, not the controller** — The handler already has `IAuditLogger` injected and knows the full context (space, group, user, action attempted). This follows the architecture rule that business logic stays in the Application layer.

2. **Catch-and-rethrow pattern** — The `UnauthorizedAccessException` is caught, the audit entry is written, then the exception is re-thrown. The `ExceptionHandlingMiddleware` still converts it to a 403 response. This preserves the existing error handling flow.

3. **Audit entry structure** — The `permission_denied` action includes `group_id`, `action_attempted`, and `required_permission` in the `beforeJson` field, providing full context for security audits.

4. **Standard deactivation unchanged** — The `constraints.manage` check at the top of the handler (and in the controller) remains as-is. Only the discard-specific `schedule.rollback` check gets audit logging for denied attempts, since that's the elevated permission per requirement 5.4.

## How it connects

- The `ExceptionHandlingMiddleware` converts `UnauthorizedAccessException` → 403 response (unchanged)
- The controller's `constraints.manage` check runs first — if that fails, the request never reaches the handler
- The handler's `schedule.rollback` check only runs when `DiscardFreezeChanges = true`
- Audit log entries are append-only per immutability rules

## How to run / verify

```bash
dotnet build --no-restore
dotnet test --no-build --filter "DeactivateFreeze"
```

Manually verify: call `POST /spaces/{id}/groups/{id}/home-leave-config/deactivate-freeze` with `{ "discardFreezeChanges": true }` using a user that has `constraints.manage` but lacks `schedule.rollback`. Expect a 403 response and a new `permission_denied` row in the audit log.

## What comes next

- Task 4.3: Unit tests for API endpoint permission enforcement
- Task 5.1: Audit log entries for freeze deactivation actions (already partially implemented)

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): audit log denied permission attempts for discard"
```
