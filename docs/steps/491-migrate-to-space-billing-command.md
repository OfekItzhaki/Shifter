# 491 — Migrate to Space Billing Command

## Phase

Space-Level Billing — Migration

## Purpose

Provides a one-time admin command to migrate all existing group-level subscriptions to the new space-level billing model. This is the core migration logic that:
- Marks all existing `GroupSubscription` records as "Migrated" without deleting them
- Creates `SpaceSubscription` records for each space based on its group subscription state
- Processes in configurable batches with per-batch transactions for resilience

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/MigrateToSpaceBillingCommand.cs` | Command record with `BatchSize` parameter, `MigrationResult` DTO, and handler implementing batch-based migration logic |

## Key decisions

1. **Batch processing with per-batch transactions** — Each batch is wrapped in its own transaction. If a batch fails, only that batch rolls back; already-completed batches remain committed and subsequent batches continue processing (Req 8.5).

2. **Determine subscription type before marking migrated** — The handler reads the `Status` of each `GroupSubscription` before calling `UpdateStatus(Migrated)` to correctly determine whether the space should get an Active or Trialing `SpaceSubscription`.

3. **Skip spaces with existing SpaceSubscriptions** — Spaces that already have a `SpaceSubscription` are excluded from migration entirely (Req 8.4), making the command safe to re-run.

4. **Active status uses latest period dates** — When a space has multiple active/trialing group subscriptions, the one with the latest `CurrentPeriodEnd` is used to set the space subscription's period dates (Req 8.2).

5. **No user permission check in handler** — This is an admin-only command restricted at the API layer. The handler itself has no `IPermissionService` dependency.

6. **MigrationResult DTO** — Returns structured results (processed count, active/trialing counts, failed batches, error messages) for observability.

## How it connects

- Uses `SpaceSubscription.CreateTrial()` and `Activate()` from the domain entity (Task 1.1)
- Uses `GroupSubscription.UpdateStatus()` to mark records as Migrated (Task 1.2)
- Uses `ITrialDurationCache` to get trial days for spaces without active subscriptions (Task 3.4)
- Will be exposed via an admin API endpoint in Task 10.2
- Property test for migration correctness in Task 9.2

## How to run / verify

```bash
dotnet build apps/api/Jobuler.Application/Jobuler.Application.csproj
```

The command will be invoked via an admin endpoint (Task 10.2) or directly via MediatR in tests.

## What comes next

- Task 9.2: Property test for migration correctness (Property 14)
- Task 10.2: Admin API endpoint to trigger the migration

## Git commit

```bash
git add -A && git commit -m "feat(billing): add MigrateToSpaceBillingCommand with batch processing"
```
