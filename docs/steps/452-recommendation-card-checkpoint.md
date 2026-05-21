# 452 — Checkpoint: Recommendation Card End-to-End Verification

## Phase

Phase: Recommendation Approval Flow — Task 5

## Purpose

Verify that all changes from tasks 3.1–4.2 (frontend API client cleanup, schedule response type update, RecommendationCard component, and HomeLeaveConfigPanel integration) compile correctly and don't break existing tests.

## What was built

| File | Change |
|------|--------|
| `apps/web/components/recommendations/TaskDoubleShiftSuggestion.tsx` | Removed `useAcceptRecommendation` import and accept-related logic (Enable button, confirmation dialog, success toast) since the hook was deleted in task 3.2. Component now only shows informational text + dismiss button. |

## Key decisions

- The `TaskDoubleShiftSuggestion` component still referenced the removed `useAcceptRecommendation` hook, causing a TypeScript error. Rather than deleting the entire component (which is scheduled for task 8.1), we stripped only the accept-related logic to unblock the build while preserving the dismiss-only informational behavior.
- All pre-existing test failures (Playwright/vitest incompatibility, "No test suite found" warnings, fairnessWarning boundary test) are unrelated to the recommendation-approval-flow changes and were left as-is.

## How it connects

- Depends on tasks 3.1 (removed `acceptRecommendation` API function), 3.2 (removed `useAcceptRecommendation` hook), 3.3 (schedule response type update), 4.1 (RecommendationCard component), 4.2 (HomeLeaveConfigPanel integration)
- Unblocks task 6 (TaskInfoBadge/Popover in schedule grid) and task 8.1 (full removal of TaskDoubleShiftSuggestion)

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit  # No errors related to recommendation/schedule changes
npm run test      # All 234 unit/property tests pass (16 pre-existing suite-level failures unrelated to this feature)
```

## What comes next

- Task 6: Task info badge and popover in schedule grid
- Task 8.1: Full removal of `TaskDoubleShiftSuggestion` component

## Git commit

```bash
git add -A && git commit -m "fix(recommendations): remove useAcceptRecommendation from TaskDoubleShiftSuggestion — checkpoint 5"
```
