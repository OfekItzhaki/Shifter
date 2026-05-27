-- Migration 076: Backfill space_memberships from group members
-- Adds space memberships for all people with linked_user_id who are in groups
-- but don't yet have a space_memberships record.
-- Also sets the space owner's permission_level to 3 (SpaceOwner).

-- ─── 1. Add missing space memberships for linked users in groups ──────────────
INSERT INTO space_memberships (id, space_id, user_id, joined_at, is_active, permission_level)
SELECT DISTINCT
    uuid_generate_v4(),
    p.space_id,
    p.linked_user_id,
    COALESCE(gm.joined_at, p.created_at),
    TRUE,
    0  -- Member level
FROM people p
JOIN group_memberships gm ON gm.person_id = p.id
WHERE p.linked_user_id IS NOT NULL
  AND p.is_active = TRUE
  AND NOT EXISTS (
      SELECT 1 FROM space_memberships sm
      WHERE sm.space_id = p.space_id AND sm.user_id = p.linked_user_id
  )
ON CONFLICT (space_id, user_id) DO NOTHING;

-- ─── 2. Set space owners to SpaceOwner permission level (3) ───────────────────
UPDATE space_memberships sm
SET permission_level = 3
FROM spaces s
WHERE sm.space_id = s.id
  AND sm.user_id = s.owner_user_id
  AND sm.permission_level < 3;

-- Track migration
INSERT INTO schema_migrations (version) VALUES ('076') ON CONFLICT DO NOTHING;
