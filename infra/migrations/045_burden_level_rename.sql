-- Migration 045: Rename burden level values from 4-level to 3-level taxonomy
-- Mapping: Hated → Hard, Disliked → Hard, Neutral → Normal, Favorable → Easy
-- Affects: task_types, tasks (group_tasks), fairness_counters

-- ─── Log counts before migration ─────────────────────────────────────────────
DO $$
DECLARE
    tt_count INT;
    t_count INT;
    fc_count INT;
BEGIN
    SELECT COUNT(*) INTO tt_count FROM task_types
        WHERE burden_level IN ('Hated', 'Disliked', 'Neutral', 'Favorable',
                               'hated', 'disliked', 'neutral', 'favorable');
    SELECT COUNT(*) INTO t_count FROM tasks
        WHERE burden_level IN ('Hated', 'Disliked', 'Neutral', 'Favorable',
                               'hated', 'disliked', 'neutral', 'favorable');
    SELECT COUNT(*) INTO fc_count FROM fairness_counters;

    RAISE NOTICE '[045] task_types with legacy burden_level: %', tt_count;
    RAISE NOTICE '[045] tasks with legacy burden_level: %', t_count;
    RAISE NOTICE '[045] fairness_counters total rows: %', fc_count;
END $$;

-- ─── Rename burden level values in task_types ─────────────────────────────────
UPDATE task_types SET burden_level = 'hard'
    WHERE burden_level IN ('Hated', 'Disliked', 'hated', 'disliked');

UPDATE task_types SET burden_level = 'normal'
    WHERE burden_level IN ('Neutral', 'neutral');

UPDATE task_types SET burden_level = 'easy'
    WHERE burden_level IN ('Favorable', 'favorable');

-- Update default value
ALTER TABLE task_types ALTER COLUMN burden_level SET DEFAULT 'normal';

-- ─── Rename burden level values in tasks (group_tasks) ────────────────────────
UPDATE tasks SET burden_level = 'hard'
    WHERE burden_level IN ('Hated', 'Disliked', 'hated', 'disliked');

UPDATE tasks SET burden_level = 'normal'
    WHERE burden_level IN ('Neutral', 'neutral');

UPDATE tasks SET burden_level = 'easy'
    WHERE burden_level IN ('Favorable', 'favorable');

-- Drop old CHECK constraint and add new one
ALTER TABLE tasks DROP CONSTRAINT IF EXISTS chk_task_burden_level;
ALTER TABLE tasks ADD CONSTRAINT chk_task_burden_level
    CHECK (burden_level IN ('easy', 'normal', 'hard'));

-- Update default value
ALTER TABLE tasks ALTER COLUMN burden_level SET DEFAULT 'normal';

-- ─── Rename fairness_counters columns ─────────────────────────────────────────

-- Rename hated_tasks_7d → hard_tasks_7d (if column exists)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'fairness_counters' AND column_name = 'hated_tasks_7d'
    ) THEN
        ALTER TABLE fairness_counters RENAME COLUMN hated_tasks_7d TO hard_tasks_7d;
    END IF;
END $$;

-- Rename hated_tasks_14d → hard_tasks_14d (if column exists)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'fairness_counters' AND column_name = 'hated_tasks_14d'
    ) THEN
        ALTER TABLE fairness_counters RENAME COLUMN hated_tasks_14d TO hard_tasks_14d;
    END IF;
END $$;

-- Rename consecutive_burden_count → consecutive_hard_count (if column exists)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'fairness_counters' AND column_name = 'consecutive_burden_count'
    ) THEN
        ALTER TABLE fairness_counters RENAME COLUMN consecutive_burden_count TO consecutive_hard_count;
    END IF;
END $$;

-- ─── Log counts after migration ──────────────────────────────────────────────
DO $$
DECLARE
    tt_hard INT;
    tt_normal INT;
    tt_easy INT;
    t_hard INT;
    t_normal INT;
    t_easy INT;
BEGIN
    SELECT COUNT(*) INTO tt_hard FROM task_types WHERE burden_level = 'hard';
    SELECT COUNT(*) INTO tt_normal FROM task_types WHERE burden_level = 'normal';
    SELECT COUNT(*) INTO tt_easy FROM task_types WHERE burden_level = 'easy';
    SELECT COUNT(*) INTO t_hard FROM tasks WHERE burden_level = 'hard';
    SELECT COUNT(*) INTO t_normal FROM tasks WHERE burden_level = 'normal';
    SELECT COUNT(*) INTO t_easy FROM tasks WHERE burden_level = 'easy';

    RAISE NOTICE '[045] After migration - task_types: hard=%, normal=%, easy=%', tt_hard, tt_normal, tt_easy;
    RAISE NOTICE '[045] After migration - tasks: hard=%, normal=%, easy=%', t_hard, t_normal, t_easy;
END $$;

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('045') ON CONFLICT DO NOTHING;
