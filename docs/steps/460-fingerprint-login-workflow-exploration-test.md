# 460 — Fingerprint Login Workflow Bug Condition Exploration Test

## Phase

Bugfix — Fingerprint/WebAuthn Login Workflow

## Purpose

Write property-based exploration tests that confirm two bugs exist in the fingerprint login workflow before implementing the fix. The tests encode the expected behavior and will validate the fix once implemented.

## What was built

- `apps/web/__tests__/bugfix/fingerprint-login-workflow.exploration.test.ts` — Property-based exploration test using fast-check that verifies:
  - Bug 1 (Token Race Condition): `listCredentials()` should accept an explicit token parameter and the login page should pass the fresh token after login, rather than relying on the apiClient interceptor's localStorage read
  - Bug 2 (Missing Auto-Trigger): `ReAuthDialog` should have a `useEffect` that auto-triggers `handleWebAuthnSubmit()` when `hasWebAuthn` is true and credential loading is complete, plus a `webAuthnDeclined` state to prevent re-triggering after cancel

## Key decisions

- Used source code analysis approach (reading files with `fs.readFileSync`) consistent with existing bugfix exploration test patterns in the codebase
- Tests check for structural code patterns rather than runtime behavior, making them deterministic and fast
- All 4 property tests FAIL on unfixed code, confirming both bugs exist
- Tests will PASS once the fix is implemented (they encode expected behavior)

## How it connects

- This is Task 1 of the fingerprint-login-workflow bugfix spec
- The same test file will be re-run in Task 3.5 to verify the fix works
- Task 2 (preservation tests) can be implemented in parallel
- Tasks 3.1–3.4 implement the actual fix

## How to run / verify

```bash
cd apps/web
npx vitest --run __tests__/bugfix/fingerprint-login-workflow.exploration.test.ts
```

Expected: All 4 tests FAIL on unfixed code (confirms bugs exist).

## Counterexamples documented

**Bug 1 — Token Race Condition:**
- Counterexample: `{"type":"post-login","webAuthnSupported":true,"biometricPromptNotDismissed":true,"existingCredentialCount":0}`
- `listCredentials()` accepts no token parameter
- Login page calls `listCredentials()` with empty parens, relying on interceptor
- `handleSubmit` does not read `localStorage.getItem("access_token")` after login

**Bug 2 — Missing Auto-Trigger:**
- Counterexample: `{"type":"reauth-dialog","hasWebAuthn":true,"credentialLoadingComplete":true}`
- No `useEffect` calls `handleWebAuthnSubmit()` when `hasWebAuthn` is true
- No `webAuthnDeclined` useState hook exists

## What comes next

- Task 2: Write preservation property tests (verify non-buggy behavior is captured)
- Task 3: Implement the fix (token passthrough + auto-trigger useEffect)

## Git commit

```bash
git add -A && git commit -m "fix(webauthn): add bug condition exploration test for fingerprint login workflow"
```
