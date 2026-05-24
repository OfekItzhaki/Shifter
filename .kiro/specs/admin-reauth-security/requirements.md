# Requirements Document

## Introduction

This feature hardens the admin re-authentication flow by preventing browser password managers from autofilling the password field and by prioritizing WebAuthn/biometric authentication (fingerprint) as the primary re-auth method. When biometrics are unavailable, the user must manually type their password. This applies exclusively to the admin re-authentication dialog, not the initial login flow.

## Glossary

- **ReAuth_Dialog**: The frontend modal component (`ReAuthDialog.tsx`) that prompts the user to verify their identity before entering an elevated privilege mode (admin/management mode).
- **Password_Manager**: The browser's built-in credential storage and autofill mechanism that can automatically populate password fields.
- **WebAuthn_Authenticator**: A platform authenticator (fingerprint sensor, Face ID, Windows Hello) registered via the Web Authentication API that can verify user identity biometrically.
- **Re_Auth_Endpoint**: The backend API endpoint (`POST /auth/re-authenticate`) that validates the user's identity using either a password or a WebAuthn assertion.
- **Credential_Check**: The process of determining whether the current user has registered WebAuthn credentials on the platform.

## Requirements

### Requirement 1: Prevent Password Manager Autofill on Re-Auth

**User Story:** As a space admin, I want the re-authentication password field to reject browser autofill, so that an unauthorized person with access to my unlocked device cannot bypass re-auth using saved passwords.

#### Acceptance Criteria

1. WHEN the ReAuth_Dialog renders the password input field, THE ReAuth_Dialog SHALL set the `autocomplete` attribute to `"new-password"` to signal browsers not to offer saved credentials for autofill.
2. WHEN the ReAuth_Dialog renders the password input field, THE ReAuth_Dialog SHALL set the input `name` attribute to `"reauth-verify"` to prevent heuristic-based autofill by Password_Manager implementations.
3. WHEN the ReAuth_Dialog opens, THE ReAuth_Dialog SHALL render the password input field with an empty value regardless of any previously stored form data.
4. WHEN the ReAuth_Dialog renders the password input field, THE ReAuth_Dialog SHALL set the input to `readonly` on initial render and remove the `readonly` attribute when the user focuses the field, preventing autofill on page load.
5. WHEN the ReAuth_Dialog renders its form element, THE ReAuth_Dialog SHALL set the form `autocomplete` attribute to `"off"` to prevent the browser from prompting the user to save the entered password after submission.

### Requirement 2: WebAuthn Biometric as Primary Re-Auth Method

**User Story:** As a space admin with a registered fingerprint, I want biometric verification to be the default re-auth method, so that I can quickly and securely confirm my identity without typing.

#### Acceptance Criteria

1. WHEN the ReAuth_Dialog opens, THE ReAuth_Dialog SHALL perform a Credential_Check to determine whether the user has registered WebAuthn_Authenticator credentials.
2. WHEN the Credential_Check confirms registered credentials exist, THE ReAuth_Dialog SHALL display the WebAuthn biometric option above the password fallback link, rendered as the default action button with primary visual styling (larger size or filled background compared to secondary options).
3. WHEN the user selects the WebAuthn option, THE ReAuth_Dialog SHALL initiate a WebAuthn assertion ceremony by requesting authentication options from the backend and invoking `navigator.credentials.get()` with a timeout of 60 seconds.
4. WHEN the WebAuthn assertion succeeds, THE ReAuth_Dialog SHALL submit the assertion to the Re_Auth_Endpoint and call `onSuccess` upon receiving a response indicating successful identity verification.
5. IF the WebAuthn assertion fails or is cancelled by the user, THEN THE ReAuth_Dialog SHALL display an error message indicating the failure reason (user cancelled, timed out, or credential not recognized), keep the WebAuthn retry option available, and display a link to switch to password entry.
6. IF `navigator.credentials.get` is not available in the current browser, THEN THE ReAuth_Dialog SHALL hide the WebAuthn option and display the password entry form as the sole authentication method.

### Requirement 3: Manual Password Entry Fallback

**User Story:** As a space admin without a registered fingerprint, I want to manually type my password to re-authenticate, so that I can still access admin mode when biometrics are unavailable.

#### Acceptance Criteria

