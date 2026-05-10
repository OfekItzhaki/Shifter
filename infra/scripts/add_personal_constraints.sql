-- Add personal constraints for Squad B members
-- These are constraints scoped to individual people

-- Personal constraint: אופק יצחקי cannot do kitchen (no_task_type_restriction)
-- Using the task ID as task_type_id in the payload
INSERT INTO constraint_rules (id, space_id, scope_type, scope_id, severity, rule_type, rule_payload_json, created_by_user_id) VALUES
  ('e1f2a3b4-c5d6-4e7f-8a9b-c0d1e2f3a4b5',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'person',
   '650ad631-e5c8-4c97-b149-abfb9b923c03',
   'hard',
   'no_task_type_restriction',
   '{"task_type_id": "a3d01500-ea30-4079-8a4f-5dfdb35f55b0"}',
   'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),

  -- Personal constraint: נעם כהן min rest 10 hours (harder than group default of 8)
  ('f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'person',
   '1fb7ad3a-4fdb-4356-851f-e986ee4208fb',
   'soft',
   'min_rest_hours',
   '{"hours": 10}',
   'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5'),

  -- Role constraint: members with commander qualification prefer תל 7
  -- (using group scope since we don't have role-scoped constraints for this)
  -- Personal constraint: Itay Shapiro no consecutive burden
  ('a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d8',
   'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9',
   'person',
   '853eacd3-95b1-4a8a-843a-2f0be4ce40a0',
   'soft',
   'no_consecutive_burden',
   '{"burden_level": "neutral"}',
   'a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5')
ON CONFLICT DO NOTHING;

SELECT scope_type, rule_type, severity, scope_id
FROM constraint_rules
WHERE space_id = 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9'
ORDER BY scope_type, rule_type;
