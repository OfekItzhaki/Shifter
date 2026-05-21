# 461 — Fingerprint Login Preservation Property Tests

## Phase

Bugfix — Fingerprint Login Workflow

## Purpose

Write property-based preservation tests that capture the existing correct behavior of the login and ReAuthDialog flows BEFORE implementing the bugfix. These tests ensure that non-buggy paths (users with existing credentials, unsupported browsers, dismissed prompts, ReAuthDialog without WebAuthn) continue to work identically after the fix is applied.

## What was built

- `apps/web/__tests__/bugfix/fingerprint-login-preservation.property.test.tsx` — Property-based tests covering:
  - 3.1: Login with existing WebAuthn credentials skips biometric prompt
  - 3.2: Login on browser without WebAuthn support skips biometric prompt
  - 3.3: Login with biometric prompt previously dismissed skips prompt
  - 3.4: ReAuthDialog without WebAuthn credentials shows only password form
  - 3.5: ReAuthDialog on browser without WebAuthn support shows only password form
  - 3.6: ReAuthDialog allows password fallback after WebAuthn cancel
  - 3.7: Conditional mediation login redirects without biometric prompt
  - Combined property: All non-eligible login scenarios result in immediate redirect
  - Combined property: All ReAuthDialog openings without WebAuthn show password-only form

## Key decisions

- Used observation-first methodology: observed behavior on unfixed code, then encoded it as properties
- Tests use fast-check for property-based generation across the input domain
- Includes both logic-level property tests and component-level rendering tests (for ReAuthDialog)
- Tests are designed to PASS on unfixed code (confirms baseline) and continue passing after fix (confirms no regression)

## How it connects

- Depends on: Task 1 (exploration test) — both are in wave 0
- Required by: Tasks 3.1–3.6 (fix implementation) — preservation tests verify no regressions
- Re-run in: Task 3.6 (verify preservation tests still pass after fix)

## How to run / verify

```bash
cd apps/web
npx vitest --run __tests__/bugfix/fingerprint-login-preservation.property.test.tsx
```

All 11 tests should pass.

## What comes next

- Task 3: Implement the actual bugfix (token race condition + ReAuthDialog auto-trigger)
- Task 3.6: Re-run these preservation tests to confirm no regressions

## Git commit

```bash
git add -A && git commit -m "feat(bugfix): fingerprint login preservation property tests"
```
