# 635 — Remove Group-Level Subscription Logic

## Phase

Phase 10 — Billing Simplification

## Purpose

Subscriptions are now exclusively at the space level. A single subscription covers the entire space and all its groups. This step removes all active code paths that checked `GroupSubscription` for access control and makes the group-level webhook dispatch a no-op.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/ScheduleRunsController.cs` | Removed the group-level subscription fallback in the `Trigger` endpoint. Now unconditionally checks `SpaceSubscription.IsAccessGranted` regardless of whether a `GroupId` is provided. |
| `apps/api/Jobuler.Application/Billing/Commands/HandleWebhookCommand.cs` | Made `DispatchGroupLevelEventAsync` a no-op that logs a warning and returns immediately. Group-level webhook events (those with `group_id` in metadata) are no longer dispatched to handlers. |

## Key decisions

- **Did NOT delete** the `GroupSubscription` entity, EF configuration, migration files, or test files — historical data remains in the DB and tests document legacy behavior.
- **Did NOT delete** `CheckGroupSubscriptionQuery` — it's now dead code but kept alongside the entity for historical reference.
- **Did NOT modify** `ExpireSubscriptionsJob` or `SubscriptionCleanupService` — these manage lifecycle of historical group subscriptions (expiry/cleanup), not access control.
- **Did NOT modify** `BillingController` legacy endpoints — they already return 410 Gone via `RejectIfMigratedAsync` when a space subscription exists.
- **Did NOT modify** `TriggerRegenerationCommand` — it already only checks `SpaceSubscription`.
- **Frontend unchanged** — `lib/api/billing.ts` already only calls space-level endpoints.
- The subscription check in `ScheduleRunsController.Trigger` is now unconditional (not gated by `req.GroupId.HasValue`), matching the space-level billing model.

## How it connects

- `SpaceSubscription.IsAccessGranted` is now the single source of truth for billing access control across the entire application.
- The `BillingController` space-level endpoints (`/billing/subscription`, `/billing/checkout`, `/billing/cancel`, `/billing/renew`, `/billing/upgrade`) are the only active billing API.
- Legacy group-level billing endpoints return 410 Gone for migrated spaces.

## How to run / verify

1. Build the API: `dotnet build` in `apps/api/`
2. Trigger a solver run — should check space subscription only, no group-level fallback
3. Send a webhook with `group_id` in metadata — should log a warning and skip processing
4. Legacy group billing endpoints should still return 410 Gone

## What comes next

- Consider removing `CheckGroupSubscriptionQuery` entirely in a future cleanup pass
- Consider removing `ExpireSubscriptionsJob` group subscription logic once all historical subscriptions have been expired/cleaned up

## Git commit

```bash
git add -A && git commit -m "feat(billing): remove group-level subscription access control logic"
```
