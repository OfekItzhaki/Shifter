# 651 — Rest Duration Property & Unit Tests

## Phase

Feature: rest-duration-display (optional test tasks)

## Purpose

Add comprehensive property-based tests and unit tests for the rest duration utility functions (`computeRestDurations`, `formatRestDuration`, `getRestColorClass`). These tests verify correctness properties hold across all valid inputs and validate specific edge cases.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/restDuration.property.test.ts` | Property-based tests using fast-check (4 properties, 11 test cases, 100+ iterations each) |
| `apps/web/__tests__/restDuration.test.ts` | Unit tests for specific examples and edge cases (19 test cases) |

## Key decisions

- Used `Math.fround()` for `fc.float` constraints as required by fast-check v3.x for 32-bit float boundaries
- Property tests generate random assignments with arbitrary personIds and timestamps to verify gap computation correctness
- Color classification property test covers all three branches (below, equal, above threshold)
- Terminal assignment property verifies N assignments → N-1 rest entries invariant
- Unit tests cover locale-specific formatting (en, he, ru), overlapping assignments, and cross-task computation

## How it connects

- Tests validate the utility functions in `apps/web/lib/utils/restDuration.ts`
- Properties correspond to the design document's correctness properties 1–4
- Validates requirements 1.1, 1.2, 1.4, 1.5, 4.1, 4.2, 4.3, 5.1, 5.2, 5.3, 6.3

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/restDuration
```

All 30 tests (11 property + 19 unit) should pass.

## What comes next

These tests complete the optional testing tasks for the rest-duration-display feature. No further tasks depend on them.

## Git commit

```bash
git add -A && git commit -m "feat(rest-duration): add property-based and unit tests for rest duration utilities"
```
