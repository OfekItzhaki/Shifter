# 378 — Redirect & Member Email Fix: Preservation Property Tests

## Phase
Bugfix — redirect-and-member-email-fix

## Purpose
Write property-based tests that capture the existing correct behavior (non-buggy paths) BEFORE implementing the fix. These tests ensure that the fix does not introduce regressions in:
- Login redirect parameter handling
- Default post-login redirect
- Member edit form field passthrough
- Permission enforcement (non-admin read-only, owner edit)
- Pricing page accessibility without auth

## What was built
- `apps/web/__tests__/redirect-and-member-email/preservation.property.test.ts` — 9 property-based tests using fast-check

## Key decisions
- Tests extract and validate the pure logic (redirect resolution, form shape, permission checks) rather than rendering full React components — this makes them fast, deterministic, and independent of DOM mocking
- Used observation-first methodology: read the unfixed source code, documented the behavior, then encoded it as properties
- Tests validate Requirements 3.1–3.6 from the bugfix spec

## How it connects
- These tests will be re-run after the fix (task 3.7) to confirm no regressions
- They complement the bug condition exploration test (task 1) which validates the buggy paths

## How to run / verify
```bash
cd apps/web
npx vitest --run __tests__/redirect-and-member-email/preservation.property.test.ts
```
All 9 tests should pass on both unfixed and fixed code.

## What comes next
- Task 3: Implement the actual fix (pricing back link, space redirect, member email)
- Task 3.7: Re-run these preservation tests to confirm no regressions

## Git commit
```bash
git add -A && git commit -m "test(bugfix): preservation property tests for redirect and member email fix"
```
