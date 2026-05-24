# Step 554 — HomeLeaveConfigCard Unit Tests

## Phase

Phase 8 — Space Management Frontend Tests

## Purpose

Provides unit test coverage for the `HomeLeaveConfigCard` component, verifying that mode selection, conditional field rendering, emergency freeze toggle behavior, API payload correctness, and permission gating all work as specified.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/components/spaces/HomeLeaveConfigCard.test.tsx` | 21 unit tests covering all sub-tasks for task 15.2 |

## Key decisions

- Followed the same mocking pattern as `DangerZoneCard.test.tsx` and `ManagementTimeoutCard.test.tsx` — using `vi.hoisted()` for mock functions and mocking `next-intl` with a translation lookup object.
- Used `waitFor` to handle the async config loading before asserting on rendered content.
- Tested mode switching both via initial config state and via user interaction (clicking mode buttons).
- Verified the exact payload shape passed to `updateHomeLeaveConfig` including fields carried over from the loaded config.

## How it connects

- Tests the component created in step 544 (`HomeLeaveConfigCard`)
- Validates requirements 6.1 (home-leave configuration panel) and 6.2 (persist configuration at space level)
- Mocks the API functions from `@/lib/api/spaces` (step 542)

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/components/spaces/HomeLeaveConfigCard.test.tsx
```

All 21 tests should pass.

## What comes next

- Task 16.2: Property test for DangerZoneCard transfer target dropdown
- Task 16.3: Unit tests for DangerZoneCard
- Task 17.2: Unit tests for RoleAssignmentCard

## Git commit

```bash
git add -A && git commit -m "test(space-management): add HomeLeaveConfigCard unit tests"
```
