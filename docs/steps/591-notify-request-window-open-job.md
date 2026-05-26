# 591 — Notify Request Window Open Job

## Phase
Self-Service Scheduling — Background Jobs

## Purpose
Detects scheduling cycles whose request window has just opened and sends notifications to all group members within 5 minutes of the window opening. This ensures members are promptly informed when they can start submitting shift requests for an upcoming cycle.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Scheduling/NotifyRequestWindowOpenJob.cs` | BackgroundService that runs every 5 minutes, detects newly opened request windows, and sends in-app + push notifications to all group members |
| `apps/api/Jobuler.Api/Program.cs` | Registered the new hosted service |

## Key decisions

1. **BackgroundService pattern**: Follows the same pattern as `ProcessExpiredWaitlistOffersJob` and `CheckUnderScheduledMembersJob` — a `BackgroundService` with a 5-minute interval and 1-minute startup delay.
2. **Deduplication via hash**: Uses `Notification.CreateWithDedup` with a hash of `request_window_open_{cycleId}` to ensure idempotent execution — if the job runs multiple times within the same window, notifications are only sent once.
3. **Self-service group filtering**: Only processes cycles belonging to groups with `SchedulingMode.SelfService`.
4. **Linked user resolution**: Only sends notifications to members who have a `LinkedUserId` (registered users), matching the pattern used by `NotificationService`.
5. **Push failure isolation**: Push notification failures are caught and logged but never affect in-app notification persistence (requirement 13.7).

## How it connects

- Depends on `SchedulingCycle.RequestWindowOpensAt` to detect newly opened windows
- Uses `GroupMembership` to find group members and `Person.LinkedUserId` to resolve user IDs
- Creates `Notification` entities in the database for in-app delivery
- Uses `IPushNotificationSender` for push delivery
- Registered alongside other background jobs in `Program.cs`

## How to run / verify

```bash
cd apps/api
dotnet build
```

The job starts automatically with the application. To verify:
1. Create a self-service group with a `SelfServiceConfig`
2. Create a `SchedulingCycle` with `RequestWindowOpensAt` set to within the next 5 minutes
3. Wait for the job to run — notifications should appear in the `notifications` table

## What comes next

- Task 16.5: `CheckUnderScheduledMembersJob` (already implemented)
- Task 17.1: Self-service notification events (broader notification system)
- Task 18.1: DI registration wiring for all services

## Git commit

```bash
git add -A && git commit -m "feat(self-service): notify request window open background job"
```
