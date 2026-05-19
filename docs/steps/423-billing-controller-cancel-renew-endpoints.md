# Step 423 — Billing Controller Cancel & Renew Endpoints

## Phase

Subscription Cancellation & Renewal — API Layer

## Purpose

Expose the cancel and renew subscription operations as HTTP endpoints so the frontend can trigger subscription lifecycle changes. The controller dispatches commands via MediatR; all business logic and permission checks remain in the command handlers per architecture rules.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/BillingController.cs` | Added `POST /spaces/{spaceId}/billing/groups/{groupId}/cancel` and `POST /spaces/{spaceId}/billing/groups/{groupId}/renew` endpoints |

## Key decisions

- **No permission checks in controller** — per architecture rules, controllers dispatch commands only. Authorization is enforced in `CancelSubscriptionCommandHandler` and `RenewSubscriptionCommandHandler` via `IPermissionService.RequirePermissionAsync`.
- **Return `Ok()` on success** — errors (404, 400, 403) are handled by `ExceptionHandlingMiddleware` which maps domain exceptions to HTTP status codes.
- **Extract `CurrentUserId` from JWT claims** — reuses the existing `CurrentUserId` property pattern already in the controller.

## How it connects

- Depends on `CancelSubscriptionCommand` and `RenewSubscriptionCommand` (implemented in tasks 3.1 and 3.2)
- Depends on `ExceptionHandlingMiddleware` for error mapping
- Used by the frontend billing UI to cancel/renew subscriptions
- The `[Authorize]` attribute at class level ensures all endpoints require authentication

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

The endpoints can be tested via HTTP:
- `POST /spaces/{spaceId}/billing/groups/{groupId}/cancel` — cancels the group subscription
- `POST /spaces/{spaceId}/billing/groups/{groupId}/renew` — renews the group subscription

## What comes next

- Task 5.2: `ExpireSubscriptionsJob` background service
- Task 5.3: Extend `GetSubscription` endpoint response mapping

## Git commit

```bash
git add -A && git commit -m "feat(billing): add cancel and renew endpoints to BillingController"
```
