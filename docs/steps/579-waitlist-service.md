# 579 — Waitlist Service

## Phase
Self-Service Scheduling — Application Layer

## Purpose
Implements the `WaitlistService` that manages the waitlist for full shift slots. When a slot is at capacity, members can join a FIFO-ordered waitlist. When a slot becomes available (via cancellation or admin removal), the service offers it to the next waiting member with a configurable acceptance period. Expired offers cascade to the next member automatically.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/SelfService/WaitlistService.cs` | Full implementation of `IWaitlistService` with JoinWaitlistAsync, LeaveWaitlistAsync, ProcessSlotReleasedAsync, and ProcessExpiredOffersAsync |

## Key decisions

- **FIFO ordering via Position field**: New entries get `max(position) + 1` among active entries for the slot, ensuring first-come-first-served ordering.
- **Duplicate prevention**: Checks for existing `Waiting` or `Offered` entries before allowing a join.
- **Cascade on leave with active offer**: If a member leaves while they have an active offer, it's treated as a decline and the slot is offered to the next waiting member (Req 9.6).
- **Configurable offer duration**: Uses `SelfServiceConfig.WaitlistOfferMinutes` (default 60 minutes) to set the `ExpiresAt` on offers.
- **No active offer duplication**: `ProcessSlotReleasedAsync` skips if there's already an active offer for the slot.
- **Follows existing patterns**: Uses `AppDbContext` directly, `TimeProvider` for testable time, and `ILogger<T>` — consistent with `ShiftRequestService`.

## How it connects

- Called by `ShiftRequestService.CancelRequestAsync` when a cancellation frees up slot capacity
- Called by admin override removal (task 10.1) when an admin removes a member
- `ProcessExpiredOffersAsync` is called by the `ProcessExpiredWaitlistOffersJob` background job (task 16.2)
- Uses `WaitlistEntry` domain entity methods: `Offer()`, `Accept()`, `Expire()`, `Decline()`, `Remove()`

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

The service compiles cleanly. Full integration testing requires the background job and controller wiring (tasks 16.2, 14.5).

## What comes next

- Task 9.2: Property tests for waitlist (FIFO ordering, offer on release, expired cascade, Max_Shifts validation, no duplicates)
- Task 10.1: Admin override commands that trigger waitlist processing
- Task 14.5: WaitlistController API endpoints
- Task 16.2: ProcessExpiredWaitlistOffersJob background job

## Git commit

```bash
git add -A && git commit -m "feat(self-service): implement WaitlistService for slot waitlist management"
```
