# Step 021 — In-App Notification System

## Phase
Post-MVP Completion

## Purpose
Admins had no way to know when a solver run completed or failed without manually checking the schedule page or logs. This step adds an in-app notification system: the solver worker creates notifications for all space members after every run, and a bell icon in the nav bar lets users see and dismiss them.

## What was built

### Database

| File | Description |
|---|---|
| `infra/migrations/006_notifications.sql` | `notifications` table with RLS. Columns: id, space_id, user_id, event_type, title, body, metadata_json, is_read, created_at, read_at. Indexes on (user_id, space_id) and unread filter. |

### Domain

| File | Description |
|---|---|
| `Domain/Notifications/Notification.cs` | `Notification` entity implementing `ITenantScoped`. `MarkRead()` sets `IsRead=true` and `ReadAt`. No external dependencies. |

### Application

| File | Description |
|---|---|
| `Application/Notifications/INotificationService.cs` | Interface with `NotifySpaceAdminsAsync` — creates notifications for all space members. Defined in Application so Infrastructure implements it. |
| `Application/Notifications/GetNotificationsQuery.cs` | `GetNotificationsQuery(SpaceId, UserId, UnreadOnly)` — returns up to 50 notifications ordered by newest first. Includes `NotificationDto` record. |
| `Application/Notifications/DismissNotificationCommand.cs` | `DismissNotificationCommand` (single) and `DismissAllNotificationsCommand` (bulk). Both idempotent — no-op if already read. |

### Infrastructure

| File | Description |
|---|---|
| `Infrastructure/Notifications/NotificationService.cs` | Implements `INotificationService`. Fetches all `SpaceMemberships` for the space and creates one `Notification` per member. |
| `Infrastructure/Persistence/Configurations/NotificationConfiguration.cs` | EF Fluent API config mapping `Notification` to `notifications` table. |
| `Infrastructure/Persistence/AppDbContext.cs` | Added `DbSet<Notification> Notifications`. |
| `Infrastructure/Scheduling/SolverWorkerService.cs` | Injects `INotificationService`. After `MarkCompleted/MarkTimedOut` calls `NotifySpaceAdminsAsync` with a human-readable title and body. After `MarkFailed` does the same with an error message. |

### API

| File | Description |
|---|---|
| `Api/Controllers/NotificationsController.cs` | `GET /spaces/{id}/notifications?unreadOnly=false` — list. `POST /spaces/{id}/notifications/{id}/read` — dismiss one. `POST /spaces/{id}/notifications/read-all` — dismiss all. All require `[Authorize]`; no extra permission check needed (users only see their own notifications). |
| `Api/Program.cs` | Registers `INotificationService → NotificationService` as scoped. |

### Frontend

| File | Description |
|---|---|
| `lib/api/notifications.ts` | `getNotifications`, `dismissNotification`, `dismissAllNotifications` API client functions. |
| `components/shell/NotificationBell.tsx` | Bell icon in the nav bar. Shows unread count badge. Dropdown lists notifications with event icon (✅/⚠️/❌), title, body, timestamp. "Mark all read" button. Polls every 30 seconds. Closes on outside click. |
| `components/shell/AppShell.tsx` | `NotificationBell` added to the right side of the header, between display name and admin toggle. |

## Key decisions

### Notify all space members, not just the requester
The person who triggered the solve may not be the only admin watching. All space members get notified so the whole team sees when a draft is ready.

### Polling, not WebSockets
30-second polling is simple, requires no infrastructure changes, and is sufficient for a scheduling tool where runs take minutes. WebSockets can be added later if needed.

### No permission check on the controller
Users only ever query their own notifications (`UserId = CurrentUserId` is enforced in the query handler). No cross-user data is possible, so no extra permission check is needed beyond `[Authorize]`.

### Notifications are not audit log entries
Notifications are ephemeral UX helpers — they can be dismissed. Audit log entries are immutable records of actions. These are separate concerns.

## How it connects
- `SolverWorkerService` → `INotificationService.NotifySpaceAdminsAsync` after every run outcome
- `NotificationsController` → MediatR → `GetNotificationsQuery` / `DismissNotificationCommand`
- `NotificationBell` polls `GET /spaces/{id}/notifications` every 30s
- Migration 006 must be run after 005

## How to run / verify

1. Run migration: `psql $DB_URL -f infra/migrations/006_notifications.sql`
2. Trigger a solver run via `POST /spaces/{id}/schedule-runs/trigger`
3. Wait for the worker to process it
4. `GET /spaces/{id}/notifications` → should return a notification with `isRead: false`
5. In the UI: bell icon shows a red badge with count; click to see the notification; click × or "Mark all read" to dismiss

## What comes next
- PDF export (last item from the HANDOFF list)
- Update HANDOFF.md to reflect completed items

## Git commit

```bash
git add -A && git commit -m "feat(notifications): in-app solver run notifications"
```
