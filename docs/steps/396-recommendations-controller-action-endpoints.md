# 396 — Recommendations Controller Action Endpoints

## Phase

Feature: Double-Shift Recommendation — API Layer

## Purpose

Adds POST action endpoints to the `RecommendationsController` for dismissing and accepting double-shift recommendations. These endpoints allow admins to act on recommendations by either dismissing them (hiding from view) or accepting them (enabling double shift on the referenced task with an optional solver re-run).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/RecommendationsController.cs` | Created full controller with GET endpoints (task 10.1) and POST dismiss/accept endpoints (task 10.2). Includes `AcceptRecommendationRequest` DTO with optional `TriggerNewRun` boolean. |

## Key decisions

- **Single controller file**: Combined GET (list/detail) and POST (action) endpoints in one controller since they share the same resource and authorization pattern.
- **No route prefix on controller**: Used full route paths on each action method to support multiple route patterns (`spaces/{spaceId}/groups/...`, `spaces/{spaceId}/runs/...`, `spaces/{spaceId}/tasks/...`, `spaces/{spaceId}/recommendations/...`).
- **Permission checks in handlers**: The controller relies on `[Authorize]` for authentication. Permission checks (ViewAndEdit/Owner) are handled inside the MediatR command/query handlers via `IPermissionService`, following the architecture rules.
- **Optional request body for accept**: The `AcceptRecommendationRequest` body is nullable — if omitted, `TriggerNewRun` defaults to `false`.
- **No additional FluentValidation in controller**: Validators are already defined in the command files (`AcceptRecommendationCommandValidator`, `DismissRecommendationCommandValidator`).
- **HTTP status codes**: Dismiss returns 204 (no content). Accept returns 200 with the result DTO. The handlers throw `KeyNotFoundException` which the `ExceptionHandlingMiddleware` maps to 404.

## How it connects

- Dispatches `DismissRecommendationCommand` and `AcceptRecommendationCommand` from the Application layer (task 7.1, 7.2)
- Dispatches `GetActiveRecommendationsQuery`, `GetRecommendationsForRunQuery`, `GetRecommendationForTaskQuery` from the Application layer (task 8.1, 8.2, 8.3)
- Frontend hooks (task 12.2) will call these endpoints via `useDismissRecommendation()` and `useAcceptRecommendation()` mutations
- `ExceptionHandlingMiddleware` handles `KeyNotFoundException` → 404 and `UnauthorizedAccessException` → 403

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with no errors.

## What comes next

- Task 10.3: Register `IRecommendationEngine` in DI container
- Task 12: Frontend API hooks and TypeScript types

## Git commit

```bash
git add -A && git commit -m "feat(recommendations): add dismiss and accept action endpoints to RecommendationsController"
```
