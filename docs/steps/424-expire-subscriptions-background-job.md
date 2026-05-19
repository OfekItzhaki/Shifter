# 424 — ExpireSubscriptionsJob Background Service

## Phase

Subscription Cancellation & Renewal — API layer and background job

## Purpose

Automates the expiry of canceled subscriptions that have passed their billing period. Without this job, canceled subscriptions would remain in "Canceled" status indefinitely and groups would never transition to Limited_Mode. The job runs every 6 hours, dispatching the `ExpireSubscriptionsCommand` via MediatR.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Scheduling/ExpireSubscriptionsJob.cs` | BackgroundService that runs every 6 hours, counts eligible subscriptions, dispatches `ExpireSubscriptionsCommand`, and logs the number expired |
| `apps/api/Jobuler.Api/Program.cs` | Registered `ExpireSubscriptionsJob` as a hosted service |

## Key decisions

- **BackgroundService over Hangfire** — The project does not use Hangfire; all existing background jobs use `BackgroundService` (e.g., `SolverWorkerService`, `SubscriptionCleanupService`). Kept consistent.
- **6-hour interval** — Balances between timely expiry and avoiding unnecessary DB load. Daily would be acceptable but 6 hours ensures groups don't linger in a "should be expired" state for too long.
- **2-minute startup delay** — Prevents the job from running during app initialization, matching the pattern used by other services.
- **Count before dispatch** — The job queries the count of eligible subscriptions before dispatching the command, allowing it to log meaningful metrics without modifying the command's void return type.
- **MediatR dispatch** — Follows the architecture rule that background jobs dispatch commands via MediatR rather than directly manipulating the database.

## How it connects

- Dispatches `ExpireSubscriptionsCommand` (task 3.3) which handles the actual state transitions and audit logging
- Works alongside `SubscriptionCleanupService` which handles the later stage (soft-deleting groups 6 months after cancellation)
- Groups deactivated by this job enter Limited_Mode (task 7.1 enforces write restrictions)

## How to run / verify

1. Build the solution: `dotnet build` from `apps/api/`
2. Run the API — the job starts automatically after a 2-minute delay
3. To verify manually: create a subscription in `Canceled` status with `CurrentPeriodEnd` in the past, then wait for the job to run (or reduce the interval temporarily)
4. Check logs for `ExpireSubscriptionsJob: expired N subscription(s)`

## What comes next

- Task 5.3: Extend `GetSubscription` endpoint response mapping
- Task 7.1: Limited_Mode guards on write operations
- Property tests for expiry behavior (task 3.6, 3.7)

## Git commit

```bash
git add -A && git commit -m "feat(billing): implement ExpireSubscriptionsJob recurring background service"
```
