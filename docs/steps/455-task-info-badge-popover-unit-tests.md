# 455 — TaskInfoBadge and TaskInfoPopover Unit Tests

## Phase

Feature: Recommendation Approval Flow — Task 6.6

## Purpose

Validates the TaskInfoBadge and TaskInfoPopover components with unit tests covering visibility, accessibility, localization, and interaction behavior.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/schedule/taskInfoBadgeAndPopover.test.tsx` | 18 unit tests covering badge visibility, popover content, click-outside close, and localized strings |

## Key decisions

- Followed existing project pattern of mocking `next-intl` with a translation map that returns readable strings for assertions
- Placed tests in `__tests__/schedule/` directory to match the component location pattern
- Used `@testing-library/react` with `fireEvent` for interaction testing (consistent with other tests in the project)
- Tested both null and undefined config to ensure badge hides in all falsy cases

## How it connects

- Tests validate `TaskInfoBadge` (Req 5.3, 7.3) and `TaskInfoPopover` (Req 6.2, 6.3, 6.4)
- Components were implemented in tasks 6.1 and 6.2
- These tests complement the property-based tests from tasks 6.4 and 6.5

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/schedule/taskInfoBadgeAndPopover.test.tsx
```

All 18 tests should pass.

## What comes next

- Task 7.1 (localization keys) is already complete
- Task 8.1–8.4 (integration wiring and cleanup) follow

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval): add unit tests for TaskInfoBadge and TaskInfoPopover"
```
