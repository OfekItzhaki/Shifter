# 345 — Cancel Home Leave Command Parameters

## Phase

Home Leave Protection — Emergency Recall Enhancement

## Purpose

Enhance the `CancelHomeLeaveCommand` to support the emergency recall workflow by adding confirmation, reason, and expected return time parameters. This enables admins to provide context when recalling someone from home leave, and ensures the recall is always an explicit, confirmed action.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/HomeLeave/Commands/CancelHomeLeaveCommand.cs` | Added `Confirmed` (bool), `Reason` (string?, max 500), `ExpectedReturnAt` (DateTime?) parameters to the command record. Added `NotificationSent` (bool) to `CancelHomeLeaveResult`. Removed `IsDerived == true` constraint so both manual and derived AtHome windows can be cancelled. |
| `apps/api/Jobuler.Api/Controllers/HomeLeaveConfigController.cs` | Updated the `CancelHomeLeave` endpoint to pass `Confirmed: true` to the command (full controller enhancement is a later task). |

## Key decisions

- **Confirmed as required positional parameter**: Placed after `RequestingUserId` to enforce that callers always explicitly pass a confirmation value. Optional parameters (`Reason`, `ExpectedReturnAt`) use default values.
- **NotificationSent defaults to false**: The notification service integration is wired in a later task (9.1). For now, the result always reports `NotificationSent: false`.
- **Removed IsDerived constraint**: Per design, recall should work on both manual and derived AtHome windows. This allows admins to cancel any type of home leave.
- **Controller passes `Confirmed: true`**: The existing DELETE endpoint continues to work without breaking. The full controller enhancement (accepting body params) is task 10.1.

## How it connects

- **Task 4.2** adds FluentValidation for the new parameters (Reason max length, Confirmed must be true).
- **Task 4.3** adds confirmation and permission checks in the handler.
- **Task 5.x** implements the notification service that will set `NotificationSent` to true.
- **Task 9.1** wires the notification dispatch into the handler.
- **Task 10.1** updates the API endpoint to accept the new parameters from the request body.

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build should succeed with no new warnings.

## What comes next

- `CancelHomeLeaveCommandValidator` (task 4.2) — validates Reason length and Confirmed flag
- Handler confirmation/permission logic (task 4.3)
- Property test for reason length validation (task 4.4)

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-protection): add Reason, ExpectedReturnAt, Confirmed params to CancelHomeLeaveCommand"
```
