# Step 220 — Burden Level Database Migration & EF Core Update

## Phase

Statistics Overhaul — Phase 1 (Burden Level Migration)

## Purpose

Migrate existing database records from the old 4-level burden taxonomy (Favorable, Neutral, Disliked, Hated) to the new 3-level taxonomy (Easy, Normal, Hard). Update EF Core entity configurations and all C#/TypeScript references to match the renamed database columns.

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/045_burden_level_rename.sql` | SQL migration that renames burden level values in `task_types` and `tasks` tables, renames columns in `fairness_counters` (hated_tasks_7d→hard_tasks_7d, hated_tasks_14d→hard_tasks_14d, consecutive_burden_count→consecutive_hard_count), updates CHECK constraints, and logs record counts before/after |
| `apps/api/Jobuler.Domain/Scheduling/FairnessCounter.cs` | Renamed properties: `HatedTasks7d`→`HardTasks7d`, `HatedTasks14d`→`HardTasks14d`, `ConsecutiveBurdenCount`→`ConsecutiveHardCount` |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/SchedulingConfiguration.cs` | Updated EF Core column mappings to use new column names (`hard_tasks_7d`, `hard_tasks_14d`, `consecutive_hard_count`) |
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Updated fairness DTO construction to use new property names |
| `apps/api/Jobuler.Application/Scheduling/Models/SolverInputDto.cs` | Renamed `FairnessCountersDto` fields: `HatedTasks7d`→`HardTasks7d`, `ConsecutiveBurdenCount`→`ConsecutiveHardCount` |
| `apps/api/Jobuler.Application/Scheduling/Queries/GetBurdenStatsQuery.cs` | Renamed `PersonBurdenStatsDto` fields and leaderboard references |
| `apps/web/lib/api/schedule.ts` | Updated TypeScript interface to match new API field names |

## Key decisions

- **EF Core conversion unchanged**: The existing `HasConversion(v => v.ToString().ToLower(), v => Enum.Parse<TaskBurdenLevel>(v, true))` already works correctly with the new enum values (Easy, Normal, Hard) — no conversion logic changes needed.
- **Idempotent migration**: Column renames use `IF EXISTS` guards so the migration can be re-run safely.
- **Both cases handled**: The migration handles both PascalCase and lowercase versions of old values since different parts of the system stored them differently.
- **CHECK constraint updated**: The `tasks` table CHECK constraint is replaced to only allow `'easy'`, `'normal'`, `'hard'`.
- **Legacy score column preserved**: `disliked_hated_score_7d` is kept as-is for now (will be replaced in Phase 2 with proper burden score columns).

## How it connects

- Depends on: Step 219 (burden level enum rename in domain)
- Required by: Task 1.4 (SolverPayloadNormalizer update), Phase 2 (expanded fairness counters)
- The migration must run before the new API code is deployed, since the EF Core config now expects the new column names.

## How to run / verify

```bash
# Run the migration against a local database
psql -U postgres -d jobuler -f infra/migrations/045_burden_level_rename.sql

# Verify the build compiles
cd apps/api && dotnet build

# Verify column renames
psql -U postgres -d jobuler -c "\d fairness_counters"
# Should show: hard_tasks_7d, hard_tasks_14d, consecutive_hard_count

# Verify burden level values
psql -U postgres -d jobuler -c "SELECT DISTINCT burden_level FROM task_types"
# Should show: easy, normal, hard
```

## What comes next

- Task 1.4: Update `SolverPayloadNormalizer` to emit new burden level strings
- Task 1.5: Update Python solver `burden_map` with backward compatibility
- Task 1.6: Update frontend burden labels and color constants

## Git commit

```bash
git add -A && git commit -m "feat(statistics): burden level DB migration and EF Core column rename"
```
