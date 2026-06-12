-- Seed: Demo space with realistic data for local development and testing
-- Password for all demo users: Demo1234!
-- BCrypt hash verified with BCrypt.Net-Next v4.0.3

-- =============================================================================
-- UUID MAPPING TABLE (old sequential → new random-looking UUID v4)
-- =============================================================================
-- User: admin          00000000-0000-0000-0000-000000000001 → a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5
-- User: ofek           00000000-0000-0000-0000-000000000002 → b2c3d4e5-f6a7-4b8c-9d0e-f1a2b3c4d5e6
-- User: yael           00000000-0000-0000-0000-000000000003 → c3d4e5f6-a7b8-4c9d-0e1f-a2b3c4d5e6f7
-- User: viewer         00000000-0000-0000-0000-000000000004 → d4e5f6a7-b8c9-4d0e-1f2a-b3c4d5e6f7a8
-- User: dana           (new) → f0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c
-- Person: Dana         (new) → e1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c
-- Space: Unit Alpha    10000000-0000-0000-0000-000000000001 → e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9
-- Role: Soldier        20000000-0000-0000-0000-000000000001 → f6a7b8c9-d0e1-4f2a-3b4c-d5e6f7a8b9c0
-- Role: Squad Cmd      20000000-0000-0000-0000-000000000002 → a7b8c9d0-e1f2-4a3b-4c5d-e6f7a8b9c0d1
-- Role: Medic          20000000-0000-0000-0000-000000000003 → b8c9d0e1-f2a3-4b4c-5d6e-f7a8b9c0d1e2
-- Role: Duty Officer   20000000-0000-0000-0000-000000000004 → c9d0e1f2-a3b4-4c5d-6e7f-a8b9c0d1e2f3
-- GroupType: Squad     30000000-0000-0000-0000-000000000001 → d0e1f2a3-b4c5-4d6e-7f8a-b9c0d1e2f3a4
-- GroupType: Platoon   30000000-0000-0000-0000-000000000002 → e1f2a3b4-c5d6-4e7f-8a9b-c0d1e2f3a4b5
-- Group: Squad A       40000000-0000-0000-0000-000000000001 → f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6
-- Group: Squad B       40000000-0000-0000-0000-000000000002 → a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7
-- Person: Admin         (new) → a0b1c2d3-e4f5-4a6b-7c8d-e9f0a1b2c3d4
-- Person: Ofek         50000000-0000-0000-0000-000000000001 → b4c5d6e7-f8a9-4b0c-1d2e-f3a4b5c6d7e8
-- Person: Yael         50000000-0000-0000-0000-000000000002 → c5d6e7f8-a9b0-4c1d-2e3f-a4b5c6d7e8f9
-- Person: Daniel       50000000-0000-0000-0000-000000000003 → d6e7f8a9-b0c1-4d2e-3f4a-b5c6d7e8f9a0
-- Person: Michal       50000000-0000-0000-0000-000000000004 → e7f8a9b0-c1d2-4e3f-4a5b-c6d7e8f9a0b1
-- Person: Ron          50000000-0000-0000-0000-000000000005 → f8a9b0c1-d2e3-4f4a-5b6c-d7e8f9a0b1c2
-- Person: Noa          50000000-0000-0000-0000-000000000006 → a9b0c1d2-e3f4-4a5b-6c7d-e8f9a0b1c2d3
-- TaskType: Post 1     60000000-0000-0000-0000-000000000001 → b0c1d2e3-f4a5-4b6c-7d8e-f9a0b1c2d3e4
-- TaskType: Post 2     60000000-0000-0000-0000-000000000002 → c1d2e3f4-a5b6-4c7d-8e9f-a0b1c2d3e4f5
-- TaskType: Kitchen    60000000-0000-0000-0000-000000000003 → d2e3f4a5-b6c7-4d8e-9f0a-b1c2d3e4f5a6
-- TaskType: War Room   60000000-0000-0000-0000-000000000004 → e3f4a5b6-c7d8-4e9f-0a1b-c2d3e4f5a6b7
-- TaskType: Patrol     60000000-0000-0000-0000-000000000005 → f4a5b6c7-d8e9-4f0a-1b2c-d3e4f5a6b7c8
-- TaskType: Reserve    60000000-0000-0000-0000-000000000006 → a5b6c7d8-e9f0-4a1b-2c3d-e4f5a6b7c8d9
-- =============================================================================

SET client_encoding = 'UTF8';

