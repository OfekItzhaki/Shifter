# 394 — GetActiveRecommendationsQuery Handler

## Phase

Feature: Double-Shift Recommendation — Application Layer Queries

## Purpose

Implements the `GetActiveRecommendationsQuery` MediatR handler that returns active double-shift recommendations for a group, filtered by the user's group role permission level and emergency freeze state. This is the primary query used by the frontend to display the list of actionable recommendations.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Queries/GetActiveRecommendationsQuery.cs` | Query record + handler that checks group role permission (ViewAndEdit/Owner), emergency freeze, and returns active recommendations sorted by impact |

## Key decisions

- **Group role permission check instead of space-level permission**: The spec (Req 6.1, 6.5) requires checking the user's group role `PermissionLevel`, not the space-level `TasksManage` permission. This is different from the command handlers which use `RequirePermissionAsync`.
- **Return empty instead of throwing**: Per spec, queries return an empty list for unauthorized users rather than throwing 403. This provides a graceful degradation for the frontend.
- **Space owner bypass**: Space owners implicitly have full access (per architecture rules), so they always see recommendations regardless of group role.
- **Ordering**: Results are sorted by `AdditionalSlotsCovered` DESC, then `TaskName` ASC, matching the ranking defined in the design document.

## How it connects

- Used by `RecommendationsController` (task 10.1) to serve `GET /spaces/{spaceId}/groups/{groupId}/recommendations`
- Depends on `DoubleShiftRecommendation` entity (task 1.1), `HomeLeaveConfig` entity, `PersonRoleAssignment`, and `SpaceRole`
- Frontend `useRecommendations(groupId)` hook (task 12.2) will call this endpoint

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no new warnings.

## What comes next

- Task 8.2: `GetRecommendationsForRunQuery` (banner data for a specific solver run)
- Task 8.3: `GetRecommendationForTaskQuery` (inline suggestion for a specific task)
- Task 10.1: Wire this query into the `RecommendationsController`

## Git commit

```bash
git add -A && git commit -m "feat(double-shift): implement GetActiveRecommendationsQuery handler"
```
