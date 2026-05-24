# Step 555 — RoleAssignmentCard Unit Tests

## Phase

Phase 8 — Frontend Tests (Space Management)

## Purpose

Validates the `RoleAssignmentCard` component behavior through unit tests, ensuring it correctly renders members with permission levels, dispatches API calls on role changes, shows success/error toasts, and hides for non-owners.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/components/spaces/RoleAssignmentCard.test.tsx` | Unit tests covering 4 test scenarios with 13 individual test cases |

## Key decisions

- Used a stable `tFn` reference for the `useTranslations` mock to prevent `useCallback` re-creation loops caused by unstable function references in the `next-intl` mock.
- Mocked `getSpaceMembers` and `getSpacePermissionLevels` API functions to simulate async data loading.
- Used `act` + `waitFor` pattern to properly handle the component's async `useEffect` data fetching lifecycle.
- Used never-resolving promises to test the loading state without triggering state updates.

## How it connects

- Tests the `RoleAssignmentCard` component (step 545) which implements Requirement 4.6 (role assignment UI).
- Validates the component's integration with the `assignSpaceRole` API client function (step 542).
- Part of the space-management spec's frontend test suite (Task 17.2).

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/components/spaces/RoleAssignmentCard.test.tsx
```

All 13 tests should pass.

## What comes next

- Task 18.2: Invite code section unit tests (already done in step 551)
- Final checkpoint (Task 19) — full integration verification

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add RoleAssignmentCard unit tests"
```
