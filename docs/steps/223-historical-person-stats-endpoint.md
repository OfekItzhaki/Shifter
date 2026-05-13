# 223 — Historical Person Stats Endpoint

## Phase

Statistics Overhaul — Phase 2: Enhanced Statistics Backend (Tasks 3.3, 3.4, 3.5)

## Purpose

Provide a time-series API for per-person daily statistics so the frontend can render historical burden trend graphs. This requires persisting daily snapshots after each solver run and exposing them via a new query endpoint.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Domain/Scheduling/FairnessCounterSnapshot.cs` | Added `Update()` method to support upsert logic when the same person+date is updated multiple times in a day |
| `Jobuler.Application/Scheduling/Commands/UpdateFairnessCountersCommand.cs` | After updating the FairnessCounter, now also creates/upserts a `FairnessCounterSnapshot` for today with 30d metrics and burden score |
| `Jobuler.Application/Scheduling/Queries/GetHistoricalPersonStatsQuery.cs` | New query + handler: accepts SpaceId, StartDate, EndDate, optional GroupId; validates date range ≤ 365 days; returns daily per-person stats sorted by date ascending |
| `Jobuler.Api/Controllers/StatsController.cs` | Added `GET /spaces/{spaceId}/stats/historical/persons` endpoint with `[Authorize]` and SpaceView permission check |

## Key decisions

- **Snapshot uses 30d window values**: The snapshot stores `total_assignments`, `hard_count`, `normal_count`, `easy_count`, and `burden_score` from the 30-day rolling window. This gives the most useful historical view.
- **Upsert logic**: If the same person+date snapshot already exists (e.g., multiple solver runs in one day), it updates in place rather than creating duplicates.
- **Burden score formula**: `(hard × 3) − (easy × 1)` as specified in the design.
- **Normal count derived**: `normal_count = total_assignments - hard_count - easy_count` (not stored separately in the fairness counter, computed for the snapshot).
- **Group filtering via GroupMemberships join**: When GroupId is provided, only people who are members of that group are included in results.
- **Validation throws InvalidOperationException**: Consistent with the project's error handling middleware which maps these to 400 responses.

## How it connects

- `UpdateFairnessCountersCommand` is called by `SolverWorkerService` after each solver run — snapshots are now automatically persisted.
- The new `GetHistoricalPersonStatsQuery` reads from `fairness_counter_snapshots` table (created in task 3.2).
- The `StatsController` endpoint is consumed by the frontend's Recharts line/bar charts (Phase 4).
- The `FairnessCounterSnapshot` entity and EF configuration were created in task 3.2.

## How to run / verify

1. Build: `dotnet build` in `apps/api` — should succeed with no errors.
2. After a solver run completes, verify a row appears in `fairness_counter_snapshots` for today.
3. Call `GET /spaces/{spaceId}/stats/historical/persons?startDate=2024-01-01&endDate=2024-01-31` — should return data points sorted by date.
4. Call with `groupId` param to verify group filtering works.
5. Call with date range > 365 days — should return 400.

## What comes next

- Phase 3: Task Rotation Tracking (army-template groups)
- Phase 4: Frontend graphs consuming this endpoint via Recharts

## Git commit

```bash
git add -A && git commit -m "feat(stats): historical person stats endpoint with snapshot persistence"
```
