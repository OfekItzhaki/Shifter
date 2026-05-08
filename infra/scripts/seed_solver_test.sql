-- Seed: Solver test data for Squad B
-- Ensures Squad B has 3 active tasks with future dates so the solver can produce a feasible schedule.
-- Squad B UUID: a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7
-- Space UUID:   e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9
-- Admin user:   a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5

SET client_encoding = 'UTF8';

-- Update existing Squad B tasks to have future dates (if they exist with past dates)
UPDATE tasks
SET
    starts_at = NOW(),
    ends_at   = NOW() + INTERVAL '90 days',
    updated_at = NOW()
WHERE
    space_id = 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9'
    AND group_id = 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7'
    AND is_active = TRUE
    AND ends_at < NOW();

-- Insert Squad B tasks if they don't exist yet
-- תל 7 (Post 7) — 8-hour shifts, 1 person required, neutral burden
INSERT INTO tasks (
    id, space_id, group_id, name,
    starts_at, ends_at,
    duration_hours, required_headcount,
    burden_level, allows_double_shift, allows_overlap,
    is_active, created_by_user_id
)
VALUES (
    'b1c2d3e4-f5a6-4b7c-8d9e-f0a1b2c3d4e5',
    'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
    'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
    'תל 7',
    NOW(),
    NOW() + INTERVAL '90 days',
    8, 1,
    'neutral', FALSE, FALSE,
    TRUE, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'
)
ON CONFLICT (space_id, group_id, name) WHERE is_active = TRUE
DO UPDATE SET
    starts_at  = NOW(),
    ends_at    = NOW() + INTERVAL '90 days',
    updated_at = NOW();

-- תל 9 (Post 9) — 8-hour shifts, 1 person required, neutral burden
INSERT INTO tasks (
    id, space_id, group_id, name,
    starts_at, ends_at,
    duration_hours, required_headcount,
    burden_level, allows_double_shift, allows_overlap,
    is_active, created_by_user_id
)
VALUES (
    'c2d3e4f5-a6b7-4c8d-9e0f-a1b2c3d4e5f6',
    'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
    'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
    'תל 9',
    NOW(),
    NOW() + INTERVAL '90 days',
    8, 1,
    'neutral', FALSE, FALSE,
    TRUE, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'
)
ON CONFLICT (space_id, group_id, name) WHERE is_active = TRUE
DO UPDATE SET
    starts_at  = NOW(),
    ends_at    = NOW() + INTERVAL '90 days',
    updated_at = NOW();

-- מטבח (Kitchen) — 8-hour shifts, 1 person required, disliked burden
INSERT INTO tasks (
    id, space_id, group_id, name,
    starts_at, ends_at,
    duration_hours, required_headcount,
    burden_level, allows_double_shift, allows_overlap,
    is_active, created_by_user_id
)
VALUES (
    'd3e4f5a6-b7c8-4d9e-0f1a-b2c3d4e5f6a7',
    'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
    'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
    'מטבח',
    NOW(),
    NOW() + INTERVAL '90 days',
    8, 1,
    'disliked', FALSE, FALSE,
    TRUE, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'
)
ON CONFLICT (space_id, group_id, name) WHERE is_active = TRUE
DO UPDATE SET
    starts_at  = NOW(),
    ends_at    = NOW() + INTERVAL '90 days',
    updated_at = NOW();
