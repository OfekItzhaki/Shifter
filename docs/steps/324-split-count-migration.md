# Step 324: Split Count Database Migration

## Phase
Feature — Split-Burden Scaling

## Purpose
Adds the `split_count` column to the `tasks` table (mapped as `GroupTask` in the domain). This column tracks how many sub-shifts a task is divided into, which is the foundation for the burden scaling feature. A value of 1 means no split (default behavior).

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/062_split_count.sql` | Adds `split_count INTEGER NOT NULL DEFAULT 1` with `CHECK (split_count >= 1)` constraint to the `tasks` table |

## Key decisions
- Used the same idempotent migration pattern as other recent migrations (DO $$ blocks with IF NOT EXISTS checks)
- Column defaults to 1 so existing rows are automatically backfilled without an explicit UPDATE
- CHECK constraint named `chk_split_count_positive` ensures data integrity at the database level
- The table is `tasks` in PostgreSQL but mapped to the `GroupTask` entity in EF Core

## How it connects
- **Requirement 1.1**: Persists the split count alongside the task
- **Requirement 1.2**: Default value of 1 means unsplit tasks are handled correctly
- **Next**: Task 1.2 (BurdenScalingService), Task 1.3 (GroupTask entity property), Task 1.4 (EF Core column mapping)

## How to run / verify
```bash
# Apply migration against local PostgreSQL
psql -U postgres -d jobuler -f infra/migrations/062_split_count.sql

# Verify column exists
psql -U postgres -d jobuler -c "\d tasks" | grep split_count

# Verify constraint
psql -U postgres -d jobuler -c "SELECT constraint_name FROM information_schema.constraint_column_usage WHERE table_name = 'tasks' AND column_name = 'split_count';"
```

## What comes next
- Task 1.2: Create `BurdenScalingService` static class
- Task 1.3: Add `SplitCount` property to `GroupTask` domain entity
- Task 1.4: Map `SplitCount` to `split_count` column in EF Core configuration

## Git commit
```bash
git add -A && git commit -m "feat(split-burden): add split_count column migration to tasks table"
```
