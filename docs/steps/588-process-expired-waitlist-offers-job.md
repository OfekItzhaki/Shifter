# Step 588 — ProcessExpiredWaitlistOffersJob

## Phase
Phase — Self-Service Scheduling (Background Jobs)

## Purpose
Implements a recurring background job that processes expired waitlist offers every 5 minutes. When a waitlist offer times out (member doesn't accept within the configured window), this job marks it as expired and cascades the offer to the next waiting member in the queue.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Scheduling/ProcessExpiredWaitlistOffersJob.cs` | BackgroundService that runs every 5 minutes, resolving `IWaitlistService` from a scoped DI container and calling `ProcessExpiredOffersAsync()` |
| `apps/api/Jobuler.Api/Program.cs` | Registered the job via `AddHostedService<ProcessExpiredWaitlistOffersJob>()` |

## Key decisions

- **BackgroundService pattern**: Follows the same hosted service pattern as `ExpireSubscriptionsJob` rather than using Hangfire's `RecurringJob` API, since the project doesn't use Hangfire directly — it uses .NET's built-in `BackgroundService` abstraction.
- **1-minute startup delay**: Gives the application time to stabilize before the first run (shorter than the 2-minute delay used by `ExpireSubscriptionsJob` since waitlist expiry is more time-sensitive).
- **Debug-level logging**: Uses `LogDebug` for routine execution since this runs every 5 minutes and would be noisy at Info level. Errors are logged at `LogError`.
- **Scoped service resolution**: Creates a new DI scope per execution to properly resolve scoped services like `IWaitlistService` (which depends on `AppDbContext`).

## How it connects

- Calls `IWaitlistService.ProcessExpiredOffersAsync()` which was implemented in task 9.1
- Works alongside the waitlist flow: when a slot is released, the first waiting member gets an offer with a time limit. This job ensures expired offers are cleaned up and the next member is offered the slot.
- Validates Requirement 9.4: expired waitlist offers cascade to the next member

## How to run / verify

```bash
cd apps/api
dotnet build
```

The job starts automatically with the application. To verify behavior, check logs for `ProcessExpiredWaitlistOffersJob` entries at Debug level.

## What comes next

- Task 16.3: `ExpireSwapRequestsJob` — marks 72h-old pending swaps as expired
- Task 16.4: `NotifyRequestWindowOpenJob` — sends notifications when request windows open
- Task 18.1: DI registration wiring for all self-service services

## Git commit

```bash
git add -A && git commit -m "feat(self-service): add ProcessExpiredWaitlistOffersJob background service"
```
