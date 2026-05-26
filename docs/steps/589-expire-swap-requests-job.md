# 589 — Expire Swap Requests Job

## Phase

Self-Service Scheduling — Background Jobs (Task 16.3)

## Purpose

Swap requests between members have a 72-hour expiry window. If the target member doesn't respond within that time, the swap request should automatically be marked as `Expired` and the initiator should be notified. This background job runs every hour to detect and expire stale pending swap requests.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Scheduling/ExpireSwapRequestsJob.cs` | New `BackgroundService` that runs every hour, queries pending `SwapRequests` past their `ExpiresAt` time, calls `Expire()` on each, persists changes, and logs notification intent for initiators |
| `apps/api/Jobuler.Api/Program.cs` | Registered `ExpireSwapRequestsJob` as a hosted service |

## Key decisions

- **BackgroundService pattern**: Follows the same pattern as `ExpireSubscriptionsJob` and `ProcessExpiredWaitlistOffersJob` — uses `BackgroundService` with `IServiceScopeFactory` rather than Hangfire directly (the project doesn't use Hangfire's `RecurringJob` API despite the spec mentioning it)
- **Direct DB query**: The job queries `SwapRequests` directly via `AppDbContext` rather than delegating to `IShiftSwapService`, since the service interface doesn't expose an expiry method. This is consistent with how `ExpireSubscriptionsJob` works.
- **Notification logging**: Full notification integration is deferred to task 17.1. For now, the job logs the notification intent with structured logging so it's traceable.
- **Startup delay**: 3-minute delay after app startup before first run, allowing the application to stabilize.
- **Batch processing**: Loads all expired swaps in one query, expires them in memory, then saves once — efficient for typical volumes.

## How it connects

- Uses the `SwapRequest.Expire()` domain method (created in task 1.8)
- Queries `AppDbContext.SwapRequests` (DbSet added in task 2.1)
- Complements `ShiftSwapService` (task 11.1) which creates swap requests with 72h expiry
- Will integrate with `INotificationService` when task 17.1 is implemented
- Registered alongside other self-service background jobs in `Program.cs`

## How to run / verify

```bash
cd apps/api
dotnet build
```

The job starts automatically with the API. To verify behavior:
1. Create a swap request (via `ShiftSwapService.ProposeSwapAsync`)
2. Wait for the `ExpiresAt` to pass (or manually set it in the DB for testing)
3. The job will mark it as `Expired` on its next hourly run
4. Check logs for `ExpireSwapRequestsJob: expired swap request {id}` entries

## What comes next

- Task 16.4: `NotifyRequestWindowOpenJob` — sends notifications when request windows open
- Task 16.5: `CheckUnderScheduledMembersJob` — flags under-scheduled members
- Task 17.1: Full notification integration (will replace log-based notification intent)
- Task 18.1: DI registration wiring for all self-service services

## Git commit

```bash
git add -A && git commit -m "feat(self-service): implement ExpireSwapRequestsJob background service"
```
