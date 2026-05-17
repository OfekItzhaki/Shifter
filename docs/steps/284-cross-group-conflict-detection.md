# 284 — Cross-Group Conflict Detection

## Phase

Cross-Group Conflict Detection — Personal Scheduling Conflict Notifications

## Purpose

Detects scheduling conflicts (overlapping assignments and insufficient rest gaps) for users who belong to multiple groups — within the same space or across spaces via `LinkedUserId`. Runs post-facto after schedule publication and on user login, creating personal notifications visible only to the affected user. No group data is ever exposed to other groups.

## What was built

| File | Description |
|------|-------------|
| `infra/migrations/058_notification_dedup_hash.sql` | Adds `deduplication_hash VARCHAR(64)` column + partial index to notifications table |
| `apps/api/Jobuler.Domain/Conflicts/FlatAssignment.cs` | Immutable record for flattened assignment data |
| `apps/api/Jobuler.Domain/Conflicts/ConflictPair.cs` | Record pairing two conflicting assignments |
| `apps/api/Jobuler.Domain/Conflicts/ConflictType.cs` | Enum: Overlap, RestViolation |
| `apps/api/Jobuler.Domain/Conflicts/ConflictResult.cs` | Result wrapper for detected conflicts |
| `apps/api/Jobuler.Domain/Conflicts/ConflictDetector.cs` | Pure static sort-then-sweep algorithm (O(n log n)) |
| `apps/api/Jobuler.Domain/Notifications/Notification.cs` | Extended with `DeduplicationHash` property and `CreateWithDedup` factory |
| `apps/api/Jobuler.Application/Conflicts/IConflictDetectionService.cs` | Interface: `DetectOnPublishAsync`, `DetectOnLoginAsync` |
| `apps/api/Jobuler.Infrastructure/Persistence/ConflictDetectionDbContext.cs` | Minimal DbContext without RLS for cross-space queries |
| `apps/api/Jobuler.Infrastructure/Conflicts/ConflictDetectionService.cs` | Full implementation: publish + login paths, dedup, localization, push |
| `apps/api/Jobuler.Infrastructure/Conflicts/ConflictNotificationText.cs` | Localized notification text (he/en/ru) |
| `apps/api/Jobuler.Application/Scheduling/Commands/PublishVersionCommand.cs` | Added fire-and-forget conflict detection hook |
| `apps/api/Jobuler.Application/Auth/Commands/LoginCommandHandler.cs` | Added fire-and-forget conflict detection hook |
| `apps/api/Jobuler.Api/Program.cs` | DI registration for ConflictDetectionDbContext + service |
| `apps/api/Jobuler.Tests/Conflicts/ConflictNotificationTextTests.cs` | Unit tests for localization |

## Key decisions

- **Fire-and-forget pattern**: Same `Task.Run` + new DI scope pattern used by existing notification sending. Never blocks publish/login response.
- **Dedicated DbContext (no RLS)**: `ConflictDetectionDbContext` bypasses tenant isolation to query across spaces via `LinkedUserId`. Only reads the user's own assignments — no data leaks.
- **Sort-then-sweep algorithm**: O(n log n) with active-set pruning. Handles overlaps and rest violations in a single pass.
- **Deduplication via SHA-256 fingerprint**: Prevents notification spam. Sorted assignment pair IDs → stable hash. Only suppresses unread duplicates.
- **Cross-space privacy**: Each space's notification only includes group names visible within that space. Foreign groups show null.
- **No solver changes**: Purely post-facto detection. Solver runs independently per group.
- **No new API endpoints**: Notifications flow through existing `NotificationsController` and push infrastructure.

## How it connects

- Hooks into `PublishVersionCommandHandler` (after audit log) and `LoginCommandHandler` (after token generation)
- Uses existing `Notification` entity and `NotificationsController` for delivery
- Uses existing `IPushNotificationSender` for web-push delivery
- Respects `Group.MinRestBetweenShiftsHours` for rest violation detection (uses max of both groups)
- Respects `Space.Locale` for notification text localization

## How to run / verify

```bash
# Build
cd apps/api && dotnet build

# Run conflict-related tests
dotnet test --filter "FullyQualifiedName~Conflict"

# Run migration on VPS
docker exec -i compose-postgres-1 psql -U jobuler -d jobuler -f /migrations/058_notification_dedup_hash.sql

# Verify: publish a schedule for a user who is in multiple groups
# → Check notifications table for event_type = 'schedule.cross_group_conflict'
```

## What comes next

- Frontend: optional conflict indicator on "My Missions" page (not required — notifications already display)
- Multi-space architecture: allow users to create multiple spaces for different organizations
- Conflict resolution UI: let users acknowledge/dismiss conflicts with a note to their manager

## Git commit

```bash
git add -A && git commit -m "feat(conflicts): cross-group conflict detection with personal notifications"
```
