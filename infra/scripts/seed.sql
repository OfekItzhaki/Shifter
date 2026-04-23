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
-- Space: Unit Alpha    10000000-0000-0000-0000-000000000001 → e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9
-- Role: Soldier        20000000-0000-0000-0000-000000000001 → f6a7b8c9-d0e1-4f2a-3b4c-d5e6f7a8b9c0
-- Role: Squad Cmd      20000000-0000-0000-0000-000000000002 → a7b8c9d0-e1f2-4a3b-4c5d-e6f7a8b9c0d1
-- Role: Medic          20000000-0000-0000-0000-000000000003 → b8c9d0e1-f2a3-4b4c-5d6e-f7a8b9c0d1e2
-- Role: Duty Officer   20000000-0000-0000-0000-000000000004 → c9d0e1f2-a3b4-4c5d-6e7f-a8b9c0d1e2f3
-- GroupType: Squad     30000000-0000-0000-0000-000000000001 → d0e1f2a3-b4c5-4d6e-7f8a-b9c0d1e2f3a4
-- GroupType: Platoon   30000000-0000-0000-0000-000000000002 → e1f2a3b4-c5d6-4e7f-8a9b-c0d1e2f3a4b5
-- Group: Squad A       40000000-0000-0000-0000-000000000001 → f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6
-- Group: Squad B       40000000-0000-0000-0000-000000000002 → a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7
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

-- People
INSERT INTO people (id, space_id, full_name, display_name) VALUES
  ('b4c5d6e7-f8a9-4b0c-1d2e-f3a4b5c6d7e8', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Ofek Israeli',   'Ofek'),
  ('c5d6e7f8-a9b0-4c1d-2e3f-a4b5c6d7e8f9', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Yael Cohen',     'Yael'),
  ('d6e7f8a9-b0c1-4d2e-3f4a-b5c6d7e8f9a0', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Daniel Levi',    'Daniel'),
  ('e7f8a9b0-c1d2-4e3f-4a5b-c6d7e8f9a0b1', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Michal Avraham', 'Michal'),
  ('f8a9b0c1-d2e3-4f4a-5b6c-d7e8f9a0b1c2', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Ron Shamir',     'Ron'),
  ('a9b0c1d2-e3f4-4a5b-6c7d-e8f9a0b1c2d3', 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'Noa Golan',      'Noa')
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
