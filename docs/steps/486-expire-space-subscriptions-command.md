# 486 — Expire Space Subscriptions Command

## Phase

Space-Level Billing — Application Layer Commands

## Purpose

Implements the background job command that expires canceled space subscriptions whose grace period has ended. When a space subscription is canceled, users retain access until `CurrentPeriodEnd`. Once that date passes, this job transitions the subscription to `Expired` status and triggers statistics period closure for all groups in the space.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/ExpireSpaceSubscriptionsCommand.cs` | MediatR command + handler that queries canceled subscriptions past their period end, calls `Expire()` on each, triggers `IStatisticsPeriodService.OnSubscriptionExpiredAsync`, and creates audit log entries |

## Key decisions

- **No parameters**: The command takes no input — it's designed to run as a scheduled background job that processes all eligible subscriptions in one pass.
- **No permission check**: Background jobs run without user context, so no `IPermissionService` call is needed.
- **No FluentValidation validator**: No user input to validate.
- **Audit logging with `Guid.Empty` actor**: Since there's no user triggering this action, the actor is recorded as `Guid.Empty` (same pattern as the existing `ExpireSubscriptionsCommand` for group-level billing).
- **Statistics period closure**: Each expired subscription triggers `OnSubscriptionExpiredAsync` to close active statistics periods for all groups in the space, maintaining accurate billing-cycle-bounded tracking.
- **Null check on `CurrentPeriodEnd`**: The query filters for `CurrentPeriodEnd != null && CurrentPeriodEnd <= now` to avoid processing subscriptions that were canceled during trial (which have no period end).

## How it connects

- **Domain**: Calls `SpaceSubscription.Expire()` which enforces the state machine (only `Canceled` → `Expired` is valid).
- **IStatisticsPeriodService**: Closes active `SubscriptionPeriod` records for all groups in the space when a subscription expires.
- **Scheduling**: This command will be dispatched by a recurring background job (e.g., Hangfire or a hosted service timer) — wiring is handled separately.
- **Existing pattern**: Mirrors `ExpireSubscriptionsCommand` (group-level) but operates on `SpaceSubscriptions` and integrates with the statistics period service.

## How to run / verify

```bash
dotnet build apps/api/Jobuler.Application/Jobuler.Application.csproj
```

The command can be tested by dispatching it via MediatR in a test or integration scenario with canceled subscriptions that have `CurrentPeriodEnd` in the past.

## What comes next

- Wire the command into a recurring background job schedule (hosted service or Hangfire)
- `SyncTrialDurationCommand` (task 5.6) — another background job command
- Property tests for expiry state transition (Property 11 in design)

## Git commit

```bash
git add -A && git commit -m "feat(billing): add ExpireSpaceSubscriptionsCommand background job"
```
