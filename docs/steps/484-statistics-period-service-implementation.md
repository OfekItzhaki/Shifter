# 484 — Statistics Period Service Implementation

## Phase

Space-Level Billing — Application/Infrastructure Layer

## Purpose

Implements `IStatisticsPeriodService` (defined in `Jobuler.Application/Billing/`) in the Infrastructure layer. This service manages statistics period boundaries in response to subscription lifecycle events (trial start, trial expiry, subscription activation, subscription expiry, period renewal). Each event closes active `SubscriptionPeriod` records and/or opens new ones for all groups in the space, ensuring cumulative tracking data is partitioned by billing cycle.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Infrastructure/Billing/StatisticsPeriodService.cs` | Full implementation of `IStatisticsPeriodService` with five lifecycle methods |
| `Jobuler.Api/Program.cs` | DI registration: `AddScoped<IStatisticsPeriodService, StatisticsPeriodService>()` |

## Key decisions

1. **EF Entry override for boundary dates** — The `SubscriptionPeriod.Create()` factory sets `StartsAt = DateTime.UtcNow` and `Close()` sets `EndsAt = DateTime.UtcNow`. Since lifecycle events need specific boundary dates, we use `_db.Entry(period).Property(p => p.StartsAt).CurrentValue = boundary` (same pattern as `BackfillSubscriptionPeriodsCommand`). This avoids modifying the domain entity for this task.

2. **Scoped registration** — Registered as `Scoped` to match the `AppDbContext` lifetime and other Infrastructure services in the project.

3. **Active groups only** — Queries groups with `DeletedAt == null` to skip soft-deleted groups, matching the pattern used throughout the codebase.

4. **Reconciliation logging** — When no groups exist in a space at the time of a lifecycle event, the service logs a warning and returns early (Requirement 7.6). This allows reconciliation when a group is later added.

5. **Separate SaveChanges calls** — Close and open operations each call `SaveChangesAsync` separately to ensure the close is persisted before new periods are created, preventing potential conflicts.

## How it connects

- **Consumed by**: Subscription lifecycle commands (`HandleSpaceSubscriptionCreatedCommand`, `ExpireSpaceSubscriptionsCommand`, space creation flow, etc.)
- **Depends on**: `AppDbContext` (for `Groups`, `SubscriptionPeriods` DbSets), `SubscriptionPeriod` domain entity
- **Related**: `PeriodManager` in `Jobuler.Infrastructure/Scheduling/` handles single-group period operations; this service handles space-wide bulk operations triggered by billing events

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

The service is integration-tested via the property test in task 3.5 (Property 13: Lifecycle events rotate statistics periods).

## What comes next

- Task 3.4: `TrialDurationCache` implementation
- Task 3.5: Property test for statistics period rotation (Property 13)
- Task 5.x: Subscription commands that call this service

## Git commit

```bash
git add -A && git commit -m "feat(billing): implement StatisticsPeriodService for subscription lifecycle period rotation"
```
