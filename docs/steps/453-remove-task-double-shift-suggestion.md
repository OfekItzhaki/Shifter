# 453 — Remove TaskDoubleShiftSuggestion Component

## Phase

Recommendation Approval Flow — Integration wiring and cleanup (Task 8.1)

## Purpose

The old action-oriented `TaskDoubleShiftSuggestion` component is no longer needed. It was replaced by the passive `RecommendationCard` component (task 4.1) which displays recommendations above the emergency freeze section instead of inline next to the double-shift toggle. Removing this component eliminates dead code and completes the transition to the informational recommendation model.

## What was built

| File | Change |
|------|--------|
| `apps/web/components/recommendations/TaskDoubleShiftSuggestion.tsx` | **Deleted** — the entire component file was removed |
| `apps/web/app/groups/[groupId]/tabs/TasksTab.tsx` | Removed the import of `TaskDoubleShiftSuggestion` and the conditional rendering block that displayed it below the flags section |

## Key decisions

- Only the component file and its direct usages were removed. The `useRecommendationForTask` hook (which was the component's data source) was left in place since removing it is a separate concern and it may still be useful for other features.
- The `SuccessToast` component in the recommendations folder was already unused (its import was removed in the checkpoint step 452) — no additional cleanup needed.

## How it connects

- Depends on task 4.1 (RecommendationCard) which provides the replacement UI
- Depends on task 3.2 (removal of `useAcceptRecommendation` hook) which removed the accept mutation
- Completes the frontend cleanup for the recommendation approval flow refactor
- The `RecommendationCard` in `HomeLeaveConfigPanel` is now the sole recommendation UI

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

Confirm no TypeScript errors related to `TaskDoubleShiftSuggestion`. The component should no longer exist and no imports should reference it.

## What comes next

- Task 8.2: Wire schedule data fetching to pass `taskConfigurations` to `ScheduleTable2D`
- Task 8.3: Property test for dismiss preserving task state
- Task 8.4: Integration tests

## Git commit

```bash
git add -A && git commit -m "feat(recommendations): remove deprecated TaskDoubleShiftSuggestion component"
```
