# Step 286 — Biometric Login Flow Fix

## Phase
Phase 9 — UX & Auth Improvements

## Purpose
Fix two issues with the biometric (WebAuthn/passkey) login flow:
1. Users had no way to register a biometric credential from the login flow — registration was buried in the profile page. Now, after a successful email+password login, if WebAuthn is supported and no credentials exist, a prompt offers to set up biometric login.
2. When biometric authentication fails (no registered device), the error message now clearly tells the user to log in with password first.

Additionally, hardcoded Hebrew text in the biometric button was replaced with proper i18n translation keys.

## What was built

### `apps/web/app/login/page.tsx`
- Added imports for `listCredentials` and `registerCredential` from webauthn lib
- After successful email+password login: checks if WebAuthn is supported and user has no credentials, then shows a biometric registration prompt modal
- Added `showBiometricPrompt` / `biometricRegistering` state
- Added `handleBiometricRegister()` — calls `registerCredential("המכשיר שלי")` and redirects on completion
- Added `handleBiometricPromptDismiss()` — stores `biometric_prompt_dismissed` in localStorage and redirects
- Changed biometric login error from generic `biometricFailed` to `noCredentialFound` — a helpful message telling users to log in with password first
- Replaced hardcoded Hebrew text: "התחבר עם ביומטרי" → `t("biometricLogin")`, "מאמת..." → `t("authenticating")`, "או" → `t("or")`
- Added biometric registration prompt modal overlay with gradient button matching existing design language

### `apps/web/messages/he.json`
- Added keys: `biometricLogin`, `authenticating`, `or`, `biometricFailed`, `enableBiometric`, `enableBiometricDesc`, `enableBiometricYes`, `enableBiometricSkip`, `noCredentialFound`

### `apps/web/messages/en.json`
- Added same keys with English translations

### `apps/web/messages/ru.json`
- Added same keys with Russian translations

## Key decisions
- Biometric prompt uses `localStorage("biometric_prompt_dismissed")` to avoid re-prompting users who decline — simple, no server round-trip needed
- Registration uses a fixed nickname "המכשיר שלי" to keep the flow frictionless (one tap to enable)
- If biometric registration fails or is cancelled, the user is silently redirected — no error shown since it's optional
- The biometric login failure message was changed from a generic "failed" to a specific "no device found, log in with password" message — this is the most common failure case and guides the user to the solution

## How it connects
- Uses the existing `listCredentials()` and `registerCredential()` from `lib/webauthn.ts`
- Uses the existing `useTranslations("auth")` pattern from next-intl
- The biometric section in the profile page (`/profile`) remains unchanged for managing/deleting credentials

## How to run / verify
1. Start the app: `npm run dev` from `apps/web`
2. Log in with email+password as a user with NO registered biometric credentials
3. After login succeeds, a modal should appear: "רוצה להפעיל כניסה ביומטרית?"
4. Click "כן, הפעל" — browser authenticator prompt appears, register a credential, then redirect happens
5. Alternatively click "לא עכשיו" — redirects immediately, won't ask again
6. Log out, try "כניסה ביומטרית" button — should work now
7. Test with a user who has NO credentials: click biometric login → should show "לא נמצא מכשיר רשום..." message
8. Switch language to EN/RU and verify all biometric-related text is translated

## What comes next
- Consider adding a "register another device" prompt or device management link in the biometric error state
- Could add analytics tracking for biometric adoption rate

## Git commit
```bash
git add -A && git commit -m "fix(auth): biometric login flow — registration prompt after login, i18n, better error messages"
```
