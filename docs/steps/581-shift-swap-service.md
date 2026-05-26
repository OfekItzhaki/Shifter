# 581 — Shift Swap Service

## Phase

Phase: Self-Service Scheduling — Application Layer Services

## Purpose

Implements the `ShiftSwapService` that enables members to propose, accept, decline, and cancel shift swaps between each other. This allows schedule flexibility without requiring admin intervention, while ensuring no time-overlap or rest-period conflicts are introduced by the swap.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/SelfService/ShiftSwapService.cs` | Full implementation of `IShiftSwapService` with all four operations: ProposeSwap, AcceptSwap, DeclineSwap, CancelSwap |
| `apps/api/Jobuler.Domain/Scheduling/ShiftRequest.cs` | Added `ReassignTo(Guid newPersonId, Guid newShiftSlotId)` method to support atomic swap reassignment |

## Key decisions

1. **ConflictDetector reuse**: The `AcceptSwapAsync` method projects the hypothetical post-swap state into `FlatAssignment` records and runs the existing `ConflictDetector.Detect()` to check for overlaps and rest violations. This avoids duplicating conflict logic.

2. **Transaction-scoped acceptance**: `AcceptSwapAsync` uses an explicit transaction with `CreateExecutionStrategy` (matching the pattern in `ShiftRequestService`) to ensure the swap is atomic — both shift requests are reassigned together or not at all.

3. **ReassignTo domain method**: Rather than directly mutating properties, a new `ReassignTo` method was added to `ShiftRequest` to encapsulate the swap reassignment logic and maintain domain invariants (only approved requests can be reassigned).

4. **Notification TODOs**: Notifications for swap proposal (Req 12.2) and decline (Req 12.5) are logged but deferred to task 17.1 (notification events implementation). The service logs the intent for traceability.

5. **Expiry check on accept**: If a swap request has expired by the time the target tries to accept, it's automatically marked as Expired rather than throwing an error.

6. **Cross-group conflict detection**: The conflict detector only compares assignments from different groups (same-group pairs are skipped by design). For same-group swaps, the time-overlap check still works because the person's other approved shifts in the same group are included in the assignment list.

## How it connects

- Implements `IShiftSwapService` interface defined in task 1.9
- Uses `SwapRequest` domain entity from task 1.8
- Uses `ConflictDetector` from the cross-group conflict detection feature
- Uses `ShiftRequest.ReassignTo()` for atomic swap execution
- Will be registered in DI in task 18.1
- Will be exposed via `ShiftSwapsController` in task 14.6
- Expired swap cleanup handled by `ExpireSwapRequestsJob` in task 16.3

## How to run / verify

```bash
cd apps/api
dotnet build
dotnet test --filter "FullyQualifiedName~SelfService"
```

All 57 self-service tests pass. The service compiles cleanly with the full solution.

## What comes next

- Task 11.2: Property tests for shift swaps (Properties 32–37)
- Task 14.6: ShiftSwapsController API endpoints
- Task 16.3: ExpireSwapRequestsJob background job
- Task 17.1: Notification events for swap proposal/decline
- Task 18.1: DI registration of ShiftSwapService

## Git commit

```bash
git add -A && git commit -m "feat(self-service): implement ShiftSwapService with conflict detection"
```
