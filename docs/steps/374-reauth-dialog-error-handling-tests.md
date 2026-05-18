# Step 374 — ReAuth Dialog Error Handling and Recovery Tests

## Phase

Security — Admin Re-Authentication Gate

## Purpose

Verifies that the ReAuthDialog component correctly handles all error scenarios (API 401, 429, network errors, WebAuthn cancellation) with localized messages, and that recovery behavior works properly (password cleared, input re-focused, dialog stays open, isSubmitting reset).

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/admin/reAuthDialogErrorHandling.test.tsx` | 22 unit tests covering error handling and recovery for the ReAuthDialog component |

## Key decisions

- Tested WebAuthn NotAllowedError cancellation by mocking `navigator.credentials.get` to throw a `NotAllowedError`
- Used `vi.useFakeTimers` to verify the `setTimeout`-based re-focus behavior after errors
- Verified all three error code paths: 401 (authFailed), 429 (rateLimited), and network/other errors (networkError)
- Confirmed WebAuthn-specific error paths (cancellation, 401, 429) are handled separately from password errors
- Verified ARIA `role="alert"` with `aria-live="assertive"` for screen reader accessibility

## How it connects

- Validates requirements 3.4, 4.5, 4.6, 5.4 from the admin-reauth-gate spec
- Complements existing tests in `reAuthDialogSubmission.test.tsx` (loading states) and `reAuthDialogCredentials.test.tsx` (credential availability)
- Tests the error handling code paths in `ReAuthDialog.tsx` `handlePasswordSubmit` and `handleWebAuthnSubmit` callbacks

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/admin/reAuthDialogErrorHandling.test.tsx --reporter=verbose
```

All 22 tests should pass.

## What comes next

- Frontend property-based tests (task 5.x) for universal correctness properties
- End-to-end integration wiring verification (task 7.x)

## Git commit

```bash
git add -A && git commit -m "test(admin-reauth): add error handling and recovery unit tests"
```
