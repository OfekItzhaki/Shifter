# 518 — Solver Worker Regeneration Handling

## Phase

Schedule Regeneration Feature — Worker Integration

## Purpose

Updates the solver background worker (`SolverWorkerService`) to handle regeneration runs differently from standard runs. When the solver completes successfully for a regeneration trigger, the worker creates a `ScheduleVersion` via the `CreateRegenerationDraft` factory (which sets `SupersedesVersionId` and `SourceType = "regeneration"`), links the result version to the run, and skips auto-publish. On failure, the run is marked failed and the published version remains unchanged.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs` | Added regeneration branch in version creation: uses `CreateRegenerationDraft` when `triggerMode == "regeneration"` |
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs` | Added `run.SetResultVersion(version.Id)` call before marking run completed (for all run types) |
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs` | Modified auto-publish guard to skip regeneration runs — regeneration drafts always require admin review |

## Key decisions

1. **Reuse existing flow**: The regeneration branch reuses the same payload normalization, solver call, assignment parsing, diff summary, and fairness counter logic as standard runs. Only the version creation factory and post-processing differ.
2. **SetResultVersion for all runs**: Added `run.SetResultVersion(version.Id)` for all successful runs (not just regeneration). This enables the frontend to poll the run and navigate to the draft review panel once complete.
3. **No auto-discard of existing drafts**: Unlike some standard run flows, regeneration drafts coexist with other drafts. The worker does not discard existing drafts when a regeneration run completes.
4. **No auto-publish**: Regeneration drafts are never auto-published even if the group has `AutoPublish` enabled. The admin must explicitly review and publish.
5. **Failure handling unchanged**: On solver failure (timeout, infeasibility, error), the existing failure handling applies identically — the run is marked failed with an error summary and the published version remains unchanged.

## How it connects

- **Upstream**: `TriggerRegenerationCommand` (task 4.1) enqueues a `SolverJobMessage` with `triggerMode = "regeneration"` and `BaselineVersionId` set to the current published version.
- **Downstream**: The created `ScheduleVersion` with `SupersedesVersionId` is used by the publish flow (task 9.1) to include regeneration metadata in audit logs. The `ResultVersionId` on the run is used by the frontend polling (task 10.3) to navigate to the draft review panel.
- **Domain**: Uses `ScheduleVersion.CreateRegenerationDraft` factory (task 1.2) and `ScheduleRun.SetResultVersion` method (task 1.1).

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
dotnet build Jobuler.Tests/Jobuler.Tests.csproj
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "SolverWorker"
```

## What comes next

- Property tests for successful regeneration draft creation (task 7.2)
- Property tests for failed regeneration error recording (task 7.3)
- Property tests for regeneration period assignment bounds (task 7.4)
- Property tests for published version immutability (task 7.5)
- Publish audit log metadata for regeneration (task 9.1)

## Git commit

```bash
git add -A && git commit -m "feat(scheduling): handle regeneration trigger mode in solver background worker"
```
