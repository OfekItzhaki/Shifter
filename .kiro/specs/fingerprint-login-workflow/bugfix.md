# Bugfix Requirements Document

## Introduction

Two bugs exist in the fingerprint/WebAuthn login workflow that prevent the biometric experience from working as designed:

1. **Post-login biometric registration prompt never appears** — After a successful email/password login, the app should offer to register a fingerprint for future logins if the user has no WebAuthn credentials. The `listCredentials()` call fails silently because the apiClient interceptor may not have the fresh access token available yet (race condition between token storage and the subsequent API call).

2. **ReAuthDialog doesn't auto-trigger fingerprint authentication** — When re-authenticating for admin/super-admin mode, the dialog renders both password and fingerprint options side by side. The expected behavior is to automatically trigger the WebAuthn prompt when the user has registered credentials, falling back to the password form only if the user cancels.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a user logs in with email/password AND has no registered WebAuthn credentials AND WebAuthn is supported AND the biometric prompt has not been previously dismissed THEN the system fails to show the biometric registration prompt because `listCredentials()` receives a 401 response (the fresh access token is not yet available to the apiClient interceptor at the time of the call) and the error is silently caught

1.2 WHEN the ReAuthDialog opens AND the user has registered WebAuthn credentials (`hasWebAuthn` is true) THEN the system renders both the password form and the fingerprint button side by side without automatically triggering the WebAuthn authentication flow, and focuses the password input instead

### Expected Behavior (Correct)

2.1 WHEN a user logs in with email/password AND has no registered WebAuthn credentials AND WebAuthn is supported AND the biometric prompt has not been previously dismissed THEN the system SHALL successfully call `listCredentials()` with the fresh access token, determine the user has zero credentials, and display the biometric registration prompt modal

2.2 WHEN the ReAuthDialog opens AND the user has registered WebAuthn credentials (`hasWebAuthn` is true) AND credential loading has completed THEN the system SHALL automatically trigger the WebAuthn authentication flow (call `navigator.credentials.get()`) without requiring the user to manually click the fingerprint button, and SHALL fall back to showing the password form only if the user cancels or the WebAuthn prompt fails

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a user logs in with email/password AND already has registered WebAuthn credentials THEN the system SHALL CONTINUE TO skip the biometric registration prompt and redirect to the target page immediately

3.2 WHEN a user logs in with email/password AND WebAuthn is not supported by the browser THEN the system SHALL CONTINUE TO skip the biometric registration prompt and redirect normally

3.3 WHEN a user logs in with email/password AND has previously dismissed the biometric prompt (localStorage flag set) THEN the system SHALL CONTINUE TO skip the biometric registration prompt and redirect normally

3.4 WHEN the ReAuthDialog opens AND the user does NOT have registered WebAuthn credentials THEN the system SHALL CONTINUE TO show only the password form and focus the password input

3.5 WHEN the ReAuthDialog opens AND WebAuthn is not supported by the browser THEN the system SHALL CONTINUE TO show only the password form without attempting any WebAuthn flow

3.6 WHEN the user cancels the auto-triggered WebAuthn prompt in the ReAuthDialog THEN the system SHALL CONTINUE TO allow the user to authenticate via the password form as a fallback

3.7 WHEN conditional mediation (passkey autofill) is used on the login page THEN the system SHALL CONTINUE TO authenticate and redirect without showing the biometric registration prompt
