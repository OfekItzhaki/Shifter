SET client_encoding = 'UTF8';
UPDATE group_qualifications SET name = 'מפקד כיתה'  WHERE id = 'c1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c';
UPDATE group_qualifications SET name = 'חובש'        WHERE id = 'd2b3c4d5-e6f7-4a8b-9c0d-1e2f3a4b5c6d';
UPDATE group_qualifications SET name = 'נהג'         WHERE id = 'e3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e';
UPDATE group_qualifications SET name = 'מפקד מחלקה' WHERE id = 'a5e6f7a8-b9c0-4d1e-2f3a-4b5c6d7e8f9a';
SELECT id, name FROM group_qualifications WHERE group_id = 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7';
