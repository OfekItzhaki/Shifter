SET client_encoding = 'UTF8';

-- Fix group messages
UPDATE group_messages SET content = 'שלום לכולם! הסידור החדש עלה. אנא בדקו את המשמרות שלכם.'
WHERE id = 'e6f7a8b9-c0d1-4e2f-3a4b-5c6d7e8f9a0b';

UPDATE group_messages SET content = 'תזכורת: כל מי שיש לו כישורים רפואיים - אנא עדכנו אותי.'
WHERE id = 'f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c';

UPDATE group_messages SET content = 'קיבלתי את הסידור, תודה!'
WHERE id = 'a8b9c0d1-e2f3-4a4b-5c6d-7e8f9a0b1c2d';

-- Fix group alerts
UPDATE group_alerts SET title = 'עדכון סידור', body = 'הסידור לשבוע הקרוב פורסם. בדקו את המשמרות שלכם.'
WHERE id = 'b9c0d1e2-f3a4-4b5c-6d7e-8f9a0b1c2d3e';

UPDATE group_alerts SET title = 'תרגיל מחר', body = 'מחר בשעה 06:00 יתקיים תרגיל. כל הכוחות נדרשים להיות נוכחים.'
WHERE id = 'c0d1e2f3-a4b5-4c6d-7e8f-9a0b1c2d3e4f';

-- Fix constraint payloads
UPDATE constraint_rules SET rule_payload_json = '{"qualification_name": "מפקד כיתה", "task_name": "תל 7", "min_count": 1}'
WHERE id = 'c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f';

UPDATE constraint_rules SET rule_payload_json = '{"qualification_name": "חובש", "task_name": "תל 9", "min_count": 1}'
WHERE id = 'd5e6f7a8-b9c0-4d1e-2f3a-4b5c6d7e8f9a';

-- Verify
SELECT 'messages' AS tbl, content FROM group_messages WHERE group_id = 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7'
UNION ALL
SELECT 'alerts', title FROM group_alerts WHERE group_id = 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7';
