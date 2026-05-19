# 414 — Freeze Deactivation Dialog Unit Tests

## Phase

Feature: Freeze Period Discard — Task 7.4

## Purpose

Validates the `FreezeDeactivationDialog` component behavior through unit tests, ensuring the dialog correctly displays change counts, hides/disables the discard toggle based on permissions and data state, and passes the correct discard flag on confirmation.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/home-leave/freezeDeactivationDialog.test.tsx` | 11 unit tests covering all sub-tasks for task 7.4 |

## Key decisions

- Followed existing test patterns from `__tests__/admin/` — Vitest + React Testing Library with mocked `next-intl` and API modules.
- Mocked `getFreezePeriodChangesCount` at the module level to control resolved/rejected states.
- Tests cover all five sub-task scenarios: counts display, permission-based hiding, no-changes hiding, error-state disabling, and confirm flag correctness.

## How it connects

- Tests validate the `FreezeDeactivationDialog` component (task 7.2) against requirements 1.1–1.5 and 5.2.
- Uses the same `@/lib/api/homeLeave` module mocking pattern as other frontend tests.
- Completes the frontend testing layer for the freeze-period-discard feature.

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/home-leave/freezeDeactivationDialog.test.tsx --reporter=verbose
```

All 11 tests should pass.

## What comes next

- Task 8: Final checkpoint — full integration verification.

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): add unit tests for FreezeDeactivationDialog component"
```
