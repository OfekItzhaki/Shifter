# 630 — Capacity Format Property Test

## Phase
Shift Picker Lite — Property-Based Tests

## Purpose
Validates that the capacity indicator formatting logic correctly produces the `"{currentFillCount}/{capacity}"` string for any valid input values. This ensures the slot browser always displays capacity information in the expected format (Requirement 4.2).

## What was built
- `apps/web/lib/utils/pickCapacityFormat.ts` — Extracted `formatCapacity(currentFill, capacity)` utility function
- `apps/web/__tests__/selfService/capacityFormat.property.test.ts` — Property-based test using fast-check (100 iterations)

## Key decisions
- Extracted the inline `{slot.currentFillCount}/{slot.capacity}` formatting into a standalone testable utility function rather than testing the component directly
- Used `fc.nat()` for currentFillCount (non-negative integers) and `fc.integer({ min: 1 })` for capacity (positive integers) to match the domain constraints
- Kept the utility minimal — a single pure function with no dependencies

## How it connects
- The `formatCapacity` utility can be used by `SlotBrowserTab` and `AdminOverridesTab` to replace inline formatting
- Part of the shift-picker-lite spec task 11.2, validating Property 6 from the design document

## How to run / verify
```bash
cd apps/web
npx vitest run __tests__/selfService/capacityFormat.property.test.ts
```

## What comes next
- Task 11.3: Cancellation eligibility property test (Property 7)
- Task 12: Final checkpoint — all tests pass

## Git commit
```bash
git add -A && git commit -m "feat(shift-picker-lite): capacity format utility and property test"
```
