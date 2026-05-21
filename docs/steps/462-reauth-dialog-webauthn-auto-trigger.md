# 462 — ReAuthDialog WebAuthn Auto-Trigger

## Phase

Bugfix — Fingerprint Login Workflow (Bug 2: Missing Auto-Trigger)

## Purpose

The ReAuthDialog component renders both password and fingerprint options side by side when the user has registered WebAuthn credentials, but never automatically triggers the WebAuthn authentication flow. Users must manually click the fingerprint button. This step adds a `useEffect` that auto-triggers `handleWebAuthnSubmit()` when credential loading completes and `hasWebAuthn` is true, with guards to prevent re-triggering after cancellation or when the dialog is closed.

## What was built

| File | Change |
|------|--------|
| `apps/web/components/admin/ReAuthDialog.tsx` | Added `webAuthnDeclined` state variable to guard against re-triggering after user cancels |
| `apps/web/components/admin/ReAuthDialog.tsx` | Added auto-trigger `useEffect` that calls `handleWebAuthnSubmit()` when `open`, `!credentials.loading`, `credentials.hasWebAuthn`, and `!webAuthnDeclined` |
| `apps/web/components/admin/ReAuthDialog.tsx` | Reset `webAuthnDeclined` to false when dialog opens (in existing reset useEffect) |

## Key decisions

- Added `webAuthnDeclined` state now (ahead of task 3.3) so the auto-trigger guard works immediately without a placeholder
- The `useEffect` dependency array includes `[open, credentials.loading, credentials.hasWebAuthn, webAuthnDeclined]` — this ensures it fires exactly once when conditions are met and doesn't re-fire after cancellation
- Guards are ordered: `!open` → `credentials.loading` → `!credentials.hasWebAuthn` → `webAuthnDeclined` for early exit clarity
- `webAuthnDeclined` is reset when the dialog opens so subsequent openings will auto-trigger again

## How it connects

- **Task 3.3** will add the cancel handling that sets `webAuthnDeclined = true` in `handleWebAuthnSubmit`'s error path
- **Task 3.4** will adjust focus management to not focus password input when WebAuthn auto-trigger is pending
- **Task 3.5** will verify the exploration test passes with this fix in place
- The existing `handleWebAuthnSubmit` callback is reused — no new WebAuthn logic needed

## How to run / verify

1. Open the app and navigate to a page that triggers the ReAuthDialog (admin/platform mode elevation)
2. Ensure the user has registered WebAuthn credentials
3. When the dialog opens, the WebAuthn prompt should auto-trigger without clicking the fingerprint button
4. If cancelled, the password form should remain usable (full cancel handling in task 3.3)

## What comes next

- Task 3.3: Wire `setWebAuthnDeclined(true)` into the cancel/error path of `handleWebAuthnSubmit`
- Task 3.4: Adjust focus management to defer password focus when auto-trigger is pending

## Git commit

```bash
git add -A && git commit -m "fix(webauthn): add auto-trigger useEffect in ReAuthDialog"
```
