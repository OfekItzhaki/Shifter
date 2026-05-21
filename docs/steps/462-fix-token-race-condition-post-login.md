# 462 — Fix Token Race Condition in Post-Login listCredentials Call

## Phase

Bugfix — Fingerprint Login Workflow (Bug 1)

## Purpose

After a successful email/password login, the app calls `listCredentials()` to determine if the user should be offered biometric registration. The `apiClient` interceptor reads the access token from `localStorage`, but there is a race condition where the token may not be reliably available to the interceptor at the exact moment of the call. This results in a 401 that is silently caught, preventing the biometric registration prompt from ever appearing.

The fix passes the fresh access token directly to `listCredentials()` after login, bypassing the interceptor's localStorage read entirely.

## What was built

| File | Change |
|------|--------|
| `apps/web/lib/webauthn.ts` | Added optional `token` parameter to `listCredentials()`. When provided, the token is used directly in the `Authorization` header, bypassing the apiClient interceptor. |
| `apps/web/app/login/page.tsx` | Modified `handleSubmit` to read `localStorage.getItem("access_token")` immediately after `await login(email, password)` and pass it to `listCredentials(freshToken)`. |

## Key decisions

- **Explicit token passing over timing hacks**: Rather than adding `await Promise.resolve()` or `setTimeout` delays to "wait" for the interceptor to pick up the token, we pass the token explicitly. This is deterministic and eliminates the race condition entirely.
- **Optional parameter preserves backward compatibility**: The `token` parameter is optional, so all existing callers of `listCredentials()` (e.g., settings pages, credential management) continue to work unchanged via the interceptor.
- **apiClient still used for the request**: We pass the token as a header override to `apiClient.get()` rather than using raw `axios`. This preserves the response interceptor behavior (refresh logic, error routing) for this call.

## How it connects

- Fixes Bug 1 from the fingerprint-login-workflow bugfix spec
- The exploration test (task 1) should now pass for the post-login scenario
- Preservation tests (task 2) remain unaffected since non-buggy paths don't use the new token parameter

## How to run / verify

1. Run the exploration test: `npx vitest run --reporter=verbose apps/web/__tests__/webauthn-bug-exploration.test.ts`
2. Run the preservation tests: `npx vitest run --reporter=verbose apps/web/__tests__/webauthn-preservation.test.ts`
3. Manual verification: Log in with email/password on a WebAuthn-supported browser with no registered credentials → biometric prompt should appear

## What comes next

- Task 3.2: Add WebAuthn auto-trigger useEffect in ReAuthDialog
- Task 3.3: Add webAuthnDeclined state and update cancel handling
- Task 3.5: Verify bug condition exploration test passes

## Git commit

```bash
git add -A && git commit -m "fix(webauthn): pass fresh token to listCredentials after login to fix race condition"
```
