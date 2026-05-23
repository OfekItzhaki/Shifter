# 511 — Scope Isolation Unit Tests

## Phase

Admin Re-Auth Security — Scope Verification

## Purpose

Ensures that the autofill-prevention attributes applied to the `ReAuthDialog` component do NOT leak into the login form. The login form must continue to support password managers (`autocomplete="current-password"`), while the `ReAuthDialog` must be the only component with autofill-prevention attributes (`autocomplete="new-password"`, `autocomplete="off"`, `name="reauth-verify"`).

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/admin/scopeIsolation.test.tsx` | Unit tests verifying scope isolation between login form and ReAuthDialog |

## Key decisions

- **Separate test file**: Created a dedicated test file for scope isolation rather than adding to existing test files, since this tests the relationship between two different components.
- **Mock pattern**: Used function-reference mocks (`mockApiGet`, `mockApiPost`) instead of inline `vi.fn()` in the factory to survive `vi.clearAllMocks()` calls in `beforeEach`.
- **Login form mocks**: Mocked `next/navigation`, `next/link`, `@/lib/store/authStore`, `@/components/shell/ShifterLogo`, `@/components/LanguageSwitcher`, and `@/lib/utils/detectLocale` to render the login form in isolation without network or routing dependencies.

## How it connects

- Validates Requirements 5.1 and 5.2 from the admin-reauth-security spec
- Complements task 6.1 (manual verification of login form) with automated regression tests
- Ensures future changes to either component don't accidentally break scope isolation

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/admin/scopeIsolation.test.tsx --reporter=verbose
```

All 8 tests should pass:
- 4 tests verify the login form retains password manager support
- 4 tests verify the ReAuthDialog has autofill-prevention attributes (and renders nothing when closed)

## What comes next

- Task 7: Final checkpoint — full integration verification

## Git commit

```bash
git add -A && git commit -m "test(admin-reauth): scope isolation unit tests for login vs reauth autofill"
```
