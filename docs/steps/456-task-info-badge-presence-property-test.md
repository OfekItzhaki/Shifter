# Step 456 — Task Info Badge Presence Property Test

## Phase

Phase: Recommendation Approval Flow — Property-Based Testing

## Purpose

Validates that the `TaskInfoBadge` component correctly renders exactly one accessible button with the proper `aria-label` for any arbitrary task configuration, and renders nothing when config is null/undefined. This ensures universal accessibility compliance across all possible task configurations.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/schedule/taskInfoBadge.property.test.tsx` | Property-based test using fast-check to verify badge presence and accessibility |

## Key decisions

- Used `fast-check` arbitraries to generate random `TaskConfigSummaryDto` objects covering all valid combinations of task configuration fields
- Tested both the positive case (config provided → badge rendered with correct aria-label) and negative cases (null/undefined → nothing rendered)
- Minimum 100+ iterations per property test (150 for positive cases, 100 for null/undefined cases)
- Followed existing project patterns from `formatTime.property.test.ts` and `fairnessWarning.property.test.ts`

## How it connects

- Validates Requirements 5.1 (badge displayed adjacent to each task name) and 5.3 (accessible aria-label)
- Tests the `TaskInfoBadge` component created in step 449
- Complements the unit tests in step 455

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/schedule/taskInfoBadge.property.test.tsx
```

## What comes next

- Task 6.5: Property test for TaskInfoPopover configuration display
- Task 6.6: Unit tests for TaskInfoBadge and TaskInfoPopover

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval-flow): task info badge presence property test"
```
