# 395 — Recommendations Controller (GET Endpoints)

## Phase

Phase 10 — API Layer (Double-Shift Recommendation)

## Purpose

Exposes the double-shift recommendation data via REST endpoints so the frontend can fetch active recommendations for a group, for a specific solver run (banner), and for a specific task (inline suggestion).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/RecommendationsController.cs` | New controller with three GET endpoints dispatching MediatR queries |

### Endpoints

- `GET /spaces/{spaceId}/groups/{groupId}/recommendations` → dispatches `GetActiveRecommendationsQuery`
- `GET /spaces/{spaceId}/runs/{runId}/recommendations` → dispatches `GetRecommendationsForRunQuery`
- `GET /spaces/{spaceId}/tasks/{taskId}/recommendation` → dispatches `GetRecommendationForTaskQuery`

## Key decisions

- All endpoints require `[Authorize]` at the class level.
- Permission check (`IPermissionService.RequirePermissionAsync` with `Permissions.TasksManage`) runs before dispatching queries, matching the pattern used in `ScheduleRunsController` and `TasksController`.
- The query handlers themselves also perform permission and emergency-freeze checks internally, providing defense-in-depth.
- Returns 200 with the result body (list, banner DTO, or single DTO/null) — consistent with other read endpoints in the project.
- Controller does not contain business logic — it only extracts the user ID from claims, checks permission, and dispatches.

## How it connects

- Depends on `GetActiveRecommendationsQuery`, `GetRecommendationsForRunQuery`, and `GetRecommendationForTaskQuery` (implemented in tasks 8.1–8.3).
- Task 10.2 will add dismiss/accept POST endpoints to this same controller.
- Frontend hooks (`useRecommendations`, `useRecommendationsForRun`, `useRecommendationForTask`) will call these endpoints.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with zero errors.

## What comes next

- Task 10.2: Add dismiss and accept action endpoints to this controller.
- Task 10.3: Register `IRecommendationEngine` in the DI container.

## Git commit

```bash
git add -A && git commit -m "feat(api): add RecommendationsController with GET endpoints"
```
