# Step 622 — Pick Last-Group Invalid Memory Property Test

## Phase
Feature: Shift Picker Lite — Property-Based Testing

## Purpose
Validates that the `resolveLastGroup` function correctly returns `null` for any input that is not a valid UUID or any valid UUID that does not appear in the member's self-service groups list. This ensures stale or corrupted localStorage values trigger the group selector instead of silently failing.

## What was built
- `apps/web/__tests__/selfService/pickLastGroup.property.test.ts` — Property-based test file using fast-check with 100+ iterations per property

## Key decisions
- Two sub-properties tested independently: non-UUID strings and valid UUIDs not in the groups list
- Used `fc.uuid()` for generating valid UUIDs and filtered arbitrary strings to exclude accidental UUID matches
- Groups list generated with random scheduling modes to ensure the test covers realistic data shapes
- Minimum 100 iterations per property as specified in the design document

## How it connects
- Tests the `resolveLastGroup` function from `apps/web/lib/utils/pickLastGroup.ts` (created in step 620)
- Validates Requirements 3.2 and 3.5 from the shift-picker-lite spec
- Complements the unit tests in `pickLastGroup.test.ts` and the round-trip property test (task 1.2)

## How to run / verify
```bash
cd apps/web
npx vitest run __tests__/selfService/pickLastGroup.property.test.ts
```

## What comes next
- Property tests for group filtering (task 2.2) and group sorting (task 2.3)

## Git commit
```bash
git add -A && git commit -m "feat(shift-picker-lite): property test for invalid last-group memory clearing"
```
