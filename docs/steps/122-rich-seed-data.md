# Step 122 — Rich Test Data for Squad B

## Phase
Phase 8 — Data & Testing Infrastructure

## Purpose
Extend the seed data for Squad B with realistic qualifications, member qualification assignments, qualification-based scheduling constraints, group messages, group alerts, and task qualification requirements. This gives developers and the solver a fully populated Squad B to work with — covering all the qualification-aware features added in steps 096, 120, and 121.

## What was built

### Files modified
- **`infra/scripts/seed.sql`** — New section appended at the end with all Squad B rich data.
- **`infra/migrations/999_seed.sql`** — Overwritten to match `infra/scripts/seed.sql` (kept in sync).

### Data added

#### 1. Group Qualifications (5 rows in `group_qualifications`)
Five qualification types defined for Squad B:

| UUID | Name |
|------|------|
| c1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c | מפקד כיתה (squad commander) |
| d2b3c4d5-e6f7-4a8b-9c0d-1e2f3a4b5c6d | חובש (medic) |
| e3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e | נהג (driver) |
| f4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f | צלף (sniper) |
| a5e6f7a8-b9c0-4d1e-2f3a-4b5c6d7e8f9a | מפקד מחלקה (platoon commander) |

#### 2. Member Qualifications (13 rows in `member_qualifications`)
Assignments:
- אופק יצחקי → מפקד כיתה, נהג
- נעם כהן → חובש
- דימה לוי → צלף, נהג
- יוגב שמיר → מפקד כיתה, חובש
- שחר דמרי → מפקד מחלקה, מפקד כיתה
- אופיר יצחקי → נהג
- Avi Mizrahi → חובש, צלף
- Tamar Ben-David → מפקד כיתה

#### 3. Qualification-Based Constraints (2 new rows in `constraint_rules`)
- `required_qualification_per_shift` (soft): תל 7 requires ≥1 מפקד כיתה per shift
- `preferred_qualification_per_shift` (soft): תל 9 prefers ≥1 חובש per shift

The existing `max_kitchen_per_week` and `min_rest_hours` constraints are kept unchanged.

#### 4. Group Messages (3 rows in `group_messages`)
Three messages in Squad B's channel, all authored by the admin user (Squad B members have no linked user accounts). The first message is pinned.

> Note: `group_messages` uses `author_user_id` (FK → `users`), not `author_person_id`. Squad B members are people-only records without user accounts, so admin is used as the author for all messages.

#### 5. Group Alerts (2 rows in `group_alerts`)
- "עדכון סידור" — severity `info`
- "תרגיל מחר" — severity `warning`

> Note: `group_alerts` has no `updated_at` column (per migration 012).

#### 6. Task Qualification Requirements (2 UPDATEs on `tasks`)
- תל 7: `qualification_requirements = [{"qualification_name": "מפקד כיתה", "min_count": 1, "mandatory": true}]`
- תל 9: `qualification_requirements = [{"qualification_name": "חובש", "min_count": 1, "mandatory": false}]`
- מטבח: unchanged (`[]`)

## Key decisions

- **Fixed UUIDs** for all new rows so the seed is idempotent (`ON CONFLICT DO NOTHING` everywhere).
- **`author_user_id` not `author_person_id`** — the actual `group_messages` schema uses a FK to `users`, not `people`. The task spec described `author_person_id` but the migration and EF configuration both use `author_user_id`.
- **No `updated_at` on `group_alerts`** — migration 012 did not add this column; the EF configuration confirms it.
- **Qualification names in JSON payloads** are stored as UTF-8 Hebrew strings, matching how the solver and constraint engine look them up by name.
- **`צלף` required a separate INSERT** on first apply because the unique partial index `(space_id, group_id, name) WHERE is_active = TRUE` caused a silent `ON CONFLICT DO NOTHING` skip when the seed was run twice — the name was new but the index conflict path was hit. Fixed by ensuring the row is present before member_qualifications FK references it.

## How it connects

- **Solver** reads `qualification_requirements` from tasks and `member_qualifications` to enforce/prefer qualified assignees.
- **Constraint engine** reads `required_qualification_per_shift` and `preferred_qualification_per_shift` rule types from `constraint_rules`.
- **Group detail page** (steps 027–030) displays messages and alerts from these tables.
- **Qualifications tab** (step 120) shows the group qualifications and member assignments seeded here.

## How to run / verify

```bash
# Re-apply seed (idempotent)
Get-Content infra/scripts/seed.sql | docker exec -i compose-postgres-1 psql -U jobuler -d jobuler

# Verify counts
docker exec compose-postgres-1 psql -U jobuler -d jobuler -c "
SELECT 'qualifications' AS tbl, count(*) FROM group_qualifications WHERE group_id = 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7'
UNION ALL SELECT 'member_quals', count(*) FROM member_qualifications WHERE group_id = 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7'
UNION ALL SELECT 'constraints', count(*) FROM constraint_rules WHERE scope_id = 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7'
UNION ALL SELECT 'messages', count(*) FROM group_messages WHERE group_id = 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7'
UNION ALL SELECT 'alerts', count(*) FROM group_alerts WHERE group_id = 'a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7';
"
# Expected: 5, 13, 4, 3, 2
```

## What comes next

- Solver integration tests that assert qualified members are preferred/required on תל 7 and תל 9.
- Frontend display of member qualification badges on the group detail page.
- Admin UI for assigning qualifications to members.

## Git commit

```bash
git add -A && git commit -m "feat(seed): rich test data — qualifications, messages, alerts, qualification constraints"
```
