# 260 — Home-Leave EF Core Mode Column Mapping

## Phase

Home-Leave Overhaul — Database & Domain (Task 1.5)

## Purpose

Map the new mode-system columns added in migration 053 to the `HomeLeaveConfig` domain entity via EF Core Fluent API configuration. This ensures the ORM can read/write the new fields (`mode`, `base_days`, `home_days`, `emergency_freeze_active`, `emergency_use_for_scheduling`, `freeze_started_at`, `pre_freeze_mode`) to/from the database.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Domain/Groups/HomeLeaveConfig.cs` | Added new properties: `Mode`, `BaseDays`, `HomeDays`, `EmergencyFreezeActive`, `EmergencyUseForScheduling`, `FreezeStartedAt`, `PreFreezeMode` |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/HomeLeaveConfigConfiguration.cs` | Added column mappings for all new properties, with string conversions for `Mode` and `PreFreezeMode` enums |

## Key decisions

- **Enum-to-string conversion** — `Mode` and `PreFreezeMode` are stored as lowercase text (`"automatic"`, `"manual"`) matching the CHECK constraints in migration 053. Uses the same `ToString().ToLower()` / `Enum.Parse` pattern as other enums in the project (e.g., `TaskBurdenLevel`, `ScheduleRunTrigger`).
- **Nullable DateTime** — `FreezeStartedAt` is mapped as a nullable `DateTime?` since it's only populated when emergency freeze is active.
- **Properties added to domain entity** — The new properties were added with `private set` to maintain encapsulation. Full domain methods (task 1.4) will be added separately.

## How it connects

- Depends on: Migration 053 (task 1.1) which created the database columns, and `HomeLeaveMode` enum (task 1.3)
- Enables: Application layer services (tasks 2.1, 2.2) and API endpoints (tasks 4.x) that read/write the new fields
- The `SolverPayloadNormalizer` (task 6.1) will read these mapped properties to construct mode-based solver payloads

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build should succeed with no errors. The mapping will be exercised when the API reads/writes `HomeLeaveConfig` entities after the migration has been applied.

## What comes next

- Task 1.4: Add domain methods (`SetMode`, `SetRatio`, `ActivateEmergencyFreeze`, etc.)
- Task 2.1: `OptimalRatioCalculator` service
- Task 4.1: Updated `UpsertHomeLeaveConfigCommand` using the new fields

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): map new mode-system columns in EF Core configuration"
```
