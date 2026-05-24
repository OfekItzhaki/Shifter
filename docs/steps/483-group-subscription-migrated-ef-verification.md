# 483 — GroupSubscription EF Configuration Supports Migrated Status

## Phase

Space-Level Billing — Infrastructure Layer

## Purpose

Verify that the existing `GroupSubscription` EF configuration correctly handles the new `Migrated` enum value added to `SubscriptionStatus` in task 1.2. The migration workflow (Requirement 8.1) sets all existing group subscriptions to status "migrated" without deleting them, so the persistence layer must serialize/deserialize this value correctly.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Billing/GroupSubscriptionMigratedStatusTests.cs` | Unit tests verifying the `Migrated` enum value round-trips through EF's string conversion |

## Key decisions

- **No code change needed**: The existing `GroupSubscriptionConfiguration` uses `.HasConversion<string>()` which is EF Core's generic enum-to-string value converter. It calls `ToString()` for serialization and `Enum.Parse<T>()` for deserialization, automatically handling any new enum values without configuration changes.
- **Verification via tests**: Instead of modifying the configuration, we added unit tests that prove the `Migrated` value serializes to `"Migrated"` and deserializes back correctly, giving confidence for the migration command (task 9.1).

## How it connects

- **Depends on**: Task 1.2 (added `Migrated` to `SubscriptionStatus` enum)
- **Used by**: Task 9.1 (`MigrateToSpaceBillingCommand`) which sets group subscriptions to `Migrated` status
- **EF configuration**: `Jobuler.Infrastructure/Persistence/Configurations/BillingConfiguration.cs` — `GroupSubscriptionConfiguration` class

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~GroupSubscriptionMigratedStatusTests" --no-restore
```

All 5 tests should pass, confirming:
1. `SubscriptionStatus.Migrated.ToString()` → `"Migrated"`
2. `Enum.Parse<SubscriptionStatus>("Migrated")` → `SubscriptionStatus.Migrated`
3. All enum values round-trip through string conversion
4. `GroupSubscription.UpdateStatus(SubscriptionStatus.Migrated)` works correctly

## What comes next

- Task 3.x: Application layer interfaces and services
- Task 9.1: `MigrateToSpaceBillingCommand` which uses `UpdateStatus(SubscriptionStatus.Migrated)` on all existing group subscriptions

## Git commit

```bash
git add -A && git commit -m "feat(space-billing): verify GroupSubscription EF config supports Migrated status"
```
