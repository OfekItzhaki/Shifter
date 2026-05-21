# 464 — ReAuthDialog Focus Management for Auto-Trigger Flow

## Phase

Bugfix — Fingerprint Login Workflow (Task 3.4)

## Purpose

When WebAuthn auto-trigger is pending (hasWebAuthn=true AND webAuthnDeclined=false), the focus management useEffect was unconditionally focusing the password input. This competed with the browser's WebAuthn prompt for focus. The fix ensures the password input is only focused when WebAuthn is not available or has been declined.

## What was built

- **Modified**: `apps/web/components/admin/ReAuthDialog.tsx`
  - Updated the focus management `useEffect` to check whether WebAuthn auto-trigger is pending
  - Added `webAuthnDeclined` to the dependency array so focus re-triggers after cancel
  - When `credentials.hasWebAuthn && !webAuthnDeclined`, the effect returns early without focusing anything, letting the WebAuthn browser prompt take focus
  - When `hasWebAuthn` is false OR `webAuthnDeclined` is true, the original focus behavior is preserved (password input focused)

## Key decisions

- Used a simple boolean `webAuthnAutoTriggerPending` computed inside the effect for clarity
- The WebAuthn browser prompt inherently receives focus when `navigator.credentials.get()` is called — no explicit focus call needed
- After the user cancels WebAuthn (webAuthnDeclined becomes true), the password input focus is handled by the setTimeout in the cancel handler (task 3.3), but the useEffect also re-runs due to the `webAuthnDeclined` dependency change, providing a safety net

## How it connects

- Depends on task 3.3 (webAuthnDeclined state and cancel handling)
- Depends on task 3.2 (auto-trigger useEffect that calls handleWebAuthnSubmit)
- Completes the focus management story: auto-trigger → browser prompt gets focus → cancel → password input gets focus

## How to run / verify

1. Open ReAuthDialog for a user with registered WebAuthn credentials
2. Verify the WebAuthn browser prompt appears and receives focus (password input is NOT focused)
3. Cancel the WebAuthn prompt
4. Verify the password input receives focus as fallback
5. Open ReAuthDialog for a user WITHOUT WebAuthn credentials
6. Verify the password input is focused immediately (unchanged behavior)

## What comes next

- Task 3.5: Verify bug condition exploration test now passes
- Task 3.6: Verify preservation tests still pass

## Git commit

```bash
git add -A && git commit -m "fix(webauthn): update ReAuthDialog focus management for auto-trigger flow"
```
