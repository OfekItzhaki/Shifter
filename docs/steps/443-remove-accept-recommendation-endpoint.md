# 443 — Remove Accept Recommendation Endpoint

## Phase

Recommendation Approval Flow — Backend Simplification

## Purpose

The `AcceptRecommendationCommand` silently enabled `AllowsDoubleShift` on a task when an admin accepted a recommendation. This violates the new passive recommendation model where admins must explicitly toggle settings via the Tasks tab. Removing the accept endpoint and command eliminates the auto-enable path entirely.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Commands/AcceptRecommendationCommand.cs` | **Deleted** — command record, validator, handler, and result types |
| `apps/api/Jobuler.Api/Controllers/RecommendationsController.cs` | Removed the `Accept` action method and `AcceptRecommendationRequest` record |

## Key decisions

- **Full deletion over repurposing**: The accept endpoint is removed entirely rather than repurposed as a dismiss-only action, since the dismiss endpoint already exists and handles that use case.
- **No separate validator deregistration needed**: The `AcceptRecommendationCommandValidator` was defined inline in the command file and auto-discovered by FluentValidation's assembly scanning — deleting the file removes it automatically.
- **Frontend references left for later tasks**: The frontend API client (`acceptRecommendation` function) and React Query hook (`useAcceptRecommendation`) still reference the removed endpoint. These are cleaned up in tasks 3.1 and 3.2.

## How it connects

- Removes the backend path that called `task.EnableDoubleShift()` (task 1.2 removes the method itself)
- The dismiss endpoint (`POST /spaces/{spaceId}/recommendations/{id}/dismiss`) remains unchanged
- All GET endpoints for recommendations remain unchanged
- Frontend cleanup follows in tasks 3.1 and 3.2

## How to run / verify

```bash
cd apps/api && dotnet build --no-restore
```

Build should succeed with no errors. The accept endpoint (`POST /spaces/{spaceId}/recommendations/{id}/accept`) will return 404 since the route no longer exists.

## What comes next

- Task 1.2: Remove `EnableDoubleShift` method from `GroupTask` domain entity
- Task 3.1: Remove `acceptRecommendation` function from frontend API client
- Task 3.2: Remove `useAcceptRecommendation` hook

## Git commit

```bash
git add -A && git commit -m "feat(recommendations): remove AcceptRecommendationCommand and accept endpoint"
```
