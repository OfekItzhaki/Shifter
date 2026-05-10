-- Add person_qualifications for Squad B members so the solver can match them
-- qualification names must match what's in task qualification_requirements

INSERT INTO person_qualifications (id, space_id, person_id, qualification, is_active, created_at) VALUES
  -- אופק יצחקי: commander, driver
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', '650ad631-e5c8-4c97-b149-abfb9b923c03', 'commander', TRUE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', '650ad631-e5c8-4c97-b149-abfb9b923c03', 'driver',    TRUE, NOW()),
  -- נעם כהן: medic
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', '1fb7ad3a-4fdb-4356-851f-e986ee4208fb', 'medic',     TRUE, NOW()),
  -- דימה לוי: sniper, driver
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', '9dc1bd92-d276-4939-8405-9e9a18b44580', 'sniper',    TRUE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', '9dc1bd92-d276-4939-8405-9e9a18b44580', 'driver',    TRUE, NOW()),
  -- יוגב שמיר: commander, medic
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a994261b-ca89-41b0-807b-f6032b7aff43', 'commander', TRUE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', 'a994261b-ca89-41b0-807b-f6032b7aff43', 'medic',     TRUE, NOW()),
  -- שחר דמרי: commander
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', '8f568eb7-57aa-4c7a-850f-5b313bbd8938', 'commander', TRUE, NOW()),
  -- אופיר יצחקי: driver
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', '9478c177-64e4-4a54-a35e-0a771c52929e', 'driver',    TRUE, NOW()),
  -- Avi Mizrahi: medic, sniper
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', '3589519f-49a9-40a9-9f8a-9263ef624d03', 'medic',     TRUE, NOW()),
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', '3589519f-49a9-40a9-9f8a-9263ef624d03', 'sniper',    TRUE, NOW()),
  -- Tamar Ben-David: commander
  (uuid_generate_v4(), 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9', '2bd3b096-10b8-4685-953a-28619e1500f2', 'commander', TRUE, NOW())
ON CONFLICT DO NOTHING;

SELECT p.display_name, pq.qualification
FROM person_qualifications pq
JOIN people p ON p.id = pq.person_id
WHERE pq.space_id = 'e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9'
ORDER BY p.display_name;
