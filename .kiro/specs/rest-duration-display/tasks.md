# Implementation Plan: Rest Duration Display

## Overview

This plan implements inline rest duration indicators in the schedule view, showing admins the time gap between a person's consecutive assignments. The implementation is entirely frontend — a pure computation utility calculates gaps from already-loaded assignment data, a presentational component renders the result with color-coding, and i18n keys provide locale-aware labels. The approach starts with the pure utility functions (easily testable), then the React component, then integration into the existing ScheduleTaskTable, and finally i18n wiring.

## Tasks

- [x] 1. Create rest duration utility functions
  - [x] 1.1 Implement `computeRestDurations` in `apps/web/lib/utils/restDuration.ts`
    - Create the file with `RestDurationInput` and `RestDurationEntry` interfaces
    - Implement grouping by `personId`, sorting by `slotStartsAt` ascending
    - Compute gap as `(next.slotStartsAt - current.slotEndsAt) / 3_600_000` for each consecutive pair
    - Skip assignments with missing/invalid `personId`, `slotStartsAt`, or `slotEndsAt`
    - Clamp negative gaps (overlapping assignments) to 0
    - Return `RestDurationEntry[]` — one entry per assignment that has a subsequent assignment
    - _Requirements: 1.1, 1.2, 1.5, 5.1, 5.2, 5.3, 7.1, 7.3_

  - [x] 1.2 Implement `formatRestDuration` in the same file
    - Accept `hours: number` and `locale: "en" | "he" | "ru"`
    - For hours >= 24: return `"${days}d ${remainingHours}h"` with locale-appropriate abbreviations
    - For hours < 24: return `"${hours}h"` with locale-appropriate abbreviation
    - Use `Math.floor` for integer display values
    - Locale map: en → (h, d), he → (ש, י), ru → (ч, д)
    - _Requirements: 1.4, 6.3_

  - [x] 1.3 Implement `getRestColorClass` in the same file
    - Accept `restHours: number` and `minRestThresholdHours: number`
    - Return `"text-red-600"` when `restHours < minRestThresholdHours`
    - Return `"text-amber-600"` when `restHours === minRestThresholdHours`
    - Return `"text-slate-500"` when `restHours > minRestThresholdHours`
    - _Requirements: 4.1, 4.2, 4.3_

  - [x]* 1.4 Write property test for rest gap computation correctness
    - **Property 1: Rest gap computation correctness**
    - Generate random assignment arrays with arbitrary personIds, task types, and timestamps
    - Verify computed gaps equal `(next.slotStartsAt - current.slotEndsAt) / 3_600_000` for chronologically sorted pairs
    - Verify results are ordered chronologically per person
    - Test file: `apps/web/__tests__/restDuration.property.test.ts`
    - **Validates: Requirements 1.1, 1.2, 5.1, 5.2, 5.3**

  - [x]* 1.5 Write property test for duration formatting correctness
    - **Property 2: Duration formatting correctness**
    - Generate random positive hour values and random supported locales
    - Verify "Xd Yh" pattern when hours >= 24, "Xh" pattern when < 24
    - Verify X = floor(hours/24), Y = floor(hours % 24) for the days+hours case
    - **Validates: Requirements 1.4, 6.3**

  - [x]* 1.6 Write property test for color classification correctness
    - **Property 3: Color classification correctness**
    - Generate random (restHours, threshold) pairs with positive threshold
    - Verify `text-red-600` when rest < threshold, `text-amber-600` when equal, `text-slate-500` when greater
    - **Validates: Requirements 4.1, 4.2, 4.3**

  - [x]* 1.7 Write property test for terminal assignment count
    - **Property 4: No rest entry for terminal assignments**
    - Generate random sets of assignments per person (1 to N assignments)
    - Verify a person with 1 assignment produces 0 rest entries
    - Verify a person with N assignments produces exactly N-1 rest entries
    - **Validates: Requirements 1.5**

- [x] 2. Checkpoint — Utility functions complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Create RestDurationBadge component and integrate into ScheduleTaskTable
  - [x] 3.1 Create `RestDurationBadge` component in `apps/web/components/schedule/RestDurationBadge.tsx`
    - Accept props: `restHours: number`, `minRestThresholdHours: number`
    - Use `useLocale()` from `next-intl` to get current locale
    - Use `useTranslations("schedule")` for the "rest" label suffix
    - Render a `<span>` with `text-[10px] font-normal` styling
    - Call `formatRestDuration(restHours, locale)` for the duration text
    - Append localized "rest" label from translation key `schedule.rest.label`
    - Apply color class from `getRestColorClass(restHours, minRestThresholdHours)`
    - _Requirements: 3.1, 3.2, 3.3, 4.4, 6.1, 6.2_

  - [x] 3.2 Add `minRestHours` prop to `ScheduleTaskTable` and wire rest duration display
    - Add optional `minRestHours?: number` prop to the `Props` interface
    - Call `computeRestDurations(assignments)` memoized with `useMemo`
    - Build a lookup `Map<string, RestDurationEntry>` keyed by `${personId}|${slotStartsAt}`
    - In each person cell render: if `isAdmin && minRestHours != null && restEntry exists`, render `<RestDurationBadge>` below the person name
    - _Requirements: 2.1, 2.2, 2.3, 3.1, 4.4_

  - [x] 3.3 Pass `minRestHours` from the parent schedule page to `ScheduleTaskTable`
    - Identify the parent component that renders `ScheduleTaskTable` (ScheduleTab or group schedule page)
    - Read `minRestBetweenShiftsHours` from the group constraints/settings data already loaded
    - Pass it as the `minRestHours` prop to `ScheduleTaskTable`
    - _Requirements: 4.4, 7.2_

  - [x]* 3.4 Write unit tests for RestDurationBadge and ScheduleTaskTable integration
    - Test: render with `isAdmin=false` → no badges visible
    - Test: render with `isAdmin=true` + `minRestHours` → badges appear for assignments with subsequent assignments
    - Test: person with single assignment → no badge
    - Test: overlapping assignments (negative gap) → shows "0h" in red
    - Test: exactly 24h gap → shows "1d 0h" format
    - Test: locale-specific output for he, en, ru
    - Test file: `apps/web/__tests__/restDuration.test.ts`
    - _Requirements: 1.4, 1.5, 2.1, 2.2, 4.1, 4.2, 4.3_

- [x] 4. Add i18n translation keys for all three locales
  - [x] 4.1 Add rest duration translation keys to locale files
    - Add `schedule.rest.label`, `schedule.rest.hoursAbbrev`, `schedule.rest.daysAbbrev` to `apps/web/messages/en.json`
    - Add corresponding Hebrew translations to `apps/web/messages/he.json` (מנוחה, ש, י)
    - Add corresponding Russian translations to `apps/web/messages/ru.json` (отдых, ч, д)
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 5. Final checkpoint — Full integration
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use **fast-check** (already in project test dependencies, minimum 100 iterations per property)
- Unit tests validate specific examples and edge cases
- This is a frontend-only feature — no backend changes, no API calls, no migrations
- The `computeRestDurations` utility is a pure function making it ideal for property-based testing
- The existing `isAdmin` prop on `ScheduleTaskTable` is reused for visibility gating
- Step documentation under `docs/steps/` should be created alongside each implementation task per workspace conventions

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["1.4", "1.5", "1.6", "1.7", "4.1"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["3.2"] },
    { "id": 4, "tasks": ["3.3", "3.4"] }
  ]
}
```
