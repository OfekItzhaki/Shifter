# Step 158 — GetPushSubscriptionStatusQuery

## Phase
Push Notifications — Backend Application Layer

## Purpose
Provides a MediatR query to check whether a user has any active push subscription in a given space. This is used by the frontend to display the correct toggle state in the push settings UI.

## What was built
- `apps/api/Jobuler.Application/Notifications/GetPushSubscriptionStatusQuery.cs`
  - `PushSubscriptionStatusResponse` record with `IsSubscribed` boolean
  - `GetPushSubscriptionStatusQuery` record with `SpaceId` and `UserId`
  - `GetPushSubscriptionStatusQueryHandler` that checks `AnyAsync` on `PushSubscriptions` filtered by userId and spaceId

## Key decisions
- Followed the exact same pattern as `GetNotificationsQuery` (same file structure, AppDbContext injection, AsNoTracking)
- Used `AnyAsync` instead of `Count > 0` for optimal performance — only needs to know if at least one subscription exists
- Response is a simple record with a single boolean — minimal payload for the status check endpoint

## How it connects
- Used by `PushSubscriptionsController` (task 4.1) via `GET /spaces/{spaceId}/push-subscriptions/status`
- Consumed by the frontend `usePushSubscription` hook to reflect current subscription state
- Validates Requirement 6.1 (subscription status check)

## How to run / verify
```bash
cd apps/api/Jobuler.Application
dotnet build
```

## What comes next
- Task 2.4: Property tests for subscription commands (optional)
- Task 4.1: PushSubscriptionsController wiring the GET status endpoint

## Git commit
```bash
git add -A && git commit -m "feat(push): add GetPushSubscriptionStatusQuery and handler"
```
