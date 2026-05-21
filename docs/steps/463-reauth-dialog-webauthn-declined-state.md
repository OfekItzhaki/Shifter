# 463 — ReAuthDialog WebAuthn Declined State & Cancel Handling

## Phase

Bugfix — Fingerprint Login Workflow (Task 3.3)

## Purpose

When the auto-triggered WebAuthn prompt is cancelled by the user (NotAllowedError or USER_CANCELLED), the dialog must:
1. Set `webAuthnDeclined` to `true` so the auto-trigger effect doesn't re-fire
2. Focus the password input as a fallback so the user can authenticate via password

Without this fix, cancelling the WebAuthn prompt would leave the user in a broken state where the prompt could re-trigger or the password input wouldn't receive focus.

## What was built

| File | Change |
|------|--------|
| `apps/web/components/admin/ReAuthDialog.tsx` | Added `setWebAuthnDeclined(true)` and password input focus in the WebAuthn cancel/error path |

## Key decisions

- Reused the existing `webAuthnDeclined` state (added in task 3.2) rather than introducing a new flag
- Used `setTimeout(() => passwordInputRef.current?.focus(), 50)` for focus — matches the existing focus pattern used elsewhere in the component
- The `webAuthnDeclined` state is already reset to `false` when the dialog closes (task 3.2), so re-opening the dialog will attempt auto-trigger again

## How it connects

- **Task 3.2** added the `webAuthnDeclined` state, the reset on close, and the auto-trigger `useEffect` that checks `webAuthnDeclined`
- **This task (3.3)** wires `setWebAuthnDeclined(true)` into the cancel path so the guard actually activates
- **Task 3.4** will update focus management to not focus password input when WebAuthn auto-trigger is pending

## How to run / verify

1. Open the ReAuthDialog with a user that has WebAuthn credentials registered
2. The WebAuthn prompt should auto-trigger
3. Cancel the WebAuthn prompt (click cancel or press Escape on the browser prompt)
4. Verify: password input receives focus, no re-trigger of WebAuthn prompt
5. Verify: manual WebAuthn button still works if clicked

## What comes next

- Task 3.4: Update focus management to not focus password input when WebAuthn auto-trigger is pending
- Task 3.5: Verify bug condition exploration test passes
- Task 3.6: Verify preservation tests still pass

## Git commit

```bash
git add -A && git commit -m "fix(webauthn): set webAuthnDeclined on cancel and focus password input"
```
