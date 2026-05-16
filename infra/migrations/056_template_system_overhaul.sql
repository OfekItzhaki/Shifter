-- Migration 056: Template System Overhaul
-- Adds template_type to groups, drops dead columns (disliked_hated_score, kitchen_count),
-- adds generic task_type_counts JSONB, and converts max_kitchen_per_week constraints
-- to the generic max_task_type_per_period rule type.

-- ─── 1. Add template_type column to groups ────────────────────────────────────
ALTER TABLE groups
    ADD COLUMN IF NOT EXISTS template_type TEXT NOT NULL DEFAULT 'Custom';

-- ─── 2. Drop dead disliked_hated_score columns from cumulative_records ────────
ALTER TABLE cumulative_records
    DROP COLUMN IF EXISTS disliked_hated_score_7d,
    DROP COLUMN IF EXISTS disliked_hated_score_14d,
    DROP COLUMN IF EXISTS disliked_hated_score_30d,
    DROP COLUMN IF EXISTS disliked_hated_score_90d,
    DROP COLUMN IF EXISTS disliked_hated_score_period;

-- ─── 3. Drop dead kitchen_count columns from cumulative_records ───────────────
ALTER TABLE cumulative_records
    DROP COLUMN IF EXISTS kitchen_count_7d,
    DROP COLUMN IF EXISTS kitchen_count_14d,
    DROP COLUMN IF EXISTS kitchen_count_30d,
    DROP COLUMN IF EXISTS kitchen_count_90d,
    DROP COLUMN IF EXISTS kitchen_count_period;

-- ─── 4. Add generic task_type_counts JSONB column to cumulative_records ───────
ALTER TABLE cumulative_records
    ADD COLUMN IF NOT EXISTS task_type_counts JSONB NOT NULL DEFAULT '{}';

-- ─── 5. Drop dead columns from fairness_counters ──────────────────────────────
ALTER TABLE fairness_counters
    DROP COLUMN IF EXISTS disliked_hated_score_7d,
    DROP COLUMN IF EXISTS kitchen_count_7d;

-- ─── 6. Convert max_kitchen_per_week constraints to max_task_type_per_period ──
UPDATE constraint_rules
SET rule_type = 'max_task_type_per_period',
    rule_payload_json = jsonb_build_object(
        'task_type_name', COALESCE(rule_payload_json->>'task_type_name', 'kitchen'),
        'max', COALESCE((rule_payload_json->>'max')::int, 2),
        'period_days', 7
    )
WHERE rule_type = 'max_kitchen_per_week';

-- ─── 7. Add index on template_type for filtering ─────────────────────────────
CREATE INDEX IF NOT EXISTS idx_groups_template_type ON groups (template_type);

-- ─── Track migration ──────────────────────────────────────────────────────────
INSERT INTO schema_migrations (version) VALUES ('056') ON CONFLICT DO NOTHING;
