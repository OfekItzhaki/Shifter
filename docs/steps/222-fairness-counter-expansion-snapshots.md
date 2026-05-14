# 222 — Fairness Counter Expansion & Snapshots

## Phase

Statistics Overhaul — Phase 2: Enhanced Statistics Backend

## Purpose

Expand the `FairnessCounter` entity with new time-window fields (30d hard, 7d/14d/30d easy, 7d/14d/30d burden scores) and create the `FairnessCounterSnapshot` entity + migration for historical time-series data. This enables per-person trend graphs and the historical stats API.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Scheduling/FairnessCounter.cs` | Added `HardTasks30d`, `EasyTasks7d/14d/30d`, `BurdenScore7d/14d/30d` properties. Updated `Update()` method with new signature and non-negative validation. Kept `DislikedHatedScore7d` for backward compat. |
| `apps/api/Jobuler.Domain/Scheduling/FairnessCounterSnapshot.cs` | New domain entity for daily snapshots: SpaceId, PersonId, SnapshotDate, TotalAssignments, HardCount, NormalCount, EasyCount, BurdenScore. |
| `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` | Added `DbSet<FairnessCounterSnapshot>` |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/SchedulingConfiguration.cs` | Updated `FairnessCounterConfiguration` with new column mappings. Added `FairnessCounterSnapshotConfiguration`. |
| `apps/api/Jobuler.Application/Scheduling/Commands/UpdateFairnessCountersCommand.cs` | Updated to use new `Update()` signature with easy/hard/burden score computations per time window. |
| `infra/migrations/046_fairness_counter_snapshots.sql` | Adds new columns to `fairness_counters`, creates `fairness_counter_snapshots` table with RLS, unique constraint, and index. |

## Key decisions

- Kept `DislikedHatedScore7d` property on the entity for backward compatibility (the column still exists in DB from before the rename migration).
- Burden score formula: `(hard×3) − (easy×1)` — normal tasks contribute 0.
- Non-negative validation in `Update()` throws `InvalidOperationException` per architecture rules (maps to 400 via middleware).
- Used `IF NOT EXISTS` in migration for idempotency.
- RLS policy on snapshots table uses `app.current_space_id` session variable per security rules.

## How it connects

- **Upstream**: Migration 045 renamed burden levels; this builds on that foundation.
- **Downstream**: Task 3.3 will update `UpdateFairnessCountersCommand` to persist snapshots. Task 3.4 will query snapshots for the historical API.
- The `FairnessCounterSnapshot` entity is the data source for the historical stats endpoint and frontend graphs.

## How to run / verify

```bash
# Build
cd apps/api && dotnet build

# Run tests (excluding integration tests that need solver service)
dotnet test --filter "FullyQualifiedName!~SolverEndToEnd&FullyQualifiedName!~SolverWorkerPipeline"

# Apply migration (requires running PostgreSQL)
psql -U postgres -d jobuler -f infra/migrations/046_fairness_counter_snapshots.sql
```

## What comes next

- Task 3.3: Update `UpdateFairnessCountersCommand` to persist daily snapshots
- Task 3.4: Create `GetHistoricalPersonStatsQuery` and handler
- Task 3.5: Create historical stats API endpoint

## Git commit

```bash
git add -A && git commit -m "feat(stats): expand FairnessCounter entity and add snapshots table"
```
