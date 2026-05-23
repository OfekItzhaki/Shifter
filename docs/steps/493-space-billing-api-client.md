# 493 — Space Billing API Client

## Phase

Space-Level Billing — Frontend API Layer

## Purpose

Provides typed API client functions for the space billing endpoints so that frontend components (TrialBanner, SpaceBillingCard) can interact with the backend billing system without duplicating HTTP logic.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/billing.ts` | New API client module with `SpaceSubscriptionDto` interface and five exported functions: `getSpaceSubscription`, `createSpaceCheckout`, `cancelSpaceSubscription`, `renewSpaceSubscription`, `upgradeSpacePlan` |

## Key decisions

- **Follows existing pattern**: Uses the shared `apiClient` (axios instance) from `lib/api/client.ts`, matching the style of `spaces.ts`, `groups.ts`, etc.
- **Nullable return for subscription**: `getSpaceSubscription` returns `SpaceSubscriptionDto | null` since a space may not have a subscription yet.
- **Optional variantId on checkout**: `createSpaceCheckout` accepts an optional `variantId` for cases where the default variant is used.
- **Required variantId on upgrade**: `upgradeSpacePlan` requires `variantId` since the admin must explicitly select a higher tier.
- **Void returns for cancel/renew**: These endpoints return 204 No Content from the backend, so the client functions return `Promise<void>`.
- **CheckoutResponse type**: Shared between `createSpaceCheckout` and `upgradeSpacePlan` since both return a `{ checkoutUrl }` response.

## How it connects

- **Backend**: Maps to the endpoints defined in `BillingController` (task 10.1): `GET /spaces/{spaceId}/billing/subscription`, `POST .../checkout`, `POST .../cancel`, `POST .../renew`, `POST .../upgrade`
- **Frontend consumers**: Will be used by `TrialBanner` (task 13.2) and `SpaceBillingCard` (task 14.1, 14.2)
- **Auth**: Relies on the axios interceptor in `client.ts` to attach the Bearer token and handle 401 refresh

## How to run / verify

```bash
# TypeScript compilation check
cd apps/web && npx tsc --noEmit
```

The module exports are ready for import by downstream components. No runtime verification needed until the TrialBanner and SpaceBillingCard tasks are implemented.

## What comes next

- Task 13.2: Update `TrialBanner` component to use `getSpaceSubscription`
- Task 14.1: Create `SpaceBillingCard` component using all billing API functions
- Task 14.2: Wire action buttons (upgrade/cancel/renew) using `createSpaceCheckout`, `cancelSpaceSubscription`, `renewSpaceSubscription`, `upgradeSpacePlan`

## Git commit

```bash
git add -A && git commit -m "feat(billing): add space billing API client functions"
```
