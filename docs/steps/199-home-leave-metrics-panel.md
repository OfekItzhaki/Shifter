# 199 — HomeLeaveMetricsPanel Component

## Phase

Phase 6 — Frontend UI (Home-Leave Visualization)

## Purpose

Provides a visual panel showing per-person base-time vs. home-time statistics for closed-base groups. Enables group admins to quickly assess schedule fairness and identify imbalances in leave distribution.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/home-leave/HomeLeaveMetricsPanel.tsx` | React client component displaying "זמן בבסיס / בבית" panel with per-person stats, stacked bar charts, and fairness warning |

## Key decisions

- **Pure CSS/Tailwind bar charts** — No chart library needed; simple `div` elements with percentage widths provide lightweight stacked bars.
- **Blue for base-time, emerald for home-time** — Consistent with the project's existing color palette (`blue-500` for on-mission states, `emerald-500` for free/home states as seen in `LiveStatusPanel`).
- **Fairness warning at 15pp threshold** — Amber banner with warning icon appears when `max(baseTimeRatio) - min(baseTimeRatio) > 0.15`, matching requirement 9.4.
- **Hebrew locale sorting** — Uses `localeCompare` with `"he"` locale for alphabetical name sorting.
- **Null/empty guard** — Returns `null` when metrics array is empty or nullish, hiding the panel entirely per requirement 9.6.
- **Exported interface** — `HomeLeaveMetric` interface is exported for reuse by parent components and tests.

## How it connects

- Consumed by the schedule version detail page when viewing a closed-base group's schedule output
- Receives data from the solver output's `home_leave_metrics` field (via API response)
- Complements the timeline/Gantt view (task 13.2) which shows leave slots visually on the schedule

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit  # Type-check the component
```

The component renders when passed a non-empty `metrics` array and hides when the array is empty/null.

## What comes next

- Task 13.2: Render home-leave slots on the schedule timeline with distinct visual style
- Task 16.3: Property-based test for the fairness warning threshold logic

## Git commit

```bash
git add -A && git commit -m "feat(phase6): HomeLeaveMetricsPanel component with stacked bars and fairness warning"
```
