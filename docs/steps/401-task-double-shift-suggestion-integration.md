# 401 — Integrate TaskDoubleShiftSuggestion into Group Task Settings

## Phase

Feature: Double-Shift Recommendation — Frontend Integration

## Purpose

Surfaces the inline double-shift recommendation suggestion next to the `AllowsDoubleShift` toggle in the task edit form, so admins see actionable recommendations in context when editing a task.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/tabs/TasksTab.tsx` | Imported `TaskDoubleShiftSuggestion`, added `spaceId` prop, rendered the suggestion component below the flags section when editing a task with `allowsDoubleShift` currently false |
| `apps/web/app/groups/[groupId]/page.tsx` | Passed `currentSpaceId` as the `spaceId` prop to `TasksTab` |

## Key decisions

- The `TaskDoubleShiftSuggestion` component is rendered **below** the flags row (double-shift + overlap toggles) rather than inline within the label, to avoid layout issues with the chip/badge width
- Rendering is conditional on three factors: (1) editing an existing task (`editingTask` is non-null), (2) `allowsDoubleShift` is currently false in the form (Req 6.2), and (3) `spaceId` is available
- The component itself handles the "no recommendation exists" case internally (renders null), so no additional data-fetching logic is needed in `TasksTab`

## How it connects

- Depends on `TaskDoubleShiftSuggestion` component (step 399)
- Depends on `useRecommendationForTask` hook (step 398)
- Depends on backend `GET /spaces/{spaceId}/tasks/{taskId}/recommendation` endpoint (step 395)
- Satisfies Requirements 3.3 (inline suggestion in task settings) and 6.2 (hide when already enabled)

## How to run / verify

1. Open a group's Tasks tab and click "Edit" on a task where `AllowsDoubleShift` is false
2. If a recommendation exists for that task, the amber suggestion chip should appear below the toggles
3. Check the toggle on → the suggestion should disappear immediately
4. For a task where `AllowsDoubleShift` is already true, no suggestion should appear
5. Run `npx tsc --noEmit` in `apps/web/` to verify no type errors

## What comes next

- Task 15.1: Accept recommendation flow with solver re-run prompt
- Task 15.2: Dismiss recommendation flow
- Task 17.4: Frontend component unit tests

## Git commit

```bash
git add -A && git commit -m "feat(double-shift-recommendation): integrate TaskDoubleShiftSuggestion into group task settings"
```
