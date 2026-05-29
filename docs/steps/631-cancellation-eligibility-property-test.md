# 631 — Cancellation Eligibility Property Test

## Phase
Shift Picker Lite — Property-Based Tests

## Purpose
Validates that the cancellation eligibility logic correctly determines whether a shift can be cancelled based on the time remaining until the shift starts and the configured cancellation cutoff hours. A shift is cancellable if and only if `shiftStartTime - currentTime > cutoffHours * 3600000` (Requirement 5.4).

## What was built
- `apps/web/lib/utils/pickCancellationEligibility.ts` — Extracted `isCancellable(shiftStartTime, currentTime, cutoffHours)` utility function that accepts Date or ISO string for shift start time
- `apps/web/__tests__/selfService/cancellationEligibility.property.test.ts` — Property-based test using fast-check (100 iterations per property)

## Key decisions
- Extracted the cancellation eligibility logic from the inline `canCancelShift` function in `MyShiftsTab.tsx` into a standalone testable utility
- The utility accepts both `Date` and `string` inputs for `shiftStartTime` to support both parsed dates and raw ISO strings from the API
- Used `fc.date()` for current time generation within a reasonable range (2024-2026) and `fc.integer()` for future offsets (1 minute to 30 days)
- Cutoff hours range is 1-72 to match realistic configuration values
- Tests cover: the core biconditional property, exact boundary behavior (returns false), and string/Date equivalence

## How it connects
- The `isCancellable` utility can replace the inline `canCancelShift` logic in `MyShiftsTab.tsx`
- Part of the shift-picker-lite spec task 11.3, validating Property 7 from the design document

## How to run / verify
```bash
cd apps/web
npx vitest run __tests__/selfService/cancellationEligibility.property.test.ts
```

## What comes next
- Task 12: Final checkpoint — all tests pass

## Git commit
```bash
git add -A && git commit -m "feat(shift-picker-lite): cancellation eligibility utility and property test"
```
