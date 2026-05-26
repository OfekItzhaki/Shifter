# 576 — Slot Availability Engine

## Phase

Self-Service Scheduling — Application Layer (Task 7.1)

## Purpose

Implements the `SlotAvailabilityEngine` that allows group members to query which shift slots are available for them to request in a given scheduling cycle. The engine filters out full slots, slots the member already has requests for, and slots that overlap in time with the member's existing approved shifts (using exclusive endpoints for adjacency).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/SelfService/Models/SlotAvailabilityResult.cs` | Response wrapper DTO containing the slot list, a read-only flag, and an optional message for when the request window is closed |
| `apps/api/Jobuler.Application/Scheduling/SelfService/ISlotAvailabilityEngine.cs` | Updated interface to return `SlotAvailabilityResult` instead of raw list, supporting the read-only flag requirement |
| `apps/api/Jobuler.Application/Scheduling/SelfService/SlotAvailabilityEngine.cs` | Implementation of `ISlotAvailabilityEngine` with capacity filtering, member exclusion, time-overlap detection, and sorting |
| `apps/api/Jobuler.Tests/Scheduling/SlotAvailabilityEngineTests.cs` | 11 unit tests covering all requirements: capacity filtering, member exclusion, overlap detection with exclusive endpoints, sorting, read-only flag, and response completeness |

## Key decisions

1. **Response wrapper DTO**: Created `SlotAvailabilityResult` to wrap the slot list with `IsReadOnly` and `Message` fields, satisfying requirement 7.5 without polluting individual slot DTOs.
2. **Exclusive endpoint overlap**: A shift ending at time T does NOT conflict with a shift starting at time T (`approved.StartTime < slot.EndTime && approved.EndTime > slot.StartTime`).
3. **In-memory filtering**: Loads all candidate slots and member data in bulk queries, then filters in memory. This avoids complex SQL joins and keeps the logic testable with InMemory provider.
4. **Empty result for missing cycle**: Returns an empty result (not an error) when the cycle doesn't exist, per requirement 7.6.

## How it connects

- **Upstream**: Uses `AppDbContext` to query `ShiftSlots`, `ShiftRequests`, `GroupTasks`, and `SchedulingCycles`
- **Downstream**: Will be consumed by `ShiftSlotsController` (task 14.3) and `ShiftRequestService` (task 7.3) for alternative slot suggestions
- **DI Registration**: Will be registered in task 18.1

## How to run / verify

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~SlotAvailabilityEngineTests"
```

All 11 tests should pass.

## What comes next

- Task 7.2: Property tests for slot availability (Properties 18 and 19)
- Task 7.3: ShiftRequestService implementation (uses SlotAvailabilityEngine for alternatives)
- Task 14.3: ShiftSlotsController (exposes the engine via API)
- Task 18.1: DI registration

## Git commit

```bash
git add -A && git commit -m "feat(self-service): implement SlotAvailabilityEngine with filtering and read-only flag"
```
