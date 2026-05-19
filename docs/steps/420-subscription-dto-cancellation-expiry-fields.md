# 420 — Extend SubscriptionDto with Cancellation & Expiry Fields

## Phase

Phase: Subscription Cancellation & Renewal — Application Layer

## Purpose

Expose `CanceledAt` and `PeriodEndsAt` in the subscription status response so the frontend can display cancellation date and when the billing period (grace window) ends. This enables space owners to make informed billing decisions.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Billing/Queries/GetSubscriptionQuery.cs` | Added `CanceledAt` (DateTime?) and `PeriodEndsAt` (DateTime?) parameters to the `SubscriptionDto` record |
| `apps/api/Jobuler.Application/Billing/Queries/GetSubscriptionHandler.cs` | Updated the handler to map `sub.CanceledAt` → `CanceledAt` and `sub.CurrentPeriodEnd` → `PeriodEndsAt` |

## Key decisions

- `PeriodEndsAt` maps from the entity's `CurrentPeriodEnd` field — the DTO uses a more user-facing name that clearly communicates "when does my access end"
- Both fields are nullable (`DateTime?`) because they are only meaningful for canceled/expired subscriptions
- No breaking change to the API contract — new fields are additive (nullable) so existing consumers continue to work

## How it connects

- Satisfies Requirements 4.1, 4.2, 4.3 (Subscription Status Visibility)
- The `BillingController` already returns `SubscriptionDto?` from `GetSubscriptionQuery` — no controller changes needed
- Frontend can now display cancellation date and grace window end date in the billing UI

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

The build should succeed with no new errors.

## What comes next

- Task 5.3: Extend the `GET /spaces/{spaceId}/billing/groups/{groupId}/subscription` endpoint response mapping (already wired through MediatR)
- Property test 5.4: Verify status query returns correct fields per subscription state

## Git commit

```bash
git add -A && git commit -m "feat(billing): extend SubscriptionDto with canceledAt and periodEndsAt fields"
```
