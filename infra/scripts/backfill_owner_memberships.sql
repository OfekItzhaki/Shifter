-- Backfill: Link existing people to their user accounts and add owner memberships
-- Run this once to fix groups created before migration 009

-- Step 1: Link the admin user to their person record (Ofek Israeli = admin in this demo)
-- The admin user email is admin@demo.local, person is 'Ofek Israeli' (first person in space)
-- We match by display_name since there's no linked_user_id yet

-- Link admin@demo.local → first person in the space (adjust if needed)
UPDATE people
SET linked_user_id = (SELECT id FROM users WHERE email = 'admin@demo.local')
WHERE id = '50000000-0000-0000-0000-000000000001'
  AND linked_user_id IS NULL;

-- Step 2: For any group that has no owner membership, add the admin as owner
-- This covers groups created before the auto-membership fix
INSERT INTO group_memberships (id, space_id, group_id, person_id, is_owner, joined_at)
SELECT
    gen_random_uuid(),
    g.space_id,
    g.id,
    '50000000-0000-0000-0000-000000000001', -- admin person
    true,
    NOW()
FROM groups g
WHERE g.space_id = (SELECT id FROM spaces LIMIT 1)
  AND NOT EXISTS (
    SELECT 1 FROM group_memberships m
    WHERE m.group_id = g.id AND m.is_owner = true
  )
ON CONFLICT DO NOTHING;

-- Step 3: Update created_by_user_id for groups that don't have it set
UPDATE groups
SET created_by_user_id = (SELECT id FROM users WHERE email = 'admin@demo.local')
WHERE created_by_user_id IS NULL;
