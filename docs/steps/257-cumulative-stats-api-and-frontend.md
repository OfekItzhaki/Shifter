# 257 — Cumulative Stats API Endpoints and Frontend Integration

## Phase

Phase 5 — API + Frontend (Cumulative Tracking and Periods)

## Purpose

Expose cumulative tracking data through REST API endpoints and wire the frontend to consume historical schedule data and cumulative statistics. This completes the user-facing layer of the cumulative tracking feature.

## What was built

### Backend (API Layer)

- **`apps/api/Jobuler.Application/Scheduling/Queries/GetCumulativeStatsQuery.cs`** — MediatR query + handler that returns per-person cumulative statistics from `cumulative_records`, supporting time-range filtering (7d/14d/30d/90d/period) and period scoping.
- **`apps/api/Jobuler.Application/Scheduling/Queries/GetStatsTimeseriesQuery.cs`** — MediatR query + handler that returns daily time-series data from `daily_snapshots` grouped by date with burden breakdown.
- **`apps/api/Jobuler.Application/Scheduling/Queries/GetHistoricalScheduleQuery.cs`** — MediatR query + handler that delegates to `IAssignmentSnapshotService.GetHistoricalAsync` and returns historical assignments with retention-exceeded flag.
- **`apps/api/Jobuler.Api/Controllers/StatsController.cs`** — Added three new endpoints:
  - `GET /spaces/{spaceId}/stats/cumulative` — cumulative per-person stats
  - `GET /spaces/{spaceId}/stats/timeseries` — daily time-series data
  - `GET /spaces/{spaceId}/schedule/history` — historical schedule from snapshots

### Frontend

- **`apps/web/lib/api/stats.ts`** — Added API client functions: `getHistoricalSchedule`, `getCumulativeStats`, `getStatsTimeseries` with TypeScript interfaces.
- **`apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx`** — When navigating to a past week, fetches historical data from the new endpoint. Shows a blue banner indicating historical view.
- **`apps/web/app/groups/[groupId]/tabs/StatsTab.tsx`** — Added "כל התקופה" (period) option to time-range selector. Added cumulative stats table showing per-person statistics from the new endpoint.

## Key decisions

- The historical schedule endpoint uses a separate route (`/schedule/history`) rather than being on the stats controller route, since it returns schedule data not statistics.
- The cumulative stats endpoint defaults to the current active period when no `period_id` is specified, matching the design requirement.
- Historical data in the ScheduleTab is fetched only when the entire selected week is in the past, avoiding unnecessary API calls for the current week.
- The cumulative stats table in StatsTab is rendered alongside the existing burden stats, not replacing them.

## How it connects

- Backend queries use `IPeriodManager` (from task 9) to resolve the current period.
- Backend queries use `IAssignmentSnapshotService` (from task 7) for historical data.
- Backend queries read from `cumulative_records` and `daily_snapshots` tables (from task 1).
- Frontend API client functions are consumed by the schedule and stats tabs.
- All endpoints enforce `SpaceView` permission per security rules.

## How to run / verify

1. Build the API: `dotnet build` in `apps/api/Jobuler.Api`
2. Verify frontend compiles: check TypeScript diagnostics in the modified files
3. Test endpoints manually:
   - `GET /spaces/{id}/stats/cumulative?group_id={gid}&time_range=7d`
   - `GET /spaces/{id}/stats/timeseries?group_id={gid}&start_date=2024-01-01&end_date=2024-01-31`
   - `GET /spaces/{id}/schedule/history?group_id={gid}&start_date=2024-01-01&end_date=2024-01-07`

## What comes next

- Property tests for stats query scoping (task 16.4) and retention enforcement (task 16.5)
- Final integration checkpoint (task 19)

## Git commit

```bash
git add -A && git commit -m "feat(cumulative): stats API endpoints and frontend historical schedule integration"
```
