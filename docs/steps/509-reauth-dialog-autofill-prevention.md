# Step 509 — ReAuth Dialog Autofill Prevention

## Phase

Admin Re-Auth Security — Frontend Hardening

## Purpose

Prevent browser password managers from autofilling the re-authentication password field. An unauthorized person with physical access to an unlocked device should not be able to bypass re-auth using saved credentials.

## What was built

| File | Change |
|------|--------|
| `apps/web/components/admin/ReAuthDialog.tsx` | Applied autofill prevention attributes and readonly trick |

### Specific changes in `ReAuthDialog.tsx`:

1. **`autoComplete="new-password"`** on the password input — signals browsers not to offer saved credentials for autofill (was `"current-password"`)
2. **`name="reauth-verify"`** on the password input — defeats heuristic-based autofill by using a non-standard name
3. **`autoComplete="off"`** on the wrapping `<form>` element — prevents the browser from prompting to save the entered password after submission
4. **Readonly trick** — added `isReadonly` state (initialized `true`), set `readOnly={isReadonly}` on the input, removed on `onFocus`. Reset to `true` when dialog opens so it's readonly again on next open.
5. **Password field always empty on open** — verified existing `setPassword("")` in the open effect (already present)

## Key decisions

- Used the "readonly on mount, remove on focus" trick as an additional layer against autofill — some browsers ignore `autocomplete` attributes but respect `readonly`.
- The `isReadonly` state resets to `true` every time the dialog opens, ensuring the trick works on repeated opens.
- The `name="reauth-verify"` is intentionally non-standard to prevent heuristic matching by password managers.

## How it connects

- This is part of Requirement 1 (Prevent Password Manager Autofill on Re-Auth)
- The login form at `/auth/login` is NOT affected — it retains `autocomplete="current-password"` (Requirement 5)
- Works alongside the WebAuthn biometric flow (tasks 3.x) and password form hardening (task 4.2)

## How to run / verify

1. Open the app and navigate to a group admin page
2. Enter management mode to trigger the ReAuth dialog
3. Inspect the password input element — verify:
   - `autocomplete="new-password"`
   - `name="reauth-verify"`
   - `readonly` attribute present initially
   - `readonly` removed after clicking/focusing the input
4. Inspect the `<form>` element — verify `autocomplete="off"`
5. Confirm the browser does NOT autofill the password field
6. Close and reopen the dialog — confirm password field is empty and readonly is re-applied

## What comes next

- Task 4.2: Harden password form validation and error handling
- Task 4.4–4.6: Property tests for dialog state reset, whitespace rejection, and no DOM when closed

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): apply autofill prevention attributes to ReAuthDialog password input"
```
