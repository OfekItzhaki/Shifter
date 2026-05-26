# 590 — CheckUnderScheduledMembersJob

## Phase

Phase: Self-Service Scheduling — Background Jobs

## Purpose

Implements a recurring background job that detects scheduling cycles whose request window has just closed and dispatches `CheckUnderScheduledMembersCommand` for each. This ensures members below `MinShiftsPerCycle` are flagged and notified automatically without manual admin intervention.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Scheduling/CheckUnderScheduledMembersJob.cs` | BackgroundService that runs every 5 minutes, queries for cycles with recently closed request windows in self-service groups, and dispatches `CheckUnderScheduledMembersCommand` via MediatR for each |
| `apps/api/Jobuler.Api/Program.cs` | Registered `CheckUnderScheduledMembersJob` as a hosted service |

## Key decisions

- **BackgroundService pattern**: Follows the same pattern as `ExpireSubscriptionsJob` and `ProcessExpiredWaitlistOffersJob` — a `BackgroundService` with a 5-minute polling interval rather than Hangfire (the project uses hosted services for recurring jobs).
- **Look-back window**: The job looks back 5 minutes (one interval) to catch cycles whose `RequestWindowClosesAt` fell within the last polling period. This prevents missed windows due to timing drift.
- **Self-service group filter**: Only processes cycles belonging to groups with `SchedulingMode == SelfService`, avoiding false triggers on auto-generated groups.
- **Per-cycle error isolation**: Each cycle is processed independently with its own try/catch, so a failure on one cycle doesn't block processing of others.
- **Startup delay**: Waits 2 minutes after app startup before first run to let the application stabilize.

## How it connects

- Depends on `CheckUnderScheduledMembersCommand` (task 13.1) which handles the actual detection logic and notification dispatch.
- Uses `AppDbContext` to query `SchedulingCycles` and `Groups` tables.
- Dispatches commands via MediatR, maintaining the clean architecture boundary.
- Satisfies Requirements 5.4 (under-scheduled detection), 6.7 (notify admin on window close), and 13.6 (under-scheduled notification).

## How to run / verify

1. Build the solution: `dotnet build` from `apps/api/`
2. The job starts automatically when the API runs
3. To verify behavior: create a self-service group with a scheduling cycle whose `RequestWindowClosesAt` is in the past (within 5 minutes), ensure `MinShiftsPerCycle > 0`, and observe logs for detection output

## What comes next

- Task 17.1: Implement self-service notification events (full notification integration)
- Task 18.1: Register all new services in DI container (final wiring)

## Git commit

```bash
git add -A && git commit -m "feat(self-service): implement CheckUnderScheduledMembersJob background service"
```
