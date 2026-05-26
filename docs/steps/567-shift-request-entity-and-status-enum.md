# 567 — ShiftRequest Entity and ShiftRequestStatus Enum

## Phase

Self-Service Scheduling — Domain Layer (Task 1.6)

## Purpose

Defines the `ShiftRequest` domain entity and `ShiftRequestStatus` enum for the self-service scheduling feature. A ShiftRequest represents a member's intent to claim a specific shift slot, transitioning through Pending → Approved/Rejected, and Approved → Cancelled states.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Scheduling/ShiftRequestStatus.cs` | Enum with values: Pending, Approved, Rejected, Cancelled |
| `apps/api/Jobuler.Domain/Scheduling/ShiftRequest.cs` | Domain entity implementing `AuditableEntity` + `ITenantScoped` with properties for slot assignment, admin override tracking, rejection/cancellation reasons, and state transition methods |

## Key decisions

- **Separate enum file**: Follows the same pattern as `ShiftSlotStatus.cs` — enum in its own file for clarity.
- **Private setters + static factory**: Matches existing conventions (`ShiftSlot.Create`, `Assignment.Create`) to enforce invariants at construction time.
- **State transition methods with guards**: `Approve()`, `Reject()`, and `Cancel()` enforce valid state transitions (e.g., only pending requests can be approved, only approved requests can be cancelled).
- **CancelledAt timestamp**: Recorded at cancellation time via `DateTime.UtcNow` inside the `Cancel()` method, supporting audit trail requirements (Req 8.5).
- **IsAdminOverride flag**: Supports admin manual override tracking (Req 10.3).

## How it connects

- **ShiftSlot** (task 1.5): A ShiftRequest references a ShiftSlot via `ShiftSlotId`
- **SchedulingCycle** (task 1.3): Requests are scoped to a cycle via `SchedulingCycleId`
- **ShiftRequestService** (task 7.3): Will use this entity to process request submissions and cancellations
- **WaitlistService** (task 9.1): Cancellation triggers waitlist processing
- **SwapRequest** (task 1.8): References ShiftRequests for swap proposals

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build
```

## What comes next

- Task 1.7: WaitlistEntry entity and WaitlistEntryStatus enum
- Task 1.8: SwapRequest entity and SwapRequestStatus enum
- Task 2.1: EF Core entity configurations for all new entities

## Git commit

```bash
git add -A && git commit -m "feat(self-service): add ShiftRequest entity and ShiftRequestStatus enum"
```
