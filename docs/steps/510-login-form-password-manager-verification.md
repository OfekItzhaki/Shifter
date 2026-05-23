# 510 — Login Form Password Manager Support Verification

## Phase

Admin Re-Auth Security — Scope Verification

## Purpose

Verify that the initial login form at `/login` retains full password manager support and was not accidentally affected by the autofill-prevention attributes added to the `ReAuthDialog`. This ensures Requirement 5 (Scope Limitation to Admin Re-Auth Only) is satisfied.

## What was verified

### Login form (`apps/web/app/login/page.tsx`)

| Check | Result | Details |
|-------|--------|---------|
| `autocomplete="current-password"` on password input | ✅ Present | Line: `autoComplete="current-password"` |
| No `autocomplete="off"` on form | ✅ Correct | Form uses default behavior — no suppression of credential-save prompt |
| No `autocomplete="new-password"` | ✅ Correct | Not present on any input in the login form |
| No `name="reauth-verify"` | ✅ Correct | Not present on any input in the login form |
| No `readOnly` trick | ✅ Correct | Password input has no readonly attribute |
| Browser credential-save prompt not suppressed | ✅ Correct | No form-level `autocomplete="off"` or other suppression mechanisms |

### ReAuthDialog (`apps/web/components/admin/ReAuthDialog.tsx`) — Isolation confirmed

| Attribute | Present in ReAuthDialog | Present in Login Form |
|-----------|------------------------|----------------------|
| `autoComplete="off"` (form) | ✅ Yes | ❌ No |
| `autoComplete="new-password"` (input) | ✅ Yes | ❌ No |
| `name="reauth-verify"` (input) | ✅ Yes | ❌ No |
| `readOnly` trick (input) | ✅ Yes | ❌ No |

## Key decisions

- No code changes required — the login form was already correctly configured.
- The autofill-prevention attributes are properly isolated to the `ReAuthDialog` component only.

## How it connects

- Validates Requirements 5.1, 5.2, and 5.4 from the admin-reauth-security spec.
- Confirms that the changes made in steps 509 (autofill prevention on ReAuthDialog) did not leak into the login form.

## How to run / verify

1. Open the login page at `/login` in a browser with saved credentials.
2. Confirm the browser offers to autofill the password field.
3. Submit valid credentials and confirm the browser offers to save/update the password.
4. Open the ReAuthDialog (enter admin mode) and confirm the browser does NOT offer autofill.

## What comes next

- Task 6.2: Write unit tests for scope isolation (login form retains `autocomplete="current-password"`, ReAuthDialog is the only component with autofill-prevention attributes).

## Git commit

```bash
git add -A && git commit -m "docs(admin-reauth): verify login form retains password manager support"
```
