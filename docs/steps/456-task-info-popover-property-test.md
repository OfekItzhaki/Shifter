# Step 456 — Task Info Popover Property Test

## Phase

Feature: Recommendation Approval Flow — Property-Based Testing

## Purpose

Validates that the TaskInfoPopover component's display logic correctly handles arbitrary task configurations. Uses fast-check to generate random `TaskConfigSummaryDto` values and asserts that:
- Default configs show only the "defaultSettings" message
- Non-default configs always show the core fields (doubleShift, overlap, timeWindow, burden)
- Split count is shown only when > 1
- Qualifications are shown only when non-empty
- Time window shows "24/7" when both daily times are null

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/schedule/taskInfoPopover.property.test.ts` | Property-based test for TaskInfoPopover configuration display logic |

## Key decisions

- Followed the project pattern of testing **pure logic** extracted from the component rather than rendering with `@testing-library/react` (same approach as `fairnessWarning.property.test.ts`)
- Generated arbitrary configs using `fc.tuple` with appropriate arbitraries for each field type
- Used `fc.option` for nullable time strings and `fc.constantFrom` for burden levels
- Minimum 100 iterations per property test (most use 200)

## How it connects

- Validates Requirement 6.1 from the recommendation-approval-flow spec
- Tests the same logic used in `components/schedule/TaskInfoPopover.tsx`
- Complements the unit tests in task 6.6

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/schedule/taskInfoPopover.property.test.ts
```

## What comes next

- Task 6.6: Unit tests for TaskInfoBadge and TaskInfoPopover
- Task 8.3: Property test for dismiss preserving task state

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval-flow): property test for TaskInfoPopover config display"
```
