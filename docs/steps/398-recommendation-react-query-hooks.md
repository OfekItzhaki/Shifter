# 398 — Recommendation React Query Hooks

## Phase

Feature: Double-Shift Recommendation — Frontend API Layer

## Purpose

Provides React Query hooks for fetching and mutating recommendation data from the frontend. These hooks abstract the API calls and handle caching, invalidation, and loading states for the recommendation feature.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/recommendations.ts` | Added API functions (`getRecommendations`, `getRecommendationsForRun`, `getRecommendationForTask`, `dismissRecommendation`, `acceptRecommendation`) to the existing types file |
| `apps/web/lib/query/keys.ts` | Added `recommendations`, `recommendationsForRun`, and `recommendationForTask` query keys |
| `apps/web/lib/query/hooks/useRecommendations.ts` | Created React Query hooks: `useRecommendations`, `useRecommendationsForRun`, `useRecommendationForTask`, `useDismissRecommendation`, `useAcceptRecommendation` |

## Key decisions

- **Followed existing patterns**: Hooks mirror the structure of `useNotifications.ts` — same import style, `enabled` guards, and invalidation approach.
- **Broad invalidation on mutations**: Both dismiss and accept mutations invalidate all three recommendation query key prefixes to ensure UI consistency across banner, inline suggestions, and group-level lists.
- **`spaceId` as parameter**: All hooks accept `spaceId` as a parameter (consistent with other hooks in the project) rather than pulling from context.
- **Typed mutation params**: `useAcceptRecommendation` uses a typed params object with `recommendationId` and `triggerNewRun` for clarity.

## How it connects

- **Upstream**: Consumes the API client (`apiClient`) and types defined in `apps/web/lib/api/recommendations.ts` (task 12.1).
- **Downstream**: Used by `RecommendationBanner` (task 13.1), `TaskDoubleShiftSuggestion` (task 14.1), and accept/dismiss flows (task 15.1, 15.2).
- **Backend**: Calls endpoints defined in `RecommendationsController` (task 10.1, 10.2).

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

All three files should compile without errors.

## What comes next

- Task 13.1: `RecommendationBanner` component (uses `useRecommendationsForRun`)
- Task 14.1: `TaskDoubleShiftSuggestion` component (uses `useRecommendationForTask`)
- Task 15.1/15.2: Accept and dismiss flows (use mutation hooks)

## Git commit

```bash
git add -A && git commit -m "feat(recommendations): add React Query hooks for recommendation API"
```
