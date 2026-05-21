# Step 444 — Remove `useAcceptRecommendation` Hook

## Phase

Recommendation Approval Flow — Frontend API Client Cleanup

## Purpose

The `useAcceptRecommendation` hook called the now-removed accept endpoint. Removing it ensures the frontend no longer exposes a mutation for an action that no longer exists on the backend, keeping the API surface consistent with the passive recommendation model.

## What was built

| File | Change |
|------|--------|
| `apps/web/lib/query/hooks/useRecommendations.ts` | Deleted `useAcceptRecommendation` function, `AcceptRecommendationParams` interface, and removed `acceptRecommendation`/`AcceptRecommendationResult` imports |

## Key decisions

- **Only the hook file was modified** — the `TaskDoubleShiftSuggestion` component still imports `useAcceptRecommendation` but is scheduled for full removal in task 8.1. Removing it here would exceed the scope of this task.
- **Kept all other hooks intact** — `useRecommendations`, `useRecommendationsForRun`, `useRecommendationForTask`, and `useDismissRecommendation` remain unchanged.

## How it connects

- Depends on task 1.1 (backend accept endpoint removal) and task 3.1 (API client function removal)
- Task 8.1 will remove `TaskDoubleShiftSuggestion.tsx` which was the only consumer of this hook
- The `useDismissRecommendation` hook remains as the only mutation for recommendation interactions

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

Confirm no type errors in `useRecommendations.ts`. Note: `TaskDoubleShiftSuggestion.tsx` may show an import error until task 8.1 removes it.

## What comes next

- Task 3.3: Update schedule API client to handle new response shape
- Task 8.1: Remove `TaskDoubleShiftSuggestion` component (cleans up the dangling import)

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval): remove useAcceptRecommendation hook"
```
