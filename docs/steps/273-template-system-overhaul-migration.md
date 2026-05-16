# Step 273: Template System Overhaul — Database Migration

## Phase

Template System Overhaul (Spec: template-system-overhaul, Task 1.1)

## Purpose

Creates the database migration that transforms the schema from hardcoded domain assumptions (army kitchen duty, disliked/hated scoring) into a generic scheduling platform. This is the foundational schema change that all subsequent domain, infrastructure, and solver changes depend on.

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/056_template_system_overhaul.sql` | Idempotent migration that adds `template_type` to groups, drops dead columns, adds generic task-type counting, and converts constraint data |

### Migration details

1. **Add `template_type`** — TEXT column on `groups` defaulting to `'Custom'` so existing groups retain full functionality
2. **Drop `disliked_hated_score_*`** — 5 dead columns removed from `cumulative_records`
3. **Drop `kitchen_count_*`** — 5 dead columns removed from `cumulative_records`
4. **Add `task_type_counts`** — JSONB column on `cumulative_records` for generic per-task-type counting
5. **Drop fairness dead columns** — `disliked_hated_score_7d` and `kitchen_count_7d` removed from `fairness_counters`
6. **Convert constraints** — `max_kitchen_per_week` rows become `max_task_type_per_period` with `task_type_name="kitchen"`, `period_days=7`
7. **Add index** — `idx_groups_template_type` for efficient filtering by template type

## Key decisions

- Used `ADD COLUMN IF NOT EXISTS` / `DROP COLUMN IF EXISTS` for idempotency (safe to re-run)
- `rule_payload_json` is already JSONB in the `constraint_rules` table, so the UPDATE uses `jsonb_build_object` directly (no `::text` cast needed)
- COALESCE with sensible defaults (`max=2`, `task_type_name='kitchen'`) handles malformed payloads gracefully
- Existing groups default to `Custom` — no functionality lost, admins can change later

## How it connects

- **Downstream**: Domain entities (Task 1.2–1.4) will reference the new columns
- **Infrastructure**: EF Core configurations (Task 3.3) will map these columns
- **Solver**: The converted `max_task_type_per_period` constraints feed into the new generic solver handler (Task 5.2)
- **Frontend**: The `template_type` column drives the feature visibility map (Task 9.1)

## How to run / verify

```bash
# Run against local PostgreSQL
psql -U postgres -d rolduler -f infra/migrations/056_template_system_overhaul.sql

# Verify columns exist
psql -U postgres -d rolduler -c "\d groups" | grep template_type
psql -U postgres -d rolduler -c "\d cumulative_records" | grep task_type_counts

# Verify dead columns are gone
psql -U postgres -d rolduler -c "\d cumulative_records" | grep -c disliked_hated_score  # should be 0
psql -U postgres -d rolduler -c "\d cumulative_records" | grep -c kitchen_count         # should be 0
psql -U postgres -d rolduler -c "\d fairness_counters" | grep -c disliked_hated_score   # should be 0

# Verify constraint conversion
psql -U postgres -d rolduler -c "SELECT count(*) FROM constraint_rules WHERE rule_type = 'max_kitchen_per_week';"  # should be 0
```

## What comes next

- Task 1.2: Add `GroupTemplateType` enum and extend `Group` entity in the domain layer
- Task 1.3: Remove dead fields from `CumulativeRecord` and `AssignmentCountsDelta`
- Task 1.4: Remove dead fields from `FairnessCounter` entity

## Git commit

```bash
git add -A && git commit -m "feat(template-overhaul): add 056 migration — template_type, drop dead columns, generic task-type counts"
```
