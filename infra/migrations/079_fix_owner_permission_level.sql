-- Fix space owner permission levels
-- The CreateSpaceCommandHandler was creating memberships with default Member (0) level
-- instead of SpaceOwner (3). This backfills the correct level for all space owners.
UPDATE space_memberships sm
SET permission_level = 3
FROM spaces s
WHERE sm.space_id = s.id
  AND sm.user_id = s.owner_user_id
  AND sm.permission_level < 3;
