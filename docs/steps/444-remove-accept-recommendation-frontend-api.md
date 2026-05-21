# 444 — Remove `acceptRecommendation` from Frontend API Client

## Phase

Recommendation Approval Flow — Frontend API Client Cleanup

## Purpose

The backend `AcceptRecommendationCommand` and its endpoint have been removed (steps 443). The frontend API client still exports the `acceptRecommendation` function and its related types (`AcceptRecommendationResult`, `AcceptRecommendationOutcome`). This step removes them to keep the API layer consistent with the backend and prevent dead code.

## What was built

| File | Change |
|------|--------|
| `apps/web/lib/api/recommendations.ts` | Deleted `AcceptRecommendationOutcome` type, `AcceptRecommendationResult` interface, and `acceptRecommendation` function |

## Key decisions

- Only the API client types and function were removed in this step. The `useAcceptRecommendation` hook (in `lib/query/hooks/useRecommendations.ts`) and the `TaskDoubleShiftSuggestion` component that consumes it are handled in subsequent tasks (3.2 and 8.1 respectively).
- The remaining functions (`getRecommendations`, `getRecommendationsForRun`, `getRecommendationForTask`, `dismissRecommendation`) are unchanged.

## How it connects

- **Depends on**: Step 443 (backend accept endpoint removal)
- **Depended on by**: Task 3.2 (remove `useAcceptRecommendation` hook), Task 8.1 (remove `TaskDoubleShiftSuggestion` component)

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

The file should compile cleanly. Downstream files (`useRecommendations.ts`) will have import errors until task 3.2 is completed.

## What comes next

- Task 3.2: Remove `useAcceptRecommendation` hook from `lib/query/hooks/useRecommendations.ts`
- Task 3.3: Update schedule API client to handle new response shape

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval): remove acceptRecommendation from frontend API client"
```
