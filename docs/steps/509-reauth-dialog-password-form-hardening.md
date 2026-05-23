# 509 — ReAuth Dialog Password Form Hardening

## Phase

Admin Re-Auth Security — Frontend hardening

## Purpose

Harden the password form validation and error handling in `ReAuthDialog.tsx` to provide clear, differentiated feedback for each failure scenario: empty/whitespace passwords, invalid credentials (401), network errors, and rate limiting (429). This improves UX by retaining the password on network errors (for retry) while clearing it on auth failures, and adds inline validation errors separate from the general error area.

## What was built

| File | Change |
|------|--------|
| `apps/web/components/admin/ReAuthDialog.tsx` | Added `passwordValidationError` state; updated `handlePasswordSubmit` with explicit whitespace validation, differentiated error handling (401 clears password, network error retains it); added inline validation error display below input; updated `onChange` to clear validation error on typing; updated `aria-invalid`/`aria-describedby` for accessibility |
| `apps/web/messages/en.json` | Added keys: `invalidCredentials`, `connectionProblem`, `passwordRequired` |
| `apps/web/messages/he.json` | Added Hebrew translations for the same keys |
| `apps/web/messages/ru.json` | Added Russian translations for the same keys |

## Key decisions

1. **Separate validation error state** — `passwordValidationError` is distinct from `error` so inline validation (below the input) doesn't conflict with general API error messages (shown in the error banner area).
2. **Network error retains password** — On connectivity failures, the password is kept in the field so the user can retry without retyping. On 401 and 429, the password is cleared for security.
3. **Inline error clears on typing** — The `onChange` handler clears `passwordValidationError` as soon as the user starts typing, providing immediate feedback that the issue is resolved.
4. **Border color feedback** — The input border turns red (`#fca5a5`) when a validation error is present, providing visual cue.
5. **Focus management** — After any error (validation or API), focus is returned to the password input via `setTimeout` to ensure the DOM has updated.

## How it connects

- Builds on task 4.1 (autofill prevention attributes already applied to the form/input)
- The lockout logic from task 3.3 (countdown timer, `isLockedOut` state) is preserved and works correctly with the 429 handling
- The `passwordValidationError` state is reset when the dialog opens/closes alongside other state
- Property tests in tasks 4.5 will validate the whitespace rejection behavior

## How to run / verify

1. Open the ReAuth dialog in the browser
2. Try submitting with an empty or whitespace-only password → inline "Password is required" error appears below the input, focus stays on input
3. Type a character → inline error disappears
4. Submit with wrong password → "Invalid credentials" error in banner, password cleared, input refocused
5. Disconnect network and submit → "Connection problem" error in banner, password retained in field
6. Trigger 5+ failures → "Too many attempts" error, submit disabled for cooldown period

## What comes next

- Task 4.3: Verify dialog renders no DOM when closed
- Tasks 4.4–4.6: Property-based tests for dialog state reset, whitespace rejection, and no DOM rendering

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): harden password form validation and error handling"
```
