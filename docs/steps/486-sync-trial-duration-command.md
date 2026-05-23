# Step 486 — SyncTrialDurationCommand (Background Job)

## Phase

Space-Level Billing — Application Layer Commands

## Purpose

Provides a MediatR command that a background job scheduler can invoke to periodically sync the trial duration from the LemonSqueezy product variant configuration into the local cache. This ensures the cached trial duration stays up-to-date without coupling the sync logic to any specific scheduling framework.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/SyncTrialDurationCommand.cs` | MediatR command (parameterless record) and handler that delegates to `ITrialDurationCache.SyncFromLemonSqueezyAsync` |

## Key decisions

- **No parameters**: The command is a parameterless record because it's a scheduled job with no user input.
- **No validator**: Since there's no user input, FluentValidation is unnecessary.
- **No permission check**: This is a background job command — no user context exists.
- **Single responsibility**: The handler simply delegates to the cache service. All sync logic (HTTP calls, error handling, fallback) lives in the `TrialDurationCache` infrastructure implementation.
- **Follows existing pattern**: Mirrors `ExpireSubscriptionsCommand` — a parameterless background job command with a simple handler.

## How it connects

- **ITrialDurationCache** (Application interface, implemented in Infrastructure) handles the actual LemonSqueezy API call and cache update.
- A background job scheduler (e.g., Hangfire, hosted service) will dispatch this command on a periodic schedule (e.g., every 6 hours).
- The cached trial duration is consumed by `SpaceSubscription.CreateTrial()` when new spaces are created.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application
```

The command compiles and is ready to be dispatched by a background job scheduler.

## What comes next

- Wire the command into a background job scheduler (Hangfire recurring job or IHostedService).
- Implement webhook handling commands (task 6.x).
- Write property tests for checkout and upgrade commands (task 5.7).

## Git commit

```bash
git add -A && git commit -m "feat(billing): add SyncTrialDurationCommand background job"
```
