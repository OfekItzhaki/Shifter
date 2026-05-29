# 631 — Slot Sorting Property Test

## Phase

Shift Picker Lite — Property-Based Testing

## Purpose

Validates Property 5 from the shift-picker-lite design: slot sorting is date-first then time-second. Extracts the inline sorting logic from `SlotBrowserTab` into a testable utility function and verifies it with fast-check property-based tests.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/utils/pickSlotSort.ts` | Extracted `sortSlotsByDateTime` utility that sorts `AvailableSlotDto[]` by date ascending then start time ascending |
| `apps/web/__tests__/selfService/slotSorting.property.test.ts` | Property-based test (100 iterations) verifying the sort ordering invariant, length preservation, and immutability |
| `apps/web/app/groups/[groupId]/tabs/SlotBrowserTab.tsx` | Updated to import and use the extracted `sortSlotsByDateTime` utility |

## Key decisions

- Extracted sorting logic into a pure utility function (`pickSlotSort.ts`) to make it independently testable without component rendering
- Used `localeCompare` for string comparison of ISO date and HH:mm time formats (lexicographic ordering matches chronological ordering for these formats)
- Added two supplementary properties (length preservation and immutability) alongside the main ordering property

## How it connects

- The `sortSlotsByDateTime` utility is used by `SlotBrowserTab` for displaying slots in chronological order
- Validates Requirement 4.1: slots displayed sorted by date ascending then start time ascending
- Part of the Property 5 correctness guarantee from the design document

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/selfService/slotSorting.property.test.ts
```

## What comes next

- Task 11.2: Property test for capacity indicator format (Property 6)
- Task 11.3: Property test for cancellation eligibility (Property 7)

## Git commit

```bash
git add -A && git commit -m "feat(shift-picker-lite): extract slot sort utility and add property test"
```
