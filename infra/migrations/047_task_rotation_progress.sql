-- Migration 047: Task Rotation Progress
-- Tracks per-person task type rotation within army-template groups.

CREATE TABLE task_rotation_progress (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    space_id UUID NOT NULL REFERENCES spaces(id),
    person_id UUID NOT NULL REFERENCES people(id),
    group_id UUID NOT NULL REFERENCES groups(id),
    cycle_number INT NOT NULL DEFAULT 1,
    completed_task_type_ids UUID[] NOT NULL DEFAULT '{}',
    total_qualified_task_types INT NOT NULL DEFAULT 0,
    completion_percentage DOUBLE PRECISION NOT NULL DEFAULT 0,
    last_updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(space_id, person_id, group_id)
);

CREATE INDEX idx_trp_group ON task_rotation_progress(group_id);

-- RLS policy: restrict access to rows matching the current space
ALTER TABLE task_rotation_progress ENABLE ROW LEVEL SECURITY;

CREATE POLICY task_rotation_progress_space_isolation
    ON task_rotation_progress
    USING (space_id::text = current_setting('app.current_space_id', TRUE));
