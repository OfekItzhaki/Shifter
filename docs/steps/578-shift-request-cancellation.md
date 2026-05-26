# 578 — Shift Request Cancellation

## Phase

Phase: Self-Service Scheduling — Application Layer (Task 7.5)

## Purpose

Implements the `CancelRequestAsync` method in `ShiftRequestService`, enabling members to cancel previously approved shift requests. This is essential for schedule flexibility — members can free up slots when their availability changes, which in turn triggers waitlist processing for other members waiting for that slot.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/SelfService/ShiftRequestService.cs` | Replaced the `CancelRequestAsync` stub with full implementation. Added `IWaitlistService?` as an optional constructor dependency. |

## Key decisions

1. **Optional waitlist service injection**: Since `IWaitlistService` is defined but not yet implemented (task 9.1), it's injected as a nullable parameter. If available, waitlist processing is triggered on cancellation; otherwise a debug log is emitted.
2. **Cancellation cutoff logic**: Per Req 8.2, cancellation is only blocked when BOTH conditions are true: the request window is closed AND the current time is past `shift_start - CancellationCutoffHours`. If the request window is still open, cancellation is always allowed.
3. **Under-scheduled detection**: After cancellation, if the member's remaining approved count drops below `MinShiftsPerCycle`, a log entry is recorded. Full notification integration is deferred to task 13.1 (CheckUnderScheduledMembersJob).
4. **Transaction scope**: The entire cancellation (status update + fill count decrement + save) happens within a single transaction. Waitlist processing occurs after SaveChanges but before CommitAsync to ensure data consistency.
5. **Reason validation first**: The reason length check happens before any DB access to fail fast on invalid input.

## How it connects

- **Depends on**: ShiftRequest domain entity (`Cancel` method), ShiftSlot entity (`DecrementFillCount`), SelfServiceConfig (for `CancellationCutoffHours` and `MinShiftsPerCycle`), SchedulingCycle (for `RequestWindowClosesAt`)
- **Used by**: Future `ShiftRequestsController` (task 14.4) will expose a POST cancel endpoint
- **Triggers**: `IWaitlistService.ProcessSlotReleasedAsync` when available (task 9.1)
- **Validated by**: Property tests 20–23 (task 7.6)

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --no-restore --filter "FullyQualifiedName~Scheduling"
```

## What comes next

- Task 7.6: Property tests for cancellation (Properties 20–23)
- Task 9.1: WaitlistService implementation (will be called by this method)
- Task 13.1: Under-scheduled member detection and notification
- Task 14.4: ShiftRequestsController with cancel endpoint

## Git commit

```bash
git add -A && git commit -m "feat(self-service): implement shift request cancellation logic"
```
