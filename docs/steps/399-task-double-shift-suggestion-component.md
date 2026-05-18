# 399 — TaskDoubleShiftSuggestion Component

## Phase

Double-Shift Recommendation — Frontend (Task 14.1)

## Purpose

Provides an inline chip/badge component that displays next to the `AllowsDoubleShift` toggle in group task settings. When the recommendation engine detects that enabling double shifts on a task would cover additional slots, this component surfaces that suggestion with actionable buttons.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/recommendations/TaskDoubleShiftSuggestion.tsx` | Inline suggestion component with Enable/Dismiss actions and confirmation dialog |

## Key decisions

- **Renders nothing when no recommendation exists** — the component is safe to render unconditionally; it self-hides via early return when `useRecommendationForTask` returns null.
- **Uses project's existing `Modal` component** for the confirmation dialog asking about triggering a new solver run.
- **Two-step accept flow** — clicking "Enable" opens a confirmation dialog with two options: "Enable & Run Solver" (`triggerNewRun: true`) and "Enable Only" (`triggerNewRun: false`).
- **Dismiss is immediate** — no confirmation dialog needed, calls the dismiss mutation directly.
- **Date range formatting** uses the project's `useDateFormat().fRange()` for locale-aware display.
- **Amber color scheme** for the suggestion chip to draw attention without being alarming.
- **Handles "AlreadyEnabled" outcome** — shows an informational message if the task already has double shifts enabled.

## How it connects

- Consumes `useRecommendationForTask`, `useAcceptRecommendation`, and `useDismissRecommendation` hooks from `lib/query/hooks/useRecommendations.ts` (task 12.2).
- Will be integrated into `TasksTab.tsx` next to the `AllowsDoubleShift` toggle (task 14.2).
- Uses the `Modal` component from `components/Modal.tsx` for the confirmation dialog.
- Relies on the backend `GET /spaces/{spaceId}/tasks/{taskId}/recommendation` and action endpoints (tasks 10.1, 10.2).

## How to run / verify

```bash
# Type-check the component
cd apps/web && npx tsc --noEmit
```

The component renders inline and requires:
- `spaceId` and `taskId` props
- An active recommendation for the given task (otherwise renders nothing)

## What comes next

- Task 14.2: Integrate `TaskDoubleShiftSuggestion` into the group task settings (`TasksTab.tsx`)
- Task 15.1: Full accept flow with solver re-run prompt (shared logic)
- Task 15.2: Dismiss flow integration

## Git commit

```bash
git add -A && git commit -m "feat(recommendations): add TaskDoubleShiftSuggestion inline component"
```
