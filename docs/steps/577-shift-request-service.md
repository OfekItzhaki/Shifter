# 577 — Shift Request Service (Request Submission)

## Phase

Self-Service Scheduling — Application Layer

## Purpose

Implements the `ShiftRequestService` class that processes shift request submissions for self-service scheduling. This is the core transactional flow that members use to claim shift slots, enforcing concurrency safety via PostgreSQL advisory locks and validating all business constraints before approving or rejecting requests.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/SelfService/ShiftRequestService.cs` | Full implementation of `IShiftRequestService.ProcessRequestAsync` with advisory locking, validation chain, and alternative slot suggestions on rejection |

## Key decisions

1. **Transaction-scoped advisory lock**: The entire request processing happens within a single database transaction. The advisory lock is acquired first, ensuring serialized access to the slot's capacity state.
2. **Validation order**: Lock → Slot exists & Open → Request window open → No duplicate → Max_Shifts check → Capacity check. This order ensures the cheapest checks happen first after the lock.
3. **Alternatives on full capacity**: When a slot is full, up to 5 alternative slots for the same day are returned, filtered by the member's existing requests and time overlaps (exclusive endpoints).
4. **Execution strategy pattern**: Uses `_db.Database.CreateExecutionStrategy()` to handle transient failures, consistent with the existing `PublishSandboxCommand` pattern.
5. **TimeProvider injection**: Uses the .NET 8 `TimeProvider` abstraction for testable time-dependent logic (request window validation).
6. **CancelRequestAsync stub**: Left as `NotImplementedException` since task 7.5 handles cancellation logic.

## How it connects

- Implements `IShiftRequestService` (defined in task 1.9)
- Uses `ISlotLockService` (implemented in task 2.3 as `PostgresAdvisoryLockService`)
- Reads `ShiftSlot`, `SchedulingCycle`, `SelfServiceConfig`, `ShiftRequest` entities (tasks 1.2–1.6)
- Returns `ShiftRequestResult` and `AvailableSlotDto` models (defined in task 1.9)
- Will be wired into DI in task 18.1 and called from `ShiftRequestsController` in task 14.4

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with zero new warnings.

## What comes next

- Task 7.4: Property tests for shift request processing (Properties 9–12, 16, 31)
- Task 7.5: Implement `CancelRequestAsync` in the same service
- Task 18.1: Register `ShiftRequestService` in DI container

## Git commit

```bash
git add -A && git commit -m "feat(self-service): implement ShiftRequestService request submission"
```
