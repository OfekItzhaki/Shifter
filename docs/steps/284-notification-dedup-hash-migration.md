# 284 — Notification Deduplication Hash Migration

## Phase

Cross-Group Conflict Detection — Database Foundation

## Purpose

Adds a `deduplication_hash` column to the `notifications` table to support idempotent conflict notifications. This prevents duplicate notifications from being created when the same set of conflicts is detected multiple times (e.g., on repeated logins or re-publishes).

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/058_notification_dedup_hash.sql` | SQL migration adding nullable `deduplication_hash VARCHAR(64)` column and a partial index for efficient duplicate lookups |

## Key decisions

- **Nullable column**: The hash is only populated for conflict notifications; existing notifications remain unaffected.
- **VARCHAR(64)**: Sized for a SHA-256 hex string (64 characters).
- **Partial index**: `ix_notifications_dedup` indexes only unread notifications (`WHERE is_read = FALSE`) since deduplication only applies to unread conflict notifications. This keeps the index small and fast.
- **IF NOT EXISTS guards**: Both the column and index use idempotent DDL to allow safe re-runs.

## How it connects

- The `ConflictDetectionService` (task 5.4) will compute a SHA-256 fingerprint of detected conflicts and store it in this column.
- Before creating a new conflict notification, the service queries this index to check if an identical unread notification already exists (Requirement 8.2).
- Read notifications do not suppress future duplicates (Requirement 8.3) — the partial index naturally excludes them.

## How to run / verify

Run the migration against your local PostgreSQL:

```bash
psql -U postgres -d jobuler -f infra/migrations/058_notification_dedup_hash.sql
```

Verify:
```sql
SELECT column_name, data_type, character_maximum_length, is_nullable
FROM information_schema.columns
WHERE table_name = 'notifications' AND column_name = 'deduplication_hash';

SELECT indexname, indexdef FROM pg_indexes WHERE indexname = 'ix_notifications_dedup';
```

## What comes next

- Task 1.2: Extend the `Notification` domain entity with `DeduplicationHash` property and EF Core mapping.
- Task 5.4: Implement the deduplication fingerprint computation logic.

## Git commit

```bash
git add -A && git commit -m "feat(conflicts): add deduplication_hash column to notifications table"
```
