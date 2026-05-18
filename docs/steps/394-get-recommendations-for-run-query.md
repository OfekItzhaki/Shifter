# 394 — Get Recommendations For Run Query

## Phase

Feature: Double-Shift Recommendation (Task 8.2)

## Purpose

Provides a query handler that returns recommendation banner data for a specific solver run. This powers the inline banner on the solver results / draft schedule page, showing admins which tasks could benefit from enabling double shifts.

## What was built

- `apps/api/Jobuler.Application/Scheduling/Queries/GetRecommendationsForRunQuery.cs` — MediatR query handler that:
  - Accepts `SpaceId`, `RunId`, `UserId`
  - Checks user permission via `HasPermissionAsync` (returns `null` if not ViewAndEdit/Owner)
  - Checks `EmergencyFreezeActive` on the group's `HomeLeaveConfig` (returns `null` if frozen)
  - Queries active `DoubleShiftRecommendation` entities for the run
  - Returns a `RecommendationBannerDto` with up to 5 recommendations, remaining count, total uncovered slots, and affected date range

## Key decisions

- Uses `HasPermissionAsync` (soft check returning bool) instead of `RequirePermissionAsync` (throws 403) because the spec says "return empty" for insufficient permissions, not throw an error
- Gets the `GroupId` from the first recommendation to check `EmergencyFreezeActive`, since `ScheduleRun` doesn't have a direct `GroupId` property
- Returns `null` (not an empty DTO) when no recommendations exist or access is denied — the controller/frontend can interpret null as "no banner to show"
- Caps banner recommendations at 5 with a `RemainingCount` for the "+N more" indicator
- Date range formatted as `dd/MM/yyyy - dd/MM/yyyy` spanning earliest start to latest end across all recommendations

## How it connects

- Used by `RecommendationsController` (task 10.1) at `GET /spaces/{spaceId}/runs/{runId}/recommendations`
- Returns `RecommendationBannerDto` (defined in task 3.2)
- Queries `DoubleShiftRecommendation` entities (created by the engine in task 5.2)
- Frontend `useRecommendationsForRun` hook (task 12.2) consumes this endpoint
- `RecommendationBanner` component (task 13.1) renders the returned data

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

## What comes next

- Task 8.3: `GetRecommendationForTaskQuery` (inline suggestion for task settings)
- Task 10.1: Wire this query into the `RecommendationsController`

## Git commit

```bash
git add -A && git commit -m "feat(double-shift): implement GetRecommendationsForRunQuery handler"
```
