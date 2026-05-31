# 646 — Rest Duration Utility Functions

## Phase

Feature: Rest Duration Display (frontend-only)

## Purpose

Provides the pure computation layer for the rest duration display feature. These utilities calculate the time gap between a person's consecutive assignments, format the result for locale-aware display, and determine color-coding based on a configurable minimum rest threshold.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/utils/restDuration.ts` | Three pure utility functions and their TypeScript interfaces |

### Functions

1. **`computeRestDurations(assignments)`** — Groups assignments by person, sorts chronologically, computes the gap in hours between each consecutive pair. Skips invalid entries and clamps negative gaps to 0.

2. **`formatRestDuration(hours, locale)`** — Formats a numeric hour value into a localized string. Uses "Xd Yh" pattern for >= 24h and "Xh" for < 24h, with locale-appropriate abbreviations (en: h/d, he: ש/י, ru: ч/д).

3. **`getRestColorClass(restHours, minRestThresholdHours)`** — Returns a Tailwind color class based on threshold comparison: red below, amber at, slate above.

### Interfaces

- `RestDurationInput` — Minimal input shape (personId, slotStartsAt, slotEndsAt)
- `RestDurationEntry` — Output shape with computed restHours and reference timestamps
- `SupportedLocale` — Union type for "en" | "he" | "ru"

## Key decisions

- **Pure functions with no side effects** — ideal for property-based testing and memoization in React components.
- **ISO 8601 string comparison for sorting** — safe because all timestamps are UTC ISO strings with consistent format.
- **Negative gap clamping to 0** — overlapping assignments display as "0h" with red color rather than showing confusing negative values.
- **Math.floor for display** — avoids fractional hours in the UI (e.g., 8.7h displays as "8h").
- **No timezone conversion in computation** — gap arithmetic uses raw UTC millisecond differences; timezone is only relevant for display context handled elsewhere.

## How it connects

- The `RestDurationBadge` component (task 3.1) will call `formatRestDuration` and `getRestColorClass`.
- The `ScheduleTaskTable` integration (task 3.2) will call `computeRestDurations` with the full assignments array.
- Property-based tests (tasks 1.4–1.7) will validate these functions against formal correctness properties.

## How to run / verify

```bash
# Type-check (from apps/web)
npx tsc --noEmit

# Once property tests are written (tasks 1.4-1.7):
npx vitest run "restDuration"
```

## What comes next

- Tasks 1.4–1.7: Property-based tests for all three functions
- Task 3.1: `RestDurationBadge` component that uses these utilities
- Task 3.2: Integration into `ScheduleTaskTable`

## Git commit

```bash
git add -A && git commit -m "feat(rest-duration): add computeRestDurations, formatRestDuration, and getRestColorClass utilities"
```
