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

-- Demo Space
INSERT INTO spaces (id, name, description, owner_user_id, locale) VALUES
  ('e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Unit Alpha', 'Demo space for local development', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5', 'he')
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
  ('b0c1d2e3-f4a5-4b6c-7d8e-f9a0b1c2d3e4', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Post 1',    'neutral',   false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('c1d2e3f4-a5b6-4c7d-8e9f-a0b1c2d3e4f5', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Post 2',    'neutral',   false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('d2e3f4a5-b6c7-4d8e-9f0a-b1c2d3e4f5a6', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Kitchen',   'disliked',  false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('e3f4a5b6-c7d8-4e9f-0a1b-c2d3e4f5a6b7', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'War Room',  'hated',     false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('f4a5b6c7-d8e9-4f0a-1b2c-d3e4f5a6b7c8', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Patrol',    'disliked',  false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('a5b6c7d8-e9f0-4a1b-2c3d-e4f5a6b7c8d9', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Reserve',   'favorable', false, 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
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
-- Uses NOW() + interval so they're always in the future regardless of when seed runs
-- תל 7: 4-hour shifts (240 min), starts NOW so current time is covered
-- תל 9: 4-hour shifts (240 min), starts NOW so current time is covered
-- מטבח: 24-hour shifts (1440 min), starts NOW
INSERT INTO tasks (id, space_id, group_id, name, starts_at, ends_at, shift_duration_minutes, required_headcount, burden_level, allows_double_shift, allows_overlap, qualification_requirements, created_by_user_id) VALUES
  ('b7df56c7-e6d9-4584-8c87-11a2a5a1a576', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'תל 7', NOW(), NOW() + INTERVAL '90 days', 240, 1, 'neutral', false, false, '[]', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('a899c417-9e35-4afd-9572-78eab9ee0788', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'תל 9', NOW(), NOW() + INTERVAL '90 days', 240, 1, 'neutral', false, false, '[]', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('a3d01500-ea30-4079-8a4f-5dfdb35f55b0', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7',
   'מטבח', NOW(), NOW() + INTERVAL '90 days', 1440, 1, 'neutral', false, false, '[]', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT (id) DO UPDATE SET
  starts_at = NOW(),
  ends_at = NOW() + INTERVAL '90 days',
  shift_duration_minutes = EXCLUDED.shift_duration_minutes,
  required_headcount = EXCLUDED.required_headcount,
  updated_at = NOW();

-- Squad B constraints
INSERT INTO constraint_rules (id, space_id, scope_type, scope_id, severity, rule_type, rule_payload_json, created_by_user_id) VALUES
  ('aa029b4d-a1e9-4006-803b-8b8b913483a7', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'group', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'soft', 'max_kitchen_per_week', '{"max": 2, "hours": 8}', 'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),
  ('edeae01d-398f-425f-9e0e-a7e55ae0cac4', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'group', 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7', 'hard', 'min_rest_hours',      '{"hours": 8}',          'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT DO NOTHING;