1. WHEN the Credential_Check confirms no WebAuthn_Authenticator credentials are registered, THE ReAuth_Dialog SHALL display the password entry form as the sole authentication method.
2. WHEN the Credential_Check confirms registered credentials exist, THE ReAuth_Dialog SHALL display a secondary link or button allowing the user to switch to manual password entry.
3. WHEN the user submits a password via the fallback form, THE ReAuth_Dialog SHALL disable the submit button and display a loading indicator, send the password to the Re_Auth_Endpoint, and call `onSuccess` upon a successful server response.
4. IF the user attempts to submit the password form with an empty or whitespace-only password field, THEN THE ReAuth_Dialog SHALL prevent submission, display an inline validation error indicating a password is required, and keep focus on the password input.
5. IF the password verification fails, THEN THE ReAuth_Dialog SHALL display an error message indicating invalid credentials, clear the password field, re-enable the submit button, and refocus the input for retry.
6. IF the password submission fails due to a network error or the Re_Auth_Endpoint is unreachable, THEN THE ReAuth_Dialog SHALL display an error message indicating a connectivity problem, retain the entered password in the field, re-enable the submit button, and allow the user to retry.
7. IF the Re_Auth_Endpoint responds with a rate-limit rejection, THEN THE ReAuth_Dialog SHALL display an error message indicating too many attempts, disable the submit button, and re-enable it after the server-specified cooldown period elapses.

### Requirement 4: Credential Availability Detection

**User Story:** As a developer, I want the system to reliably detect whether the user has WebAuthn credentials, so that the correct re-auth method is presented.

#### Acceptance Criteria

1. WHEN the ReAuth_Dialog opens, THE ReAuth_Dialog SHALL call the WebAuthn credentials list endpoint (`GET /auth/webauthn/credentials`) to determine registered credential availability.
2. WHILE the Credential_Check is in progress, THE ReAuth_Dialog SHALL display a loading indicator and disable all authentication actions.
3. IF the Credential_Check request does not receive a response within 5 seconds, THEN THE ReAuth_Dialog SHALL abort the request, fall back to displaying the password entry form, and log a timeout warning to the console.
4. IF the Credential_Check request fails due to a network error or returns a non-success HTTP status code, THEN THE ReAuth_Dialog SHALL fall back to displaying the password entry form and log the error to the console.
5. WHEN the Credential_Check response returns an empty credentials list, THE ReAuth_Dialog SHALL treat the result as "no registered credentials" and display the password entry form as the sole authentication method.

### Requirement 5: Scope Limitation to Admin Re-Auth Only

**User Story:** As a user, I want the initial login flow to remain unchanged, so that I can still use saved passwords for my regular login.

#### Acceptance Criteria

1. THE initial login form (`/auth/login`) SHALL retain `autocomplete="current-password"` on its password input field.
2. THE ReAuth_Dialog SHALL be the only authentication component that applies autofill-prevention attributes (`autocomplete="off"`, `autocomplete="new-password"`, and non-standard input naming).
3. WHILE the ReAuth_Dialog is not open, THE ReAuth_Dialog SHALL not render any input elements or form elements in the DOM.
4. WHEN a user submits valid credentials on the initial login form (`/auth/login`), THE initial login form SHALL not suppress the browser's native credential-save prompt.

### Requirement 6: Security and Audit

**User Story:** As a platform operator, I want re-authentication attempts to be tracked, so that I can detect and investigate unauthorized access attempts.

#### Acceptance Criteria

1. WHEN a re-authentication attempt fails via password, THE Re_Auth_Endpoint SHALL increment the failed attempt counter for the user, and include the attempt in the audit log with the authentication method set to "password" and the outcome set to failure.
2. WHEN a re-authentication attempt fails via WebAuthn, THE Re_Auth_Endpoint SHALL log the failure reason (user cancelled, credential not recognized, timeout, or assertion verification failure) in the audit log with the authentication method set to "webauthn" and the outcome set to failure.
3. THE Re_Auth_Endpoint SHALL enforce the existing rate limiting policy (the "auth" rate limiter applied to all authentication endpoints) on all re-authentication attempts regardless of method (password or WebAuthn).
4. WHEN a re-authentication attempt succeeds via any method, THE Re_Auth_Endpoint SHALL record the attempt in the audit log with the authentication method used and the outcome set to success.
5. IF the failed attempt counter for a user reaches 5 consecutive failures within a 15-minute window, THEN THE Re_Auth_Endpoint SHALL reject subsequent re-authentication attempts for that user for 15 minutes and log the lockout event in the audit log.
6. THE Re_Auth_Endpoint SHALL include in each audit log entry: actor_user_id, space_id, action, entity_type, entity_id, IP address, and an after-snapshot containing the authentication method and outcome.
