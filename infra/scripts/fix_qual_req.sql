UPDATE tasks
SET qualification_requirements = '[{"qualification_name": "commander", "count": 1, "mandatory": true}]'::jsonb,
    updated_at = NOW()
WHERE id = 'b7df56c7-e6d9-4584-8c87-11a2a5a1a576';

UPDATE tasks
SET qualification_requirements = '[{"qualification_name": "medic", "count": 1, "mandatory": false}]'::jsonb,
    updated_at = NOW()
WHERE id = 'a899c417-9e35-4afd-9572-78eab9ee0788';

SELECT id, qualification_requirements FROM tasks
WHERE group_id = 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7'
  AND qualification_requirements != '[]'::jsonb;
