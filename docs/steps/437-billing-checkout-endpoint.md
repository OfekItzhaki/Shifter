# 437 — Billing Checkout Endpoint

## Phase

LemonSqueezy Billing Integration — API Layer

## Purpose

Expose the checkout session creation flow to the frontend by adding a `POST /spaces/{spaceId}/billing/groups/{groupId}/checkout` endpoint to the existing `BillingController`. This allows space admins with `BillingManage` permission to initiate a LemonSqueezy checkout session for a group.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Api/Controllers/BillingController.cs` | Added `CreateCheckout` endpoint that dispatches `CreateCheckoutCommand` via MediatR and returns `{ checkoutUrl }` |

## Key decisions

- **No controller-level permission check**: The `CreateCheckoutCommand` handler already performs the `BillingManage` permission check internally (consistent with `CancelSubscription` and `RenewSubscription` patterns in the same controller).
- **Response shape**: Returns `{ checkoutUrl: "..." }` as a JSON object rather than a raw string, matching the design document's architecture diagram.
- **Route pattern**: `POST /spaces/{spaceId}/billing/groups/{groupId}/checkout` — follows the existing group-scoped billing route convention.

## How it connects

- The controller is already `[Authorize]` at the class level, satisfying the authentication requirement.
- `CreateCheckoutCommand` (task 4.1) handles all business logic: permission check, group validation, active subscription guard, and LemonSqueezy API call.
- The frontend will call this endpoint to get a checkout URL and redirect the user to LemonSqueezy's hosted payment page.
- After payment, LemonSqueezy sends a webhook to the `LemonSqueezyWebhookController` (task 7.1) to activate the subscription.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

The endpoint is available at `POST /spaces/{spaceId}/billing/groups/{groupId}/checkout` with a valid JWT.

## What comes next

- Task 7.3: Add test-charge endpoint to BillingController
- Task 7.4: Write property tests for webhook signature verification and idempotency

## Git commit

```bash
git add -A && git commit -m "feat(billing): add checkout endpoint to BillingController"
```
