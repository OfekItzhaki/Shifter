# 377 — Redirect & Member Email Bug Condition Exploration Test

## Phase

Bugfix — redirect-and-member-email-fix

## Purpose

Write property-based exploration tests that confirm the existence of three navigation/display bugs before implementing fixes. The tests encode the expected correct behavior and will pass once the bugs are fixed.

## What was built

- `apps/web/__tests__/bugfix/redirect-and-member-email.exploration.test.ts` — PBT exploration test with 5 property assertions covering all 4 bug conditions

## Key decisions

- Used source-code inspection approach (reading file contents) rather than full component rendering to avoid complex mocking of Next.js router, stores, and API clients
- Scoped property tests to concrete failing cases using fast-check generators for each bug condition type
- Tests are designed to FAIL on unfixed code (confirming bugs exist) and PASS after fixes are applied

## How it connects

- Validates Requirements 1.1, 1.2, 1.3, 1.4 from the bugfix spec
- Will be re-run in Task 3.6 to confirm the fixes work
- Complements the preservation tests (Task 2) which verify unchanged behavior

## How to run / verify

```bash
cd apps/web
npx vitest --run __tests__/bugfix/redirect-and-member-email.exploration.test.ts
```

Expected: All 5 tests FAIL on unfixed code (this is correct — proves bugs exist).

## Counterexamples documented

1. **Pricing back link**: `{"type":"pricing_back_click","hasHistory":false}` — source has `href="/login"`, no `router.back()`
2. **Space selection**: `{"type":"space_selection","hasNoRedirectParam":true,"spaceCount":1}` — source has `router.push("/schedule/today")`, not `/groups`
3. **Member modal email**: `{"type":"member_modal_open","email":null}` — no `member.email` rendering in info view
4. **Member edit form email**: `{"type":"member_edit_start","email":"a@a.aa"}` — editForm type lacks `email`, no email input in UI
5. **GroupMemberDto email**: `{"type":"member_modal_open","email":null}` — interface lacks `email` field

## What comes next

- Task 2: Write preservation property tests (verify unchanged behavior on unfixed code)
- Task 3: Implement the actual fixes
- Task 3.6: Re-run this test to confirm fixes work

## Git commit

```bash
git add -A && git commit -m "fix(bugfix): add bug condition exploration tests for redirect and member email issues"
```
