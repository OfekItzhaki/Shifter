# 251 — Backfill Subscription Periods Command

## Phase

Phase 2 — Backfill Script (Cumulative Tracking and Periods)

## Purpose

Provides a one-time backfill operation that creates initial `SubscriptionPeriod` records for all existing groups. This is needed because the subscription periods system is being introduced after groups already exist — existing groups need a period to partition their cumulative data correctly.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Commands/BackfillSubscriptionPeriodsCommand.cs` | MediatR command + handler that queries all active groups, checks for existing periods (idempotent), and creates periods with `starts_at` set to the group's subscription start date or creation date |
| `apps/api/Jobuler.Api/Controllers/PlatformController.cs` | Added `POST /platform/backfill/subscription-periods` endpoint (platform admin only) |

## Key decisions

- **Idempotent**: Groups that already have a subscription period are skipped, making the command safe to run multiple times.
- **StartsAt override**: The `SubscriptionPeriod.Create()` factory sets `StartsAt = UtcNow`, but for backfill we override it via EF's change tracker to use the historical date (subscription start or group creation).
- **Subscription date preference**: If a group has a `GroupSubscription` record, we use the earliest subscription's `CreatedAt` as the period start. Otherwise, we fall back to the group's own `CreatedAt`.
- **Platform admin guard**: Uses the same pattern as the existing `/platform/stats` endpoint — checks `user.IsPlatformAdmin` before proceeding.

## How it connects

- Depends on: `SubscriptionPeriod` domain entity (task 2.1), EF Core configuration (task 4.1), schema migration (task 1.1)
- Used by: Subsequent backfill tasks (5.2, 5.3) that need a `period_id` to associate snapshots and cumulative records with

## How to run / verify

1. Build: `dotnet build` from `apps/api/` — should succeed with 0 errors
2. After deploying, call `POST /platform/backfill/subscription-periods` with a platform admin JWT
3. Response: `{ "created": N, "skipped": M }` — created + skipped should equal total active groups
4. Running again should return `{ "created": 0, "skipped": N+M }` (idempotent)

## What comes next

- Task 5.2: Backfill daily snapshots from existing published schedule versions
- Task 5.3: Backfill cumulative records from presence windows and snapshots

## Git commit

```bash
git add -A && git commit -m "feat(cumulative): backfill subscription periods command and endpoint"
```
