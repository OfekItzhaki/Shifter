# 592 — Self-Service Notification Events

## Phase

Self-Service Scheduling — Notifications

## Purpose

Wire actual notification delivery (in-app + push) into the self-service scheduling services that previously had TODO/log-only placeholders. Ensures members receive timely notifications for all self-service lifecycle events as specified in Requirements 13.1–13.7.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Application/Scheduling/SelfService/ShiftRequestService.cs` | Added `IPushNotificationSender` dependency; implemented `SendRequestApprovedNotificationAsync` (Req 13.1) and `SendRequestRejectedNotificationAsync` (Req 13.2) with in-app persistence + push delivery |
| `Jobuler.Application/Scheduling/SelfService/WaitlistService.cs` | Added `IPushNotificationSender` dependency; implemented `SendWaitlistOfferNotificationAsync` (Req 13.3) with acceptance deadline in notification body |
| `Jobuler.Application/Scheduling/SelfService/ShiftSwapService.cs` | Added `IPushNotificationSender` dependency; implemented `SendSwapProposalNotificationAsync` (Req 13.5) and `SendSwapDeclinedNotificationAsync` (Req 12.5); replaced TODO placeholders |
| `Jobuler.Application/Scheduling/SelfService/Commands/AcceptWaitlistOfferCommand.cs` | Added `IPushNotificationSender` dependency; implemented `SendWaitlistAcceptedNotificationAsync` (Req 13.1 via waitlist) |
| `Jobuler.Infrastructure/Scheduling/ExpireSwapRequestsJob.cs` | Replaced log-only notification intent with actual in-app + push notification delivery for expired swap requests (Req 12.7) |

## Key decisions

1. **In-app first, push second (Req 13.7)**: Every notification method persists the in-app `Notification` entity and calls `SaveChangesAsync` before attempting push delivery. Push failures are caught and logged without affecting the persisted notification.

2. **Outer try/catch on notification methods**: Notification delivery failures never propagate to the caller — the core business operation (approve, reject, offer, swap) always succeeds even if notification delivery fails entirely.

3. **Consistent event type naming**: All event types follow the `self_service.*` prefix pattern (e.g., `self_service.request_approved`, `self_service.request_rejected`, `self_service.waitlist_offer`, `self_service.swap_proposal_received`, `self_service.swap_declined`, `self_service.swap_expired`).

4. **Rich metadata JSON**: Each notification includes structured metadata (slot IDs, dates, times, task names, URLs) enabling the frontend to render actionable notification cards.

5. **Already-implemented events preserved**: `NotifyRequestWindowOpenJob` (Req 13.4) and `CheckUnderScheduledMembersCommand` (Req 13.6) were already fully implemented — no changes needed.

## How it connects

- Uses existing `INotificationService` (for admin notifications) and `IPushNotificationSender` (for member push delivery)
- Uses existing `Notification.Create()` factory method for in-app notification persistence
- Follows the same pattern established by `CheckUnderScheduledMembersCommand` and `NotifyRequestWindowOpenJob`
- All new constructor dependencies (`IPushNotificationSender`) are already registered in DI

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --no-build --filter "FullyQualifiedName~SelfService"
```

All 57 self-service tests pass. The notification methods are wrapped in try/catch so they don't affect existing test behavior.

## What comes next

- Task 18.1: Register all new services in DI container (final wiring)
- Frontend notification rendering for the new event types
- Integration tests for notification delivery

## Git commit

```bash
git add -A && git commit -m "feat(self-service): wire notification events for all self-service lifecycle (13.1-13.7)"
```
