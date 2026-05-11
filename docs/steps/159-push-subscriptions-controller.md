# Step 159 — Push Subscriptions Controller

## Phase

Push Notifications — API Layer

## Purpose

Provides REST endpoints for managing push notification subscriptions per user per space. Users can subscribe, unsubscribe, and check their subscription status through this controller.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/PushSubscriptionsController.cs` | Controller with POST (subscribe), DELETE (unsubscribe), and GET status endpoints. Request DTOs defined at the bottom of the file. |

## Key decisions

- Follows the exact same pattern as `NotificationsController` — constructor injection of `IMediator`, `CurrentUserId` extracted from JWT claims.
- Request DTOs (`CreatePushSubscriptionRequest`, `DeletePushSubscriptionRequest`) defined at the bottom of the controller file, matching the `GroupsController` convention.
- POST returns 201 (Created), DELETE returns 204 (NoContent), GET status returns 200 (Ok) with the subscription status response.
- No permission service needed — the controller only allows users to manage their own subscriptions (enforced by using `CurrentUserId` from the JWT token).
- Route scoped to `spaces/{spaceId:guid}/push-subscriptions` for tenant isolation.

## How it connects

- Dispatches `CreatePushSubscriptionCommand`, `DeletePushSubscriptionCommand`, and `GetPushSubscriptionStatusQuery` via MediatR (created in previous steps).
- Requires JWT Bearer authentication (`[Authorize]` attribute).
- Will be called by the frontend `usePushSubscription` hook (future task).

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
```

Build should succeed with no errors.

## What comes next

- Frontend `usePushSubscription` hook that calls these endpoints.
- Push notification sender integration with `NotificationService`.
- Input validation (FluentValidation) for subscription requests.

## Git commit

```bash
git add -A && git commit -m "feat(push): add PushSubscriptionsController with subscribe/unsubscribe/status endpoints"
```
