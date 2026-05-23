# 511 — ReAuth Dialog Whitespace Password Rejection Property Test

## Phase

Admin Re-Auth Security — Frontend Property Tests

## Purpose

Validates that the ReAuthDialog correctly rejects whitespace-only passwords across all possible whitespace combinations. This property-based test ensures Requirement 3.4 holds universally: for any string composed entirely of whitespace characters, form submission is prevented, no API call is made to the re-authenticate endpoint, and a validation error is displayed to the user.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/admin/reAuthDialogWhitespaceRejection.property.test.tsx` | Property-based test using fast-check that generates arbitrary whitespace-only strings (spaces, tabs, newlines, carriage returns, form feeds, vertical tabs) and verifies the ReAuthDialog prevents submission, makes no API call, and displays a validation error |

## Key decisions

- **fast-check with 100 iterations**: Configured `numRuns: 100` as specified in the design document to provide strong confidence across diverse whitespace combinations.
- **Whitespace character variety**: The arbitrary generates strings from spaces, tabs, newlines (`\n`), carriage returns (`\r`), form feeds (`\f`), and vertical tabs (`\v`) to cover all standard whitespace characters.
- **Full component rendering**: Each iteration renders the actual ReAuthDialog component with mocked API client to test the real validation logic end-to-end.
- **Three-assertion pattern**: Each iteration asserts (1) no API call to `/auth/re-authenticate`, (2) `onSuccess` not called, and (3) validation error alert is displayed with "Password is required" text.
- **Credential check mocked to empty**: The `GET /auth/webauthn/credentials` endpoint returns an empty array so the dialog shows the password form (no WebAuthn).

## How it connects

- Validates the whitespace check in `handlePasswordSubmit` (`if (!password.trim())`) in `ReAuthDialog.tsx`
- Complements the unit tests in `reAuthDialogSubmission.test.tsx` which test specific submission scenarios
- Part of the admin-reauth-security spec's Property 2 correctness guarantee
- Requirement 3.4 ensures users cannot accidentally submit empty/whitespace passwords

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/admin/reAuthDialogWhitespaceRejection.property.test.tsx --reporter=verbose
```

Expected: 1 test passes, running 100 fast-check iterations in ~4 seconds.

## What comes next

- Task 4.6: Property test for no DOM rendering when closed (Property 3)
- Task 6.2: Unit tests for scope isolation (login form vs ReAuthDialog)

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): add property test for whitespace password rejection"
```
