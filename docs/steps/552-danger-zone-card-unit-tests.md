# 552 — DangerZoneCard Unit Tests

## Phase

Phase 8 — Frontend Tests (Space Management)

## Purpose

Validates the `DangerZoneCard` component behavior through unit tests, ensuring confirmation dialogs appear before destructive actions, the member dropdown correctly excludes the current owner, success/error messages display after API calls, and the component is hidden for non-owners.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/components/spaces/DangerZoneCard.test.tsx` | 15 unit tests covering Req 9.1, 9.2, 9.3, 9.4 |

## Key decisions

- Used `getByRole("button", { name })` to disambiguate between heading text and button text that share the same translation key
- Mocked `@/lib/api/spaces` with `vi.hoisted()` pattern for proper mock hoisting
- Mocked `next-intl` with a translation map returning readable strings for assertions
- Tested both success and error paths for delete and transfer operations

## How it connects

- Tests the `DangerZoneCard` component created in task 16.1
- Validates requirements 9.1 (danger zone visibility), 9.2 (confirmation before delete), 9.3 (transfer success/error messages), 9.4 (dropdown excludes owner)
- Complements the property test in task 16.2 which validates the dropdown exclusion property across arbitrary member lists

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/components/spaces/DangerZoneCard.test.tsx
```

## What comes next

- Task 17.2: RoleAssignmentCard unit tests
- Task 18.2: Invite code section unit tests
- Task 19: Final integration checkpoint

## Git commit

```bash
git add -A && git commit -m "feat(phase8): add DangerZoneCard unit tests (task 16.3)"
```