-- Demo Users
INSERT INTO users (id, email, display_name, password_hash, preferred_locale) VALUES
  ('a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'admin@demo.local',   'Admin',   '$2a$12$WqeSlsFmXzSru4YK23qfeuMYIUd/4ZkHLLwx0NAehm.Vbmq1MYEEa', 'he'),
  ('b2c3d4e5-f6a7-4b8c-9d0e-f1a2b3c4d5e6', 'ofek@demo.local',    'Ofek',    '$2a$12$WqeSlsFmXzSru4YK23qfeuMYIUd/4ZkHLLwx0NAehm.Vbmq1MYEEa', 'he'),
  ('c3d4e5f6-a7b8-4c9d-0e1f-a2b3c4d5e6f7', 'yael@demo.local',    'Yael',    '$2a$12$WqeSlsFmXzSru4YK23qfeuMYIUd/4ZkHLLwx0NAehm.Vbmq1MYEEa', 'he'),
  ('d4e5f6a7-b8c9-4d0e-1f2a-b3c4d5e6f7a8', 'viewer@demo.local',  'Viewer',  '$2a$12$WqeSlsFmXzSru4YK23qfeuMYIUd/4ZkHLLwx0NAehm.Vbmq1MYEEa', 'he')
ON CONFLICT (id) DO UPDATE SET password_hash = EXCLUDED.password_hash;

-- Demo Organization
INSERT INTO organizations (
  id,
  display_name,
  normalized_name,
  primary_owner_user_id,
  country_code,
  setup_template,
  default_locale,
  default_timezone_id,
  status
) VALUES (
  'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
  'Unit Alpha Demo',
  'UNIT ALPHA DEMO',
  'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5',
  'IL',
  'military_style',
  'he',
  'Asia/Jerusalem',
  'Active'
)
ON CONFLICT (id) DO NOTHING;

-- Demo Space
INSERT INTO spaces (id, organization_id, name, description, owner_user_id, locale) VALUES
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Unit Alpha', 'Demo space for local development', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'he')
ON CONFLICT DO NOTHING;

-- Memberships
INSERT INTO space_memberships (space_id, user_id) VALUES
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'b2c3d4e5-f6a7-4b8c-9d0e-f1a2b3c4d5e6'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c3d4e5f6-a7b8-4c9d-0e1f-a2b3c4d5e6f7'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'd4e5f6a7-b8c9-4d0e-1f2a-b3c4d5e6f7a8')
ON CONFLICT DO NOTHING;

-- Permissions for admin user
INSERT INTO space_permission_grants (space_id, user_id, permission_key, granted_by_user_id) VALUES
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'space.view',                    'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'space.admin_mode',              'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'people.manage',                 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'tasks.manage',                  'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'schedule.publish',              'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'schedule.rollback',             'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'schedule.recalculate',          'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'constraints.manage',            'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'restrictions.manage_sensitive', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'permissions.manage',            'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'logs.view_sensitive',           'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT DO NOTHING;

-- Viewer gets only space.view
INSERT INTO space_permission_grants (space_id, user_id, permission_key, granted_by_user_id) VALUES
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'd4e5f6a7-b8c9-4d0e-1f2a-b3c4d5e6f7a8', 'space.view', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT DO NOTHING;

-- Self-service demo members need space.view so the member picker can load their groups.
INSERT INTO space_permission_grants (space_id, user_id, permission_key, granted_by_user_id) VALUES
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'b2c3d4e5-f6a7-4b8c-9d0e-f1a2b3c4d5e6', 'space.view', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c3d4e5f6-a7b8-4c9d-0e1f-a2b3c4d5e6f7', 'space.view', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT DO NOTHING;

-- Operational Roles
INSERT INTO space_roles (id, space_id, name, description, created_by_user_id) VALUES
  ('f6a7b8c9-d0e1-4f2a-3b4c-d5e6f7a8b9c0', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Soldier',   'Combat soldier',   'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('a7b8c9d0-e1f2-4a3b-4c5d-e6f7a8b9c0d1', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Squad Commander', 'Squad commander', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('b8c9d0e1-f2a3-4b4c-5d6e-f7a8b9c0d1e2', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Medic',     'Field medic',      'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('c9d0e1f2-a3b4-4c5d-6e7f-a8b9c0d1e2f3', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Duty Officer', 'Duty officer',  'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT DO NOTHING;

-- Group Types
INSERT INTO group_types (id, space_id, name) VALUES
  ('d0e1f2a3-b4c5-4d6e-7f8a-b9c0d1e2f3a4', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Squad'),
  ('e1f2a3b4-c5d6-4e7f-8a9b-c0d1e2f3a4b5', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Platoon')
ON CONFLICT DO NOTHING;

-- Groups
INSERT INTO groups (id, space_id, group_type_id, name) VALUES
  ('f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'd0e1f2a3-b4c5-4d6e-7f8a-b9c0d1e2f3a4', 'Squad A'),
  ('a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'd0e1f2a3-b4c5-4d6e-7f8a-b9c0d1e2f3a4', 'Squad B')
ON CONFLICT DO NOTHING;

-- People (linked to their user accounts)
INSERT INTO people (id, space_id, full_name, display_name, linked_user_id, invitation_status) VALUES
  ('a0b1c2d3-e4f5-4a6b-7c8d-e9f0a1b2c3d4', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Admin',          'Admin',   'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'accepted'),
  ('b4c5d6e7-f8a9-4b0c-1d2e-f3a4b5c6d7e8', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Ofek Israeli',   'Ofek',    'b2c3d4e5-f6a7-4b8c-9d0e-f1a2b3c4d5e6', 'accepted'),
  ('c5d6e7f8-a9b0-4c1d-2e3f-a4b5c6d7e8f9', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Yael Cohen',     'Yael',    'c3d4e5f6-a7b8-4c9d-0e1f-a2b3c4d5e6f7', 'accepted'),
  ('d6e7f8a9-b0c1-4d2e-3f4a-b5c6d7e8f9a0', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Daniel Levi',    'Daniel',  NULL, 'accepted'),
  ('e7f8a9b0-c1d2-4e3f-4a5b-c6d7e8f9a0b1', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Michal Avraham', 'Michal',  NULL, 'accepted'),
  ('f8a9b0c1-d2e3-4f4a-5b6c-d7e8f9a0b1c2', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Ron Shamir',     'Ron',     NULL, 'accepted'),
  ('a9b0c1d2-e3f4-4a5b-6c7d-e8f9a0b1c2d3', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Noa Golan',      'Noa',     NULL, 'accepted')
ON CONFLICT DO NOTHING;

-- Group memberships: Admin owns both groups; seed people are in Squad A
INSERT INTO group_memberships (id, space_id, group_id, person_id, is_owner, joined_at) VALUES
  -- Squad A
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6', 'a0b1c2d3-e4f5-4a6b-7c8d-e9f0a1b2c3d4', TRUE,  NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6', 'b4c5d6e7-f8a9-4b0c-1d2e-f3a4b5c6d7e8', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6', 'c5d6e7f8-a9b0-4c1d-2e3f-a4b5c6d7e8f9', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6', 'd6e7f8a9-b0c1-4d2e-3f4a-b5c6d7e8f9a0', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6', 'e7f8a9b0-c1d2-4e3f-4a5b-c6d7e8f9a0b1', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6', 'f8a9b0c1-d2e3-4f4a-5b6c-d7e8f9a0b1c2', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6', 'a9b0c1d2-e3f4-4a5b-6c7d-e8f9a0b1c2d3', FALSE, NOW()),
  -- Squad B
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'a0b1c2d3-e4f5-4a6b-7c8d-e9f0a1b2c3d4', TRUE,  NOW())
ON CONFLICT DO NOTHING;

-- Task Types
INSERT INTO task_types (id, space_id, name, burden_level, allows_overlap, created_by_user_id) VALUES
  ('b0c1d2e3-f4a5-4b6c-7d8e-f9a0b1c2d3e4', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Post 1',    'normal', false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('c1d2e3f4-a5b6-4c7d-8e9f-a0b1c2d3e4f5', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Post 2',    'normal', false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('d2e3f4a5-b6c7-4d8e-9f0a-b1c2d3e4f5a6', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Kitchen',   'hard',   false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e3f4a5b6-c7d8-4e9f-0a1b-c2d3e4f5a6b7', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'War Room',  'hard',   false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('f4a5b6c7-d8e9-4f0a-1b2c-d3e4f5a6b7c8', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Patrol',    'hard',   false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('a5b6c7d8-e9f0-4a1b-2c3d-e4f5a6b7c8d9', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Reserve',   'easy',   false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT DO NOTHING;

-- =============================================================================
-- Dana: ungrouped demo user for testing add-by-email flow
-- User UUID:   f0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c
-- Person UUID: e1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c
-- =============================================================================

INSERT INTO users (id, email, display_name, password_hash, preferred_locale) VALUES
  ('f0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c', 'dana@demo.local', 'Dana',
   '$2a$12$WqeSlsFmXzSru4YK23qfeuMYIUd/4ZkHLLwx0NAehm.Vbmq1MYEEa', 'he')
ON CONFLICT (id) DO UPDATE SET password_hash = EXCLUDED.password_hash;

INSERT INTO people (id, space_id, full_name, display_name, linked_user_id) VALUES
  ('e1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'Dana Demo', 'Dana',
   'f0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c')
ON CONFLICT DO NOTHING;

INSERT INTO space_memberships (space_id, user_id) VALUES
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'f0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c')
ON CONFLICT DO NOTHING;

INSERT INTO space_permission_grants (space_id, user_id, permission_key, granted_by_user_id) VALUES
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'f0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c',
   'space.view', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT DO NOTHING;
-- NOTE: No group_memberships rows for Dana — she is ungrouped by design

-- =============================================================================
-- Squad B: seed members for solver testing
-- Squad B needs at least 10 members to cover its tasks 24/7.
-- Adding 10 people directly into Squad B membership.
-- =============================================================================

-- Additional people for Squad B
INSERT INTO people (id, space_id, full_name, display_name, invitation_status) VALUES
  ('650ad631-e5c8-4c97-b149-abfb9b923c03', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'אופק יצחקי',  'אופק',  'accepted'),
  ('1fb7ad3a-4fdb-4356-851f-e986ee4208fb', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'נעם כהן',     'נעם',   'accepted'),
  ('9dc1bd92-d276-4939-8405-9e9a18b44580', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'דימה לוי',    'דימה',  'accepted'),
  ('a994261b-ca89-41b0-807b-f6032b7aff43', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'יוגב שמיר',   'יוגב',  'accepted'),
  ('8f568eb7-57aa-4c7a-850f-5b313bbd8938', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'שחר דמרי',    'שחר',   'accepted'),
  ('9478c177-64e4-4a54-a35e-0a771c52929e', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'אופיר יצחקי', 'אופיר', 'accepted'),
  ('057d89ce-1a61-4ef0-a8cf-76450bd35eda', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'דנוך בן-דוד', 'דנוך',  'accepted'),
  ('3589519f-49a9-40a9-9f8a-9263ef624d03', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Avi Mizrahi',     'Avi',   'accepted'),
  ('2bd3b096-10b8-4685-953a-28619e1500f2', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Tamar Ben-David', 'Tamar', 'accepted'),
  ('4282eaf4-0e04-453f-b7f9-541d652173ea', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Gal Peretz',      'Gal',   'accepted'),
  ('853eacd3-95b1-4a8a-843a-2f0be4ce40a0', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Itay Shapiro',    'Itay',  'accepted'),
  ('0ac3af9e-018e-4de8-9116-6ef267bf68f7', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Maya Katz',       'Maya',  'accepted')
ON CONFLICT DO NOTHING;

-- Squad B memberships (13 members total — enough for 24/7 coverage)
INSERT INTO group_memberships (id, space_id, group_id, person_id, is_owner, joined_at) VALUES
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '650ad631-e5c8-4c97-b149-abfb9b923c03', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '1fb7ad3a-4fdb-4356-851f-e986ee4208fb', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '9dc1bd92-d276-4939-8405-9e9a18b44580', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'a994261b-ca89-41b0-807b-f6032b7aff43', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '8f568eb7-57aa-4c7a-850f-5b313bbd8938', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '9478c177-64e4-4a54-a35e-0a771c52929e', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '057d89ce-1a61-4ef0-a8cf-76450bd35eda', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '3589519f-49a9-40a9-9f8a-9263ef624d03', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '2bd3b096-10b8-4685-953a-28619e1500f2', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '4282eaf4-0e04-453f-b7f9-541d652173ea', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '853eacd3-95b1-4a8a-843a-2f0be4ce40a0', FALSE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '0ac3af9e-018e-4de8-9116-6ef267bf68f7', FALSE, NOW())
ON CONFLICT DO NOTHING;

-- Squad B tasks with future dates for solver testing
-- starts_at uses date_trunc('hour', NOW()) so shifts always start on a clean hour boundary
INSERT INTO tasks (id, space_id, group_id, name, starts_at, ends_at, shift_duration_minutes, required_headcount, burden_level, allows_double_shift, allows_overlap, qualification_requirements, created_by_user_id) VALUES
  ('b7df56c7-e6d9-4584-8c87-11a2a5a1a576', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'תל 7', date_trunc('hour', NOW()), NOW() + INTERVAL '90 days', 240, 1, 'normal', false, false, '[]', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('a899c417-9e35-4afd-9572-78eab9ee0788', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'תל 9', date_trunc('hour', NOW()), NOW() + INTERVAL '90 days', 240, 1, 'normal', false, false, '[]', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('a3d01500-ea30-4079-8a4f-5dfdb35f55b0', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'מטבח', date_trunc('day', NOW()) + INTERVAL '8 hours', NOW() + INTERVAL '90 days', 1440, 1, 'normal', false, false, '[]', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT (id) DO UPDATE SET
  starts_at = date_trunc('hour', NOW()),
  ends_at = NOW() + INTERVAL '90 days',
  shift_duration_minutes = EXCLUDED.shift_duration_minutes,
  required_headcount = EXCLUDED.required_headcount,
  updated_at = NOW();

-- Squad B constraints
INSERT INTO constraint_rules (id, space_id, scope_type, scope_id, severity, rule_type, rule_payload_json, created_by_user_id) VALUES
  ('aa029b4d-a1e9-4006-803b-8b8b913483a7', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'group', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'soft', 'max_kitchen_per_week', '{"max": 2, "hours": 8}', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('edeae01d-398f-425f-9e0e-a7e55ae0cac4', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'group', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'hard', 'min_rest_hours',      '{"hours": 8}',          'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT DO NOTHING;

-- =============================================================================
-- Step 122: Rich test data for Squad B
-- Qualifications, member qualifications, qualification constraints,
-- group messages, group alerts, and task qualification requirements.
-- =============================================================================

SET client_encoding = 'UTF8';

-- -----------------------------------------------------------------------------
-- 1. Group Qualifications for Squad B
-- -----------------------------------------------------------------------------
-- UUID mapping:
--   מפקד כיתה  → c1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c
--   חובש       → d2b3c4d5-e6f7-4a8b-9c0d-1e2f3a4b5c6d
--   נהג        → e3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e
--   צלף        → f4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f
--   מפקד מחלקה → a5e6f7a8-b9c0-4d1e-2f3a-4b5c6d7e8f9a

INSERT INTO group_qualifications (id, space_id, group_id, name, is_active, created_at, updated_at) VALUES
  ('c1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'מפקד כיתה',  TRUE, NOW(), NOW()),
  ('d2b3c4d5-e6f7-4a8b-9c0d-1e2f3a4b5c6d', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'חובש',       TRUE, NOW(), NOW()),
  ('e3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'נהג',        TRUE, NOW(), NOW()),
  ('f4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'Sniper',     TRUE, NOW(), NOW()),
  ('a5e6f7a8-b9c0-4d1e-2f3a-4b5c6d7e8f9a', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'מפקד מחלקה', TRUE, NOW(), NOW())
ON CONFLICT DO NOTHING;

-- -----------------------------------------------------------------------------
-- 2. Member Qualifications
-- -----------------------------------------------------------------------------
-- אופק יצחקי  (650ad631): מפקד כיתה, נהג
-- נעם כהן     (1fb7ad3a): חובש
-- דימה לוי    (9dc1bd92): צלף, נהג
-- יוגב שמיר   (a994261b): מפקד כיתה, חובש
-- שחר דמרי    (8f568eb7): מפקד מחלקה, מפקד כיתה
-- אופיר יצחקי (9478c177): נהג
-- Avi Mizrahi (3589519f): חובש, צלף
-- Tamar Ben-David (2bd3b096): מפקד כיתה

INSERT INTO member_qualifications (id, space_id, group_id, person_id, qualification_id, assigned_at) VALUES
  -- אופק יצחקי: מפקד כיתה
  ('b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d5e', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '650ad631-e5c8-4c97-b149-abfb9b923c03', 'c1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c', NOW()),
  -- אופק יצחקי: נהג
  ('c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '650ad631-e5c8-4c97-b149-abfb9b923c03', 'e3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e', NOW()),
  -- נעם כהן: חובש
  ('d3e4f5a6-b7c8-4d9e-0f1a-2b3c4d5e6f7a', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '1fb7ad3a-4fdb-4356-851f-e986ee4208fb', 'd2b3c4d5-e6f7-4a8b-9c0d-1e2f3a4b5c6d', NOW()),
  -- דימה לוי: צלף
  ('e4f5a6b7-c8d9-4e0f-1a2b-3c4d5e6f7a8b', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '9dc1bd92-d276-4939-8405-9e9a18b44580', 'f4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f', NOW()),
  -- דימה לוי: נהג
  ('f5a6b7c8-d9e0-4f1a-2b3c-4d5e6f7a8b9c', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '9dc1bd92-d276-4939-8405-9e9a18b44580', 'e3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e', NOW()),
  -- יוגב שמיר: מפקד כיתה
  ('a6b7c8d9-e0f1-4a2b-3c4d-5e6f7a8b9c0d', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'a994261b-ca89-41b0-807b-f6032b7aff43', 'c1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c', NOW()),
  -- יוגב שמיר: חובש
  ('b7c8d9e0-f1a2-4b3c-4d5e-6f7a8b9c0d1e', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'a994261b-ca89-41b0-807b-f6032b7aff43', 'd2b3c4d5-e6f7-4a8b-9c0d-1e2f3a4b5c6d', NOW()),
  -- שחר דמרי: מפקד מחלקה
  ('c8d9e0f1-a2b3-4c4d-5e6f-7a8b9c0d1e2f', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '8f568eb7-57aa-4c7a-850f-5b313bbd8938', 'a5e6f7a8-b9c0-4d1e-2f3a-4b5c6d7e8f9a', NOW()),
  -- שחר דמרי: מפקד כיתה
  ('d9e0f1a2-b3c4-4d5e-6f7a-8b9c0d1e2f3a', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '8f568eb7-57aa-4c7a-850f-5b313bbd8938', 'c1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c', NOW()),
  -- אופיר יצחקי: נהג
  ('e0f1a2b3-c4d5-4e6f-7a8b-9c0d1e2f3a4b', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '9478c177-64e4-4a54-a35e-0a771c52929e', 'e3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e', NOW()),
  -- Avi Mizrahi: חובש
  ('f1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '3589519f-49a9-40a9-9f8a-9263ef624d03', 'd2b3c4d5-e6f7-4a8b-9c0d-1e2f3a4b5c6d', NOW()),
  -- Avi Mizrahi: צלף
  ('a2b3c4d5-e6f7-4a8b-9c0d-1e2f3a4b5c6d', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '3589519f-49a9-40a9-9f8a-9263ef624d03', 'f4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f', NOW()),
  -- Tamar Ben-David: מפקד כיתה
  ('b3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', '2bd3b096-10b8-4685-953a-28619e1500f2', 'c1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c', NOW())
ON CONFLICT DO NOTHING;

-- -----------------------------------------------------------------------------
-- 3. Group Constraints — qualification-based
-- -----------------------------------------------------------------------------
-- Hard constraint: תל 7 requires at least 1 מפקד כיתה per shift
-- Soft constraint: prefer חובש on תל 9

INSERT INTO constraint_rules (id, space_id, scope_type, scope_id, severity, rule_type, rule_payload_json, created_by_user_id) VALUES
  ('c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'group',
   'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'soft',
   'required_qualification_per_shift',
   '{"qualification_name": "מפקד כיתה", "task_name": "תל 7", "min_count": 1}',
   'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('d5e6f7a8-b9c0-4d1e-2f3a-4b5c6d7e8f9a',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'group',
   'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'soft',
   'preferred_qualification_per_shift',
   '{"qualification_name": "חובש", "task_name": "תל 9", "min_count": 1}',
   'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT DO NOTHING;

-- -----------------------------------------------------------------------------
-- 4. Group Messages
-- NOTE: group_messages uses author_user_id (FK to users), not author_person_id.
-- אופק יצחקי has no linked user account, so all 3 messages are authored by admin.
-- -----------------------------------------------------------------------------

INSERT INTO group_messages (id, space_id, group_id, author_user_id, content, is_pinned, created_at, updated_at) VALUES
  ('e6f7a8b9-c0d1-4e2f-3a4b-5c6d7e8f9a0b',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5',
   'שלום לכולם! הסידור החדש עלה. אנא בדקו את המשמרות שלכם.',
   TRUE,
   NOW() - INTERVAL '2 hours',
   NOW() - INTERVAL '2 hours'),
  ('f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5',
   'תזכורת: כל מי שיש לו כישורים רפואיים - אנא עדכנו אותי.',
   FALSE,
   NOW() - INTERVAL '1 hour',
   NOW() - INTERVAL '1 hour'),
  ('a8b9c0d1-e2f3-4a4b-5c6d-7e8f9a0b1c2d',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5',
   'קיבלתי את הסידור, תודה!',
   FALSE,
   NOW() - INTERVAL '30 minutes',
   NOW() - INTERVAL '30 minutes')
ON CONFLICT DO NOTHING;

-- -----------------------------------------------------------------------------
-- 5. Group Alerts
-- NOTE: group_alerts has no updated_at column per migration 012.
-- -----------------------------------------------------------------------------

INSERT INTO group_alerts (id, space_id, group_id, created_by_person_id, title, body, severity, created_at) VALUES
  ('b9c0d1e2-f3a4-4b5c-6d7e-8f9a0b1c2d3e',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'a0b1c2d3-e4f5-4a6b-7c8d-e9f0a1b2c3d4',
   'עדכון סידור',
   'הסידור לשבוע הקרוב פורסם. בדקו את המשמרות שלכם.',
   'info',
   NOW() - INTERVAL '3 hours'),
  ('c0d1e2f3-a4b5-4c6d-7e8f-9a0b1c2d3e4f',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'a0b1c2d3-e4f5-4a6b-7c8d-e9f0a1b2c3d4',
   'תרגיל מחר',
   'מחר בשעה 06:00 יתקיים תרגיל. כל הכוחות נדרשים להיות נוכחים.',
   'warning',
   NOW() - INTERVAL '1 hour')
ON CONFLICT DO NOTHING;

-- -----------------------------------------------------------------------------
-- 6. Update Squad B tasks with qualification_requirements
-- תל 7: requires 1 מפקד כיתה (mandatory)
-- תל 9: prefers 1 חובש (optional)
-- מטבח: no requirements (keep as [])
-- -----------------------------------------------------------------------------

UPDATE tasks
SET qualification_requirements = '[{"qualification_name": "מפקד כיתה", "count": 1, "mandatory": true}]'::jsonb,
    updated_at = NOW()
WHERE id = 'b7df56c7-e6d9-4584-8c87-11a2a5a1a576'
  AND space_id = 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9';

UPDATE tasks
SET qualification_requirements = '[{"qualification_name": "חובש", "count": 1, "mandatory": false}]'::jsonb,
    updated_at = NOW()
WHERE id = 'a899c417-9e35-4afd-9572-78eab9ee0788'
  AND space_id = 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9';

-- =============================================================================
-- Self-Service Demo Group
-- Stable seed for manual self-service E2E and local product review.
-- =============================================================================

INSERT INTO groups (id, space_id, group_type_id, name, scheduling_mode) VALUES
  ('c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'd0e1f2a3-b4c5-4d6e-7f8a-b9c0d1e2f3a4',
   'Self-Service Demo',
   'SelfService')
ON CONFLICT (id) DO UPDATE SET
  scheduling_mode = 'SelfService',
  updated_at = NOW();

INSERT INTO group_memberships (id, space_id, group_id, person_id, is_owner, joined_at) VALUES
  ('d7e8f9a0-b1c2-4d3e-8f4a-5b6c7d8e9f0a', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'a0b1c2d3-e4f5-4a6b-7c8d-e9f0a1b2c3d4', TRUE,  NOW()),
  ('e8f9a0b1-c2d3-4e4f-9a5b-6c7d8e9f0a1b', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'b4c5d6e7-f8a9-4b0c-1d2e-f3a4b5c6d7e8', FALSE, NOW()),
  ('f9a0b1c2-d3e4-4f5a-8b6c-7d8e9f0a1b2c', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'c5d6e7f8-a9b0-4c1d-2e3f-a4b5c6d7e8f9', FALSE, NOW()),
  ('a0b1c2d3-e4f5-4a6b-8c7d-9e0f1a2b3c4d', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'd6e7f8a9-b0c1-4d2e-3f4a-b5c6d7e8f9a0', FALSE, NOW())
ON CONFLICT DO NOTHING;

INSERT INTO self_service_configs (
  id, space_id, group_id,
  min_shifts_per_cycle, max_shifts_per_cycle,
  request_window_open_offset_hours, request_window_close_offset_hours,
  cancellation_cutoff_hours, max_late_cancellations_per_cycle,
  late_cancellation_window_hours, waitlist_offer_minutes, cycle_duration_days,
  created_at, updated_at
) VALUES (
  'b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d5e',
  'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
  'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f',
  1, 3,
  72, 12,
  24, 2,
  24, 60, 7,
  NOW(), NOW()
)
ON CONFLICT (group_id) DO UPDATE SET
  min_shifts_per_cycle = EXCLUDED.min_shifts_per_cycle,
  max_shifts_per_cycle = EXCLUDED.max_shifts_per_cycle,
  request_window_open_offset_hours = EXCLUDED.request_window_open_offset_hours,
  request_window_close_offset_hours = EXCLUDED.request_window_close_offset_hours,
  cancellation_cutoff_hours = EXCLUDED.cancellation_cutoff_hours,
  max_late_cancellations_per_cycle = EXCLUDED.max_late_cancellations_per_cycle,
  late_cancellation_window_hours = EXCLUDED.late_cancellation_window_hours,
  waitlist_offer_minutes = EXCLUDED.waitlist_offer_minutes,
  cycle_duration_days = EXCLUDED.cycle_duration_days,
  updated_at = NOW();

INSERT INTO tasks (id, space_id, group_id, name, starts_at, ends_at, shift_duration_minutes, required_headcount, burden_level, allows_double_shift, allows_overlap, qualification_requirements, created_by_user_id) VALUES
  ('c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'Front Desk', date_trunc('day', NOW()) + INTERVAL '2 days 8 hours', NOW() + INTERVAL '90 days', 480, 1, 'normal', false, false, '[]', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('d3e4f5a6-b7c8-4d9e-8f1a-2b3c4d5e6f7a', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'Evening Cover', date_trunc('day', NOW()) + INTERVAL '2 days 16 hours', NOW() + INTERVAL '90 days', 480, 1, 'normal', false, false, '[]', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e4f5a6b7-c8d9-4e0f-9a2b-3c4d5e6f7a8c', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'Night Watch', date_trunc('day', NOW()) + INTERVAL '3 days 8 hours', NOW() + INTERVAL '90 days', 480, 1, 'normal', false, false, '[]', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT (id) DO UPDATE SET
  starts_at = EXCLUDED.starts_at,
  ends_at = EXCLUDED.ends_at,
  updated_at = NOW();

INSERT INTO shift_templates (id, space_id, group_id, name, starts_at_time, ends_at_time, days_of_week, required_headcount, required_qualification_ids, required_role_ids, is_active, group_task_id, day_of_week, start_time, end_time, is_deleted, created_by_user_id, created_at, updated_at) VALUES
  ('e4f5a6b7-c8d9-4e0f-9a2b-3c4d5e6f7a8b', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'Front Desk', '08:00', '16:00', '[]', 1, '[]', '[]', TRUE, 'c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f', EXTRACT(DOW FROM NOW() + INTERVAL '2 days')::int, '08:00', '16:00', FALSE, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', NOW(), NOW()),
  ('f5a6b7c8-d9e0-4f1a-8b3c-4d5e6f7a8b9c', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'Evening Cover', '16:00', '23:00', '[]', 1, '[]', '[]', TRUE, 'd3e4f5a6-b7c8-4d9e-8f1a-2b3c4d5e6f7a', EXTRACT(DOW FROM NOW() + INTERVAL '2 days')::int, '16:00', '23:00', FALSE, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', NOW(), NOW()),
  ('a6b7c8d9-e0f1-4a2b-8c3d-5e6f7a8b9c0d', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'Night Watch', '08:00', '16:00', '[]', 1, '[]', '[]', TRUE, 'e4f5a6b7-c8d9-4e0f-9a2b-3c4d5e6f7a8c', EXTRACT(DOW FROM NOW() + INTERVAL '3 days')::int, '08:00', '16:00', FALSE, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', NOW(), NOW())
ON CONFLICT (id) DO UPDATE SET
  name = EXCLUDED.name,
  starts_at_time = EXCLUDED.starts_at_time,
  ends_at_time = EXCLUDED.ends_at_time,
  days_of_week = EXCLUDED.days_of_week,
  day_of_week = EXCLUDED.day_of_week,
  start_time = EXCLUDED.start_time,
  end_time = EXCLUDED.end_time,
  required_headcount = EXCLUDED.required_headcount,
  required_qualification_ids = EXCLUDED.required_qualification_ids,
  required_role_ids = EXCLUDED.required_role_ids,
  is_active = TRUE,
  is_deleted = FALSE,
  updated_at = NOW();

INSERT INTO scheduling_cycles (id, space_id, group_id, starts_at, ends_at, request_window_opens_at, request_window_closes_at, is_generated, created_at, updated_at) VALUES
  ('a6b7c8d9-e0f1-4a2b-9c3d-5e6f7a8b9c0d',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f',
   NOW() + INTERVAL '2 days',
   NOW() + INTERVAL '9 days',
   NOW() - INTERVAL '1 hour',
   NOW() + INTERVAL '1 day',
   TRUE,
   NOW(),
   NOW())
ON CONFLICT (id) DO UPDATE SET
  starts_at = EXCLUDED.starts_at,
  ends_at = EXCLUDED.ends_at,
  request_window_opens_at = EXCLUDED.request_window_opens_at,
  request_window_closes_at = EXCLUDED.request_window_closes_at,
  is_generated = TRUE,
  updated_at = NOW();

INSERT INTO shift_slots (id, space_id, group_id, cycle_id, template_id, starts_at, ends_at, required_headcount, max_headcount, current_headcount, group_task_id, shift_template_id, scheduling_cycle_id, date, start_time, end_time, capacity, current_fill_count, status, created_at, updated_at) VALUES
  ('b7c8d9e0-f1a2-4b3c-8d4e-6f7a8b9c0d1e', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'a6b7c8d9-e0f1-4a2b-9c3d-5e6f7a8b9c0d', 'e4f5a6b7-c8d9-4e0f-9a2b-3c4d5e6f7a8b', date_trunc('day', NOW()) + INTERVAL '2 days 8 hours', date_trunc('day', NOW()) + INTERVAL '2 days 16 hours', 1, 1, 1, 'c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f', 'e4f5a6b7-c8d9-4e0f-9a2b-3c4d5e6f7a8b', 'a6b7c8d9-e0f1-4a2b-9c3d-5e6f7a8b9c0d', (NOW() + INTERVAL '2 days')::date, '08:00', '16:00', 1, 1, 'Open', NOW(), NOW()),
  ('c8d9e0f1-a2b3-4c4d-9e5f-7a8b9c0d1e2f', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'a6b7c8d9-e0f1-4a2b-9c3d-5e6f7a8b9c0d', 'f5a6b7c8-d9e0-4f1a-8b3c-4d5e6f7a8b9c', date_trunc('day', NOW()) + INTERVAL '2 days 16 hours', date_trunc('day', NOW()) + INTERVAL '2 days 23 hours', 1, 1, 0, 'd3e4f5a6-b7c8-4d9e-8f1a-2b3c4d5e6f7a', 'f5a6b7c8-d9e0-4f1a-8b3c-4d5e6f7a8b9c', 'a6b7c8d9-e0f1-4a2b-9c3d-5e6f7a8b9c0d', (NOW() + INTERVAL '2 days')::date, '16:00', '23:00', 1, 0, 'Open', NOW(), NOW()),
  ('d9e0f1a2-b3c4-4d5e-9f6a-8b9c0d1e2f3a', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f', 'a6b7c8d9-e0f1-4a2b-9c3d-5e6f7a8b9c0d', 'a6b7c8d9-e0f1-4a2b-8c3d-5e6f7a8b9c0d', date_trunc('day', NOW()) + INTERVAL '3 days 8 hours', date_trunc('day', NOW()) + INTERVAL '3 days 16 hours', 1, 1, 0, 'e4f5a6b7-c8d9-4e0f-9a2b-3c4d5e6f7a8c', 'a6b7c8d9-e0f1-4a2b-8c3d-5e6f7a8b9c0d', 'a6b7c8d9-e0f1-4a2b-9c3d-5e6f7a8b9c0d', (NOW() + INTERVAL '3 days')::date, '08:00', '16:00', 1, 0, 'Open', NOW(), NOW())
ON CONFLICT (id) DO UPDATE SET
  cycle_id = EXCLUDED.cycle_id,
  template_id = EXCLUDED.template_id,
  starts_at = EXCLUDED.starts_at,
  ends_at = EXCLUDED.ends_at,
  required_headcount = EXCLUDED.required_headcount,
  max_headcount = EXCLUDED.max_headcount,
  current_headcount = EXCLUDED.current_headcount,
  date = EXCLUDED.date,
  start_time = EXCLUDED.start_time,
  end_time = EXCLUDED.end_time,
  capacity = EXCLUDED.capacity,
  current_fill_count = EXCLUDED.current_fill_count,
  status = 'Open',
  updated_at = NOW();

INSERT INTO shift_requests (id, space_id, shift_slot_id, person_id, group_id, scheduling_cycle_id, status, is_admin_override, processed_by_user_id, created_at, updated_at) VALUES
  ('d9e0f1a2-b3c4-4d5e-8f6a-9b0c1d2e3f4a',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'b7c8d9e0-f1a2-4b3c-8d4e-6f7a8b9c0d1e',
   'b4c5d6e7-f8a9-4b0c-1d2e-f3a4b5c6d7e8',
   'c6d7e8f9-a0b1-4c2d-8e3f-4a5b6c7d8e9f',
   'a6b7c8d9-e0f1-4a2b-9c3d-5e6f7a8b9c0d',
   'Approved',
   FALSE,
   'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5',
   NOW(),
   NOW())
ON CONFLICT (id) DO UPDATE SET
  status = 'Approved',
  updated_at = NOW();

INSERT INTO waitlist_entries (id, space_id, shift_slot_id, person_id, position, status, offered_at, expires_at, created_at, updated_at) VALUES
  ('e0f1a2b3-c4d5-4e6f-8a7b-9c0d1e2f3a4b',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'b7c8d9e0-f1a2-4b3c-8d4e-6f7a8b9c0d1e',
   'c5d6e7f8-a9b0-4c1d-2e3f-a4b5c6d7e8f9',
   1,
   'Waiting',
   NULL,
   NULL,
   NOW(),
   NOW())
ON CONFLICT (id) DO UPDATE SET
  position = 1,
  status = 'Waiting',
  updated_at = NOW();

-- מטבח already has qualification_requirements = '[]', no update needed.
