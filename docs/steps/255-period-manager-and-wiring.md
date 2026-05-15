# 255 — PeriodManager Service and Application Layer Wiring

## Phase

Phase 3 — Application Layer (Cumulative Tracking and Periods)

## Purpose

Implements the `IPeriodManager` service that manages subscription period lifecycle (open/close/query), and wires all cumulative tracking services into the existing command handlers so that publishing, rollback, presence edits, and subscription changes trigger the appropriate cumulative tracking operations.

## What was built

| File | Action | Description |
|------|--------|-------------|
| `apps/api/Jobuler.Application/Scheduling/IPeriodManager.cs` | Created | Interface defining `OpenPeriodAsync`, `ClosePeriodAsync`, `GetCurrentPeriodAsync` |
| `apps/api/Jobuler.Infrastructure/Scheduling/PeriodManager.cs` | Created | Implementation that creates/closes periods and calls `ICumulativeTracker.ResetPeriodCountersAsync` on open |
| `apps/api/Jobuler.Api/Program.cs` | Modified | Registered `IPeriodManager` → `PeriodManager` in DI |
| `apps/api/Jobuler.Application/Scheduling/Commands/PublishVersionCommand.cs` | Modified | Injected `IAssignmentSnapshotService` and `ICumulativeTracker`; calls `CreateSnapshotsAsync` then `UpdateOnPublishAsync` after publish |
| `apps/api/Jobuler.Application/People/Commands/AddPresenceWindowCommand.cs` | Modified | Injected `ICumulativeTracker`; calls `RecomputeForPersonAsync` when AtHome window is created |
| `apps/api/Jobuler.Application/People/Commands/DeletePresenceWindowCommand.cs` | Modified | Injected `ICumulativeTracker`; calls `RecomputeForPersonAsync` when AtHome window is deleted |
| `apps/api/Jobuler.Application/Scheduling/Commands/RollbackVersionCommand.cs` | Modified | Injected `ICumulativeTracker`; calls `RecomputeForPersonAsync` for all affected persons after rollback |
| `apps/api/Jobuler.Application/Billing/Commands/ActivateSubscriptionCommand.cs` | Created | Command handler that activates subscription and calls `IPeriodManager.OpenPeriodAsync` |
| `apps/api/Jobuler.Application/Billing/Commands/CancelSubscriptionCommand.cs` | Created | Command handler that cancels subscription and calls `IPeriodManager.ClosePeriodAsync` |
| `apps/api/Jobuler.Application/Groups/Commands/CreateGroupCommand.cs` | Modified | Injected `IPeriodManager`; calls `OpenPeriodAsync` after creating trial subscription |
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Modified | Injected `ICumulativeTracker`; calls `GetForSolverPayloadAsync` and includes `cumulative_tracking` in solver payload |
| `apps/api/Jobuler.Tests/Groups/GroupOwnershipPropertyTests.cs` | Modified | Updated constructor calls to pass mock `IPeriodManager` |
| `apps/api/Jobuler.Tests/Integration/AdminManagementIntegrationTests.cs` | Modified | Updated constructor calls to pass mock services |
| `apps/api/Jobuler.Tests/Integration/SolverWorkerPipelineTests.cs` | Modified | Updated constructor calls to pass mock `ICumulativeTracker` |

## Key decisions

- **PeriodManager closes existing active period before opening new one** — prevents orphaned active periods if `OpenPeriodAsync` is called without explicit close.
- **Presence window hooks only trigger on AtHome state** — `RecomputeForPersonAsync` is only needed when home-leave windows change, not for FreeInBase edits (those don't affect the consecutive-hours-at-base calculation reference point).
- **Rollback recomputes for all affected persons** — rather than trying to reverse-engineer counter decrements, we do a full recomputation from presence_windows per the design doc.
- **New billing commands created** — since no existing handler for subscription activation/cancellation existed, `ActivateSubscriptionCommand` and `CancelSubscriptionCommand` were created as the proper integration points.
- **Solver payload includes cumulative_tracking only when group-scoped** — cumulative data is per-group, so it's only included when `groupId` is provided to the normalizer.

## How it connects

- **PeriodManager** depends on `ICumulativeTracker` for resetting counters on period open.
- **PublishVersionCommand** now triggers the full snapshot + counter update pipeline.
- **SolverPayloadNormalizer** now includes cumulative data so the Python solver can use it for eligibility and fairness.
- The billing commands will be called from a future Stripe webhook controller.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore   # Should succeed with 0 errors
dotnet test --no-build      # Existing tests should pass
```

## What comes next

- Python solver extension (Task 14) to consume `cumulative_tracking` data
- Stats API endpoints (Task 16) for historical statistics
- Frontend historical schedule viewing (Task 18)

## Git commit

```bash
git add -A && git commit -m "feat(phase3): period manager service and cumulative tracking wiring"
```
