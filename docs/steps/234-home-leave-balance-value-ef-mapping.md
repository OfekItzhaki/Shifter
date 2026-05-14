# 234 — Home-Leave Balance Value EF Core Mapping

## Phase

Feature: Home-Leave Slider (Spec: `home-leave-slider`)

## Purpose

Map the `BalanceValue` domain property on `HomeLeaveConfig` to the `balance_value` database column via EF Core Fluent API configuration. This ensures the ORM correctly reads/writes the column added in the migration (step 233).

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/HomeLeaveConfigConfiguration.cs` | Added `builder.Property(c => c.BalanceValue).HasColumnName("balance_value");` |

## Key decisions

- Placed the mapping between `LeaveDurationHours` and `CreatedAt` to keep column order logical.
- No additional configuration (e.g., `HasDefaultValue`) needed here because the migration already defines the column default at the DB level.

## How it connects

- **Depends on**: Step 233 (migration adding `balance_value` column) and the domain entity update (task 1.2) that added the `BalanceValue` property.
- **Enables**: Application layer commands (task 2.x) that read/write `BalanceValue` through EF Core.

## How to run / verify

```bash
cd apps/api
dotnet build   # should succeed with no errors
```

## What comes next

- Task 2.1: Update `UpsertHomeLeaveConfigCommand` and handler to support `balance_value`.
- Task 2.4: Update GET endpoint to return `balance_value`.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-slider): map BalanceValue property in EF Core configuration"
```
