# Step 365 — formatLocalTime Property-Based Tests

## Phase

Phase 6 — Frontend auth store and time formatting utility

## Purpose

Validates the correctness of the `formatLocalTime` utility and `toUtcIsoString` function across all valid inputs using property-based testing. These tests ensure DST-aware time display works correctly for any UTC datetime and timezone combination, and that outgoing API requests always preserve UTC without client-side offset corruption.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/formatTime.property.test.ts` | Property-based tests using fast-check covering Properties 7 and 8 from the design document |

## Key decisions

1. **Curated timezone list** — Used a representative set of 25 IANA timezone IDs covering diverse UTC offsets, DST rules, and geographic regions rather than generating arbitrary strings. This avoids false failures from deprecated/exotic timezone IDs while still providing broad coverage.
2. **Independent verification via Intl.DateTimeFormat** — Each property test independently computes the expected result using `Intl.DateTimeFormat` to verify the utility's output, ensuring the test oracle is separate from the implementation.
3. **200 iterations per property** — Exceeds the minimum 100 specified in the design document for higher confidence.
4. **Date range 2000–2030** — Covers multiple DST transition cycles across all timezones in the curated list.

## How it connects

- Validates the `formatLocalTime` utility created in task 6.2 (`lib/utils/formatTime.ts`)
- Ensures the contract that all time displays use IANA timezone IDs for correct DST handling (Requirement 5.3)
- Ensures the contract that outgoing API requests always send UTC (Requirement 5.4)
- Complements the unit tests in `__tests__/formatTime.test.ts` which test specific examples

## How to run / verify

```bash
cd apps/web
npx vitest --run __tests__/formatTime.property.test.ts
```

All 7 property tests should pass (3 for Property 7, 4 for Property 8).

## What comes next

- Task 7.1: Integrate formatLocalTime across all time-rendering components
- Task 8.1: Create Settings page route and layout

## Git commit

```bash
git add -A && git commit -m "feat(timezone): property-based tests for formatLocalTime utility"
```
