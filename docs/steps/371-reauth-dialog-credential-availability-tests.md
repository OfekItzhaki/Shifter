# 371 — ReAuthDialog Credential Availability Verification Tests

## Phase

Admin Re-Authentication Gate — Frontend Verification

## Purpose

Verify that the ReAuthDialog component correctly checks WebAuthn credential availability and conditionally renders authentication methods based on browser support and user credentials.

## What was built

- `apps/web/__tests__/admin/reAuthDialogCredentials.test.tsx` — Unit tests verifying:
  - Dialog fetches WebAuthn credential availability via `listCredentials()` when dialog opens
  - `listCredentials()` is NOT called when `isWebAuthnSupported()` returns false
  - Password input is always rendered (system invariant: all registered users have a password)
  - Password input has `autocomplete="current-password"` attribute
  - WebAuthn button is only rendered when browser supports it AND user has registered credentials
  - WebAuthn button is NOT rendered when browser doesn't support WebAuthn
  - WebAuthn button is NOT rendered when user has no registered credentials
  - Both methods shown when user has both (Req 2.5)
  - Only password shown when WebAuthn unavailable (Req 2.6)
  - Graceful fallback to password-only when `listCredentials()` throws
  - Dialog renders nothing when `open` is false

## Key decisions

- No code changes were needed — the existing ReAuthDialog implementation already correctly handles all credential availability scenarios
- Tests validate the existing behavior to prevent regressions
- Tests follow the project's established patterns (vitest, @testing-library/react, vi.mock for next-intl and API client)

## How it connects

- Validates requirements 2.3, 2.4, 2.5, 2.6, 4.6, 9.2 from the admin-reauth-gate spec
- Tests the integration between `ReAuthDialog` and `@/lib/webauthn` utilities (`isWebAuthnSupported`, `listCredentials`)
- Complements task 3.4 (accessibility) and task 3.5 (loading/submission states)

## How to run / verify

```bash
cd apps/web
npx vitest --run __tests__/admin/reAuthDialogCredentials.test.tsx
```

All 13 tests should pass.

## What comes next

- Task 3.3: Implement disabled button with tooltip for no-credentials state
- Task 3.4: Verify ReAuthDialog accessibility compliance
- Task 3.5: Verify loading and submission state handling

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): verify credential availability check in ReAuthDialog"
```
