# Step 589 — GenerateCycleSlotsJob

## Phase

Phase 4 — Background Jobs (Self-Service Scheduling)

## Purpose

Automates the generation of shift slots for upcoming scheduling cycles in self-service groups. Without this job, admins would need to manually trigger slot generation for each cycle. The job ensures members always have upcoming slots available to request.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Scheduling/GenerateCycleSlotsJob.cs` | BackgroundService that runs daily at midnight UTC, queries all active self-service groups, creates upcoming cycles if needed, and calls `ISlotGenerationService.GenerateSlotsForCycleAsync` for each un-generated cycle. |
| `apps/api/Jobuler.Api/Program.cs` | Registered `GenerateCycleSlotsJob` as a hosted service. |

## Key decisions

1. **BackgroundService over Hangfire**: The project uses `BackgroundService` (hosted services) for recurring jobs, not Hangfire. Followed the existing pattern from `ExpireSubscriptionsJob` and `AutoSchedulerService`.
2. **Midnight UTC scheduling**: The job calculates the delay until the next midnight UTC after each run, ensuring it executes once per day at a consistent time.
3. **Automatic cycle creation**: If no upcoming un-generated cycle exists for a group, the job creates one based on the group's `SelfServiceConfig.CycleDurationDays` and request window offsets. This ensures the system is self-sustaining without manual intervention.
4. **Look-ahead window**: Only creates cycles that start within `RequestWindowOpenOffsetHours + CycleDurationDays` from now, preventing creation of cycles too far in the future.
5. **Idempotent execution**: Safe to run multiple times because (a) it only processes cycles where `IsGenerated = false`, (b) the underlying `SlotGenerationService` skips slots that already exist for a given template+date, and (c) cycle creation checks for existing cycles before creating new ones.
6. **Per-group error isolation**: If one group fails, the job logs the error and continues processing other groups.

## How it connects

- **Depends on**: `ISlotGenerationService` (task 5.3), `SelfServiceConfig` entity (task 1.2), `SchedulingCycle` entity (task 1.3), `Group.SchedulingMode` (task 1.1)
- **Consumed by**: Runs automatically as a hosted service — no external trigger needed.
- **Validates**: Requirements 3.1 (slot generation at cycle start) and 3.3 (idempotent generation).

## How to run / verify

1. Build the solution: `dotnet build` from `apps/api/`
2. The job starts automatically with the API host and runs daily at midnight UTC.
3. To verify behavior, check logs for `GenerateCycleSlotsJob` entries showing group processing and slot generation.
4. Idempotency can be verified by restarting the service — no duplicate slots will be created.

## What comes next

- Task 16.2: `ProcessExpiredWaitlistOffersJob` (already implemented)
- Task 16.3: `ExpireSwapRequestsJob`
- Task 16.4: `NotifyRequestWindowOpenJob`
- Task 16.5: `CheckUnderScheduledMembersJob`
- Task 18.1: DI registration of all services (will wire `ISlotGenerationService` → `SlotGenerationService`)

## Git commit

```bash
git add -A && git commit -m "feat(phase4): implement GenerateCycleSlotsJob background service for self-service slot generation"
```
