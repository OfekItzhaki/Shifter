# Step 552 — ManagementTimeoutCard Unit Tests

## Phase

Phase 8 — Space Management Frontend Tests

## Purpose

Validates the `ManagementTimeoutCard` component behavior through unit tests, ensuring it correctly renders the current timeout value, validates input ranges, dispatches API calls with the correct payload, and hides itself for non-owner users.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/spaces/ManagementTimeoutCard.test.tsx` | Unit test suite (13 tests) covering rendering, validation, API dispatch, and permission gating |

## Key decisions

- Used `fireEvent` from `@testing-library/react` to match existing project test conventions (no `@testing-library/user-event` dependency)
- Mocked `@/lib/api/spaces` module to isolate component behavior from network calls
- Mocked `next-intl` with a simple translation lookup function matching the pattern used in other test files
- Tested the "disables save button while saving" behavior using a deferred promise to control async timing

## How it connects

- Tests validate the `ManagementTimeoutCard` component created in task 14.1
- Covers Requirements 5.1 (display current value, owner-only visibility), 5.2 (valid range), 5.3 (reject invalid)
- Follows the same testing patterns established in `__tests__/admin/` for other component tests

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/spaces/ManagementTimeoutCard.test.tsx
```

All 13 tests should pass.

## What comes next

- Task 15.2: Unit tests for HomeLeaveConfigCard
- Task 16.2: Property test for DangerZoneCard transfer target dropdown
- Task 16.3: Unit tests for DangerZoneCard

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add ManagementTimeoutCard unit tests"
```
