# Step 502 — SpaceBillingCard Unit Tests

## Phase

Phase 14 — Frontend: SpaceBillingCard component

## Purpose

Validates the SpaceBillingCard component renders correctly for each subscription status, respects permission gating, and handles error states with a retry mechanism.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/billing/spaceBillingCard.test.tsx` | Unit tests for SpaceBillingCard covering date display per status, permission gating, and error/retry behavior |

## Key decisions

- Used `vi.hoisted()` for mock function declaration to work with Vitest's module mock hoisting
- Mocked `@/lib/api/billing` module to control API responses per test
- Followed existing project test conventions (describe/it blocks, `@testing-library/react`, `waitFor` for async state)
- Tests cover all subscription statuses: trialing, active, canceled, and null (no subscription)
- Verified YYYY-MM-DD date formatting for each status type

## How it connects

- Tests validate the `SpaceBillingCard` component created in task 14.1
- Covers requirements 4.1–4.6 from the space-billing spec
- Uses the same `SpaceSubscriptionDto` type from `@/lib/api/billing` (task 13.1)

## How to run / verify

```bash
cd apps/web
npx vitest --run __tests__/billing/spaceBillingCard.test.tsx
```

All 12 tests should pass.

## What comes next

- Task 15: Final checkpoint — full integration verification

## Git commit

```bash
git add -A && git commit -m "feat(space-billing): add SpaceBillingCard unit tests"
```
