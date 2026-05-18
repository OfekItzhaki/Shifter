# 401 — Accept Recommendation Success Toast

## Phase

Feature: Double-Shift Recommendation — Frontend Accept Flow

## Purpose

Adds success toast feedback to the accept recommendation flow. Previously, accepting a recommendation closed the confirmation dialog silently without any visible success confirmation. This step adds a floating success toast that confirms the action was completed, matching the project's existing toast pattern (SessionTimeoutToast).

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/recommendations/SuccessToast.tsx` | Reusable floating success toast component with auto-dismiss, following the same pattern as `SessionTimeoutToast` |
| `apps/web/components/recommendations/TaskDoubleShiftSuggestion.tsx` | Updated to show success toast after accepting a recommendation — contextual message differs based on whether a solver re-run was triggered |

## Key decisions

- Created a dedicated `SuccessToast` component in the recommendations folder rather than a global toast system, matching the project's existing pattern of purpose-built toast components
- Toast message is contextual: includes solver run info when `triggerNewRun: true`, simpler message when `false`
- The "AlreadyEnabled" outcome still shows as an inline informational message (not a toast), since it's a different UX concern
- Dialog text changed from "has been enabled" to "will be enabled" since the dialog appears before the action completes
- Auto-dismiss after 5 seconds (slightly shorter than the session timeout toast's 6s, since this is a less critical notification)

## How it connects

- Completes the accept flow defined in Requirements 4.1, 4.2, 4.5
- Uses the existing `useAcceptRecommendation` mutation hook which already handles query invalidation
- The `SuccessToast` component can be reused by other recommendation components if needed

## How to run / verify

1. Navigate to group task settings where a recommendation exists
2. Click "Enable" on the inline suggestion
3. In the confirmation dialog, click either "Enable & Run Solver" or "Enable Only"
4. Verify a green success toast appears at the bottom-right with the appropriate message
5. Verify the toast auto-dismisses after 5 seconds or can be manually closed

## What comes next

- Task 15.2: Implement dismiss recommendation flow
- Task 17.4: Unit tests for frontend components

## Git commit

```bash
git add -A && git commit -m "feat(recommendations): add success toast to accept recommendation flow"
```
