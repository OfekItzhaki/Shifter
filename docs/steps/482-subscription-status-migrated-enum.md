# 482 — Add `Migrated` value to `SubscriptionStatus` enum

## Phase

Space-Level Billing — Domain Layer

## Purpose

The migration from group-level billing to space-level billing requires marking existing `GroupSubscription` records as "migrated" without deleting them. This step adds the `Migrated` value to the `SubscriptionStatus` enum so the migration command can set this status on legacy records.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Domain/Billing/GroupSubscription.cs` | Added `Migrated` to the `SubscriptionStatus` enum |

## Key decisions

- The `Migrated` value is appended at the end of the enum to avoid changing the ordinal values of existing members.
- No other fields or behavior on `GroupSubscription` are modified — this is a non-destructive addition.

## How it connects

- **Requirement 8.1**: The migration command (`MigrateToSpaceBillingCommand`) will set all existing `GroupSubscription` records to status `Migrated`.
- **Task 2.3**: The EF configuration for `GroupSubscription` uses string conversion, so the new enum value will serialize as `"Migrated"` without additional configuration changes.
- **Task 9.1**: The migration command depends on this enum value being available.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain
```

The project should compile without errors. The enum now includes: `Trialing, Active, PastDue, Canceled, Expired, Migrated`.

## What comes next

- Task 1.3–1.5: Property tests for the `SpaceSubscription` entity
- Task 2.3: Update EF configuration to ensure the `Migrated` value is handled in persistence

## Git commit

```bash
git add -A && git commit -m "feat(space-billing): add Migrated value to SubscriptionStatus enum"
```
