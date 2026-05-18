# 353 — Recall Notification Dispatch Wiring

## Phase

Home Leave Protection — Emergency Recall Notification Integration

## Purpose

Wire the `IRecallNotificationService` into the `CancelHomeLeaveCommand` handler so that after a successful recall (truncation or deletion of an AtHome window), the recalled person receives a push notification and email with the admin's name, reason, and expected return time. Also register the service in the DI container.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Application/HomeLeave/Commands/CancelHomeLeaveCommand.cs` | Injected `IRecallNotificationService`, added `SendRecallNotificationSafeAsync` helper that resolves admin display name and dispatches notification, set `NotificationSent` on result |
| `Jobuler.Api/Program.cs` | Registered `IRecallNotificationService → RecallNotificationService` as scoped in DI container |

## Key decisions

- **Admin name resolution**: The handler resolves the admin's `DisplayName` from the `Users` table using the `RequestingUserId`. Falls back to `"Admin"` if the user is not found.
- **Non-blocking notification**: The notification call happens after the audit log, keeping the same fire-and-forget pattern. The `RecallNotificationService` already handles retries and email failures internally.
- **Result reflects push success**: `NotificationSent` on the result is set based on the return value of `SendRecallNotificationAsync` (which returns `true` only if push delivery succeeded).

## How it connects

- Depends on `IRecallNotificationService` (task 5.1) and `RecallNotificationService` (task 5.2) which were already implemented.
- Depends on the enhanced `CancelHomeLeaveCommand` with `Reason` and `ExpectedReturnAt` parameters (task 4.1).
- Depends on the audit logging integration (task 6.1) — notification is dispatched after audit.
- The API layer (task 10.1) will pass the recall parameters through to this handler.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Build should succeed with 0 errors.

## What comes next

- Task 10.1: Update HomeLeaveController endpoint to accept enhanced recall parameters and wire the full flow end-to-end.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): wire recall notification dispatch into CancelHomeLeaveCommand handler"
```
