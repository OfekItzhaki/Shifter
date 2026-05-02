-- Fix demo memberships: link seed people to their users, create admin person,
-- and add everyone to the demo groups.
--
-- UUIDs from seed.sql:
--   admin user:   a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5
--   ofek user:    b2c3d4e5-f6a7-4b8c-9d0e-f1a2b3c4d5e6  → person b4c5d6e7-f8a9-4b0c-1d2e-f3a4b5c6d7e8
--   yael user:    c3d4e5f6-a7b8-4c9d-0e1f-a2b3c4d5e6f7  → person c5d6e7f8-a9b0-4c1d-2e3f-a4b5c6d7e8f9
--   Squad A:      f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6
--   Squad B:      a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7
--   Space:        e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9

-- ── 1. Link existing seed people to their user accounts ───────────────────────
UPDATE people SET linked_user_id = 'b2c3d4e5-f6a7-4b8c-9d0e-f1a2b3c4d5e6'
WHERE id = 'b4c5d6e7-f8a9-4b0c-1d2e-f3a4b5c6d7e8'
  AND linked_user_id IS NULL;

UPDATE people SET linked_user_id = 'c3d4e5f6-a7b8-4c9d-0e1f-a2b3c4d5e6f7'
WHERE id = 'c5d6e7f8-a9b0-4c1d-2e3f-a4b5c6d7e8f9'
  AND linked_user_id IS NULL;

-- ── 2. Create a Person record for the admin user (if not already exists) ──────
INSERT INTO people (id, space_id, full_name, display_name, linked_user_id, invitation_status)
VALUES (
    'a0b1c2d3-e4f5-4a6b-7c8d-e9f0a1b2c3d4',
    'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
    'Admin',
    'Admin',
    'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5',
    'accepted'
)
ON CONFLICT DO NOTHING;

-- ── 3. Add admin as owner of Squad A (was empty) ──────────────────────────────
INSERT INTO group_memberships (id, space_id, group_id, person_id, is_owner, joined_at)
VALUES (
    uuid_generate_v4(),
    'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
    'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6',
    'a0b1c2d3-e4f5-4a6b-7c8d-e9f0a1b2c3d4',
    TRUE,
    NOW()
)
ON CONFLICT DO NOTHING;

-- ── 4. Add admin as owner of Squad B (replacing the current non-owner state) ──
-- First check if admin is already in Squad B
INSERT INTO group_memberships (id, space_id, group_id, person_id, is_owner, joined_at)
VALUES (
    uuid_generate_v4(),
    'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
    'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
    'a0b1c2d3-e4f5-4a6b-7c8d-e9f0a1b2c3d4',
    TRUE,
    NOW()
)
ON CONFLICT DO NOTHING;

-- ── 5. Add Ofek and Yael to Squad A ──────────────────────────────────────────
INSERT INTO group_memberships (id, space_id, group_id, person_id, is_owner, joined_at)
VALUES
    (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
     'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6',
     'b4c5d6e7-f8a9-4b0c-1d2e-f3a4b5c6d7e8', FALSE, NOW()),
    (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
     'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6',
     'c5d6e7f8-a9b0-4c1d-2e3f-a4b5c6d7e8f9', FALSE, NOW()),
    (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
     'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6',
     'd6e7f8a9-b0c1-4d2e-3f4a-b5c6d7e8f9a0', FALSE, NOW()),
    (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
     'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6',
     'e7f8a9b0-c1d2-4e3f-4a5b-c6d7e8f9a0b1', FALSE, NOW()),
    (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
     'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6',
     'f8a9b0c1-d2e3-4f4a-5b6c-d7e8f9a0b1c2', FALSE, NOW()),
    (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
     'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6',
     'a9b0c1d2-e3f4-4a5b-6c7d-e8f9a0b1c2d3', FALSE, NOW())
ON CONFLICT DO NOTHING;

-- ── 6. Assign the default "Member" role to all new Squad A members ────────────
-- Find the default role for Squad A and assign it to all non-owner members
INSERT INTO person_role_assignments (id, space_id, person_id, role_id, group_id, assigned_at)
SELECT
    uuid_generate_v4(),
    gm.space_id,
    gm.person_id,
    sr.id,
    gm.group_id,
    NOW()
FROM group_memberships gm
JOIN space_roles sr ON sr.group_id = gm.group_id AND sr.is_default = TRUE AND sr.is_active = TRUE
WHERE gm.group_id = 'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6'
  AND gm.is_owner = FALSE
  AND NOT EXISTS (
      SELECT 1 FROM person_role_assignments pra
      WHERE pra.person_id = gm.person_id AND pra.group_id = gm.group_id
  )
ON CONFLICT DO NOTHING;

-- Also assign default role to admin's person in both groups (non-owner role for Squad B)
-- Admin is owner so skip role assignment for them

-- ── 7. Update group member counts (groups table doesn't cache this, it's computed) ─
-- Nothing to do — member_count is computed on the fly from group_memberships

SELECT 'Done' as status;
