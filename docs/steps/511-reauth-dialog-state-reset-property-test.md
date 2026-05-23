# 511 — ReAuth Dialog State Reset Property Test

## Phase

Phase 4 — Frontend Autofill Prevention and Password Form Hardening

## Purpose

Validates that the ReAuthDialog always resets the password field to an empty string when transitioning from closed to open, regardless of what was previously typed. This is a property-based test that verifies Requirement 1.3 across 100 randomly generated password strings using fast-check.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/admin/reAuthDialogStateReset.property.test.tsx` | Property-based test using fast-check that generates arbitrary strings, types them into the password field, closes and re-opens the dialog, and asserts the field is always empty |

## Key decisions

- Used `fc.asyncProperty` since the test involves async React rendering with `waitFor`
- Mocked `apiClient.get` to return empty credentials (password-only mode) to keep the test focused on the state reset behavior
- Used `rerender` to simulate open/close transitions rather than unmount/remount, which more accurately reflects how React handles prop changes
- Configured 100 iterations (`numRuns: 100`) as specified in the design document
- Generated strings with `minLength: 1` to ensure meaningful password values are tested

## How it connects

- Validates Requirement 1.3: "WHEN the ReAuth_Dialog opens, THE ReAuth_Dialog SHALL render the password input field with an empty value regardless of any previously stored form data"
- Complements the existing unit tests in `reAuthDialogSubmission.test.tsx` which test specific submission scenarios
- Part of the admin-reauth-security spec's Property 1 as defined in the design document

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/admin/reAuthDialogStateReset.property.test.tsx
```

Expected: 1 test passes with 100 property iterations.

## What comes next

- Task 4.5: Property test for whitespace password rejection (Property 2)
- Task 4.6: Property test for no DOM rendering when closed (Property 3)

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): add property test for dialog state reset on open"
```
