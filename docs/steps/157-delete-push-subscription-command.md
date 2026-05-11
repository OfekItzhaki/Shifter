# Step 157 — Delete Push Subscription Command

## Phase
Push Notifications — Backend Application Layer

## Purpose
Implements the MediatR command and handler for removing a push subscription. This enables users to unsubscribe from push notifications on a specific device within a space.

## What was built
- `apps/api/Jobuler.Application/Notifications/DeletePushSubscriptionCommand.cs` — MediatR command record (`SpaceId`, `UserId`, `Endpoint`) and handler that queries by the (UserId, SpaceId, Endpoint) tuple and removes the matching subscription. Returns success even if no subscription exists (idempotent delete).

## Key decisions
- **Idempotent delete**: If no matching subscription is found, the handler returns without error. This matches the design requirement (Requirement 2.3) and ensures the controller can always return 204.
- **Same pattern as DismissNotificationCommand**: Uses `AppDbContext` directly, `FirstOrDefaultAsync` for lookup, and `Remove` + `SaveChangesAsync` for deletion.
- **IRequest (void)**: The command returns nothing since the controller responds with 204 No Content.

## How it connects
- Called by `PushSubscriptionsController.Unsubscribe` (task 4.1) via MediatR dispatch
- Queries the `PushSubscriptions` DbSet registered in `AppDbContext` (task 1.2)
- Complements `CreatePushSubscriptionCommand` (task 2.1) for the full subscription lifecycle

## How to run / verify
```bash
cd apps/api/Jobuler.Application
dotnet build
```

## What comes next
- Task 2.3: `GetPushSubscriptionStatusQuery` — checks if a subscription exists for a user in a space
- Task 4.1: `PushSubscriptionsController` — wires the DELETE endpoint to this command

## Git commit
```bash
git add -A && git commit -m "feat(push): implement DeletePushSubscriptionCommand and handler"
```
