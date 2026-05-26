# 568 — Self-Service Application Layer Service Interfaces

## Phase
Self-Service Scheduling — Application Layer Foundation

## Purpose
Defines the application-layer service interfaces for the self-service scheduling feature. These interfaces establish the contracts for slot locking, shift request processing, slot availability querying, waitlist management, slot generation, and shift swapping. Infrastructure and application service implementations will depend on these contracts.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/SelfService/ISlotLockService.cs` | Interface for PostgreSQL advisory lock acquisition on shift slots. Used to prevent concurrent claims on the same slot. |
| `apps/api/Jobuler.Application/Scheduling/SelfService/IShiftRequestService.cs` | Interface for processing shift request submissions and cancellations with capacity/constraint validation. |
| `apps/api/Jobuler.Application/Scheduling/SelfService/ISlotAvailabilityEngine.cs` | Interface for querying available slots filtered by capacity, existing assignments, and time conflicts. |
| `apps/api/Jobuler.Application/Scheduling/SelfService/IWaitlistService.cs` | Interface for waitlist join/leave, slot release cascading, and expired offer processing. |
| `apps/api/Jobuler.Application/Scheduling/SelfService/ISlotGenerationService.cs` | Interface for idempotent slot generation from shift templates for a scheduling cycle. |
| `apps/api/Jobuler.Application/Scheduling/SelfService/IShiftSwapService.cs` | Interface for proposing, accepting, declining, and cancelling shift swaps between members. |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Models/ShiftRequestResult.cs` | Result record for shift request processing (success/rejection with alternatives). |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Models/CancellationResult.cs` | Result record for cancellation attempts. |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Models/AvailableSlotDto.cs` | DTO for available slot information returned by the availability engine. |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Models/WaitlistResult.cs` | Result record for waitlist join attempts. |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Models/SwapResult.cs` | Result record for swap operations (propose/accept). |

## Key decisions

- **Separate `SelfService` subfolder**: Groups all self-service interfaces and models together under `Scheduling/SelfService/` to avoid cluttering the existing solver-related interfaces.
- **Result records over exceptions**: Service methods return result objects (e.g., `ShiftRequestResult`, `SwapResult`) rather than throwing exceptions for business rule violations. This keeps control flow explicit and avoids exception-driven logic.
- **Default cancellation tokens**: All async methods accept `CancellationToken ct = default` for consistency with the existing codebase pattern.
- **`IReadOnlyList<T>` return types**: Availability engine returns immutable collections to prevent accidental mutation.
- **Timeout parameter on lock service**: `TryAcquireSlotLockAsync` accepts a `TimeSpan timeout` parameter rather than hardcoding the 5-second default, allowing flexibility for testing and future configuration.

## How it connects

- `ISlotLockService` → Implemented by `PostgresAdvisoryLockService` in Infrastructure (Task 2.3)
- `IShiftRequestService` → Implemented by `ShiftRequestService` in Application (Task 7.3)
- `ISlotAvailabilityEngine` → Implemented by `SlotAvailabilityEngine` in Application (Task 7.1)
- `IWaitlistService` → Implemented by `WaitlistService` in Application (Task 9.1)
- `ISlotGenerationService` → Implemented by `SlotGenerationService` in Application (Task 5.3)
- `IShiftSwapService` → Implemented by `ShiftSwapService` in Application (Task 11.1)
- All interfaces registered in DI container (Task 18.1)

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application/Jobuler.Application.csproj
```

Build succeeds with 0 warnings and 0 errors.

## What comes next

- Task 2.1: EF Core entity configurations for all new entities
- Task 2.3: `PostgresAdvisoryLockService` implementing `ISlotLockService`
- Tasks 5.3, 7.1, 7.3, 9.1, 11.1: Service implementations

## Git commit

```bash
git add -A && git commit -m "feat(self-service): define application-layer service interfaces"
```
