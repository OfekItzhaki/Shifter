# Requirements Document

## Introduction

This feature introduces a re-authentication security gate that requires users to confirm their identity before elevating to any admin mode (management, admin, or super-admin). When a user triggers the `enterAdminMode(groupId)` action or accesses platform-level tools, a modal popup appears requesting biometric (WebAuthn) or password credentials. Admin mode is only granted after successful verification. This prevents unauthorized use of unattended sessions and ensures that privilege escalation is intentional and authenticated.

The re-authentication gate integrates with the existing auth system (JWT tokens, WebAuthn credentials, BCrypt passwords) and supports the app's three locales (Hebrew, English, Russian).

## Glossary

- **Reauth_Gate**: The frontend modal component that intercepts admin mode entry and requires the user to re-authenticate before elevation proceeds.
- **Reauth_Service**: The backend service responsible for verifying re-authentication credentials and returning a success or failure response.
- **Admin_Mode**: The elevated privilege state scoped to a specific group, activated via `enterAdminMode(groupId)` in the authStore.
- **Super_Admin_Mode**: The elevated privilege state for platform-level administration, accessible only to users with `isPlatformAdmin` set to true.
- **Credential_Method**: A means of authentication available to the user — either password-based (BCrypt) or biometric (WebAuthn/fingerprint).
- **Reauth_Modal**: The popup dialog rendered by the Reauth_Gate that presents available credential methods and handles submission.
- **Auth_Store**: The existing Zustand store (`useAuthStore`) that manages authentication state including `adminGroupId` and `isPlatformAdmin`.

## Requirements

### Requirement 1: Re-Authentication Gate Trigger

**User Story:** As a user with admin privileges, I want to be prompted for re-authentication when I try to enter admin mode, so that my elevated session is protected from unauthorized access on an unattended device.

#### Acceptance Criteria

1. WHEN a user triggers the `enterAdminMode(groupId)` action, THE Reauth_Gate SHALL intercept the action and display the Reauth_Modal before allowing elevation.
2. WHEN a user with `isPlatformAdmin` triggers entry to Super_Admin_Mode, THE Reauth_Gate SHALL intercept the action and display the Reauth_Modal before allowing elevation.
3. THE Reauth_Gate SHALL prevent Admin_Mode or Super_Admin_Mode activation until the Reauth_Service returns a successful verification response.
4. WHEN the user dismisses the Reauth_Modal without completing re-authentication, THE Reauth_Gate SHALL cancel the elevation request and the user SHALL remain in standard (non-admin) view.
5. THE Reauth_Gate SHALL apply to all admin role levels: management, admin, and super-admin.

### Requirement 2: Re-Authentication Modal Display

**User Story:** As a user, I want a clear and accessible modal that explains why I need to re-authenticate and offers my available login methods, so that I can quickly confirm my identity.

#### Acceptance Criteria

1. WHEN the Reauth_Modal is displayed, THE Reauth_Gate SHALL render a modal overlay that prevents interaction with the underlying page.
2. THE Reauth_Modal SHALL display an explanatory message indicating that identity confirmation is required to enter admin mode.
3. THE Reauth_Modal SHALL render a password input field with `autocomplete="current-password"`, a visible label, and appropriate ARIA attributes when the user has a password configured.
4. THE Reauth_Modal SHALL render a biometric/fingerprint authentication button when the user has registered WebAuthn credentials.
5. WHEN the user has both password and WebAuthn credentials configured, THE Reauth_Modal SHALL display both options and allow the user to choose either method.
6. WHEN the user has only one Credential_Method configured, THE Reauth_Modal SHALL display only that method.
7. IF the user has neither a password nor WebAuthn credentials configured, THEN THE Reauth_Gate SHALL not offer admin mode entry and SHALL display a message indicating that credentials must be configured first.
8. THE Reauth_Modal SHALL include a cancel/close button that dismisses the modal without attempting authentication.

### Requirement 3: Password Re-Authentication Flow

**User Story:** As a user with a password configured, I want to re-authenticate using my password, so that I can enter admin mode using my familiar login method.

#### Acceptance Criteria

1. WHEN the user submits a password via the Reauth_Modal, THE Reauth_Service SHALL verify the password against the stored BCrypt hash for the authenticated user.
2. WHEN password verification succeeds, THE Reauth_Service SHALL return a success response and THE Reauth_Gate SHALL activate the requested admin mode.
3. IF password verification fails, THEN THE Reauth_Service SHALL return a generic authentication error that does not reveal the specific failure cause.
4. IF password verification fails, THEN THE Reauth_Modal SHALL display a generic error message, clear the password input field, and remain open for retry.
5. THE Reauth_Service SHALL reject password submissions exceeding 128 characters without performing hash verification.
6. THE Reauth_Modal SHALL support keyboard submission (Enter key) for the password form.

### Requirement 4: Biometric (WebAuthn) Re-Authentication Flow

**User Story:** As a user with biometric credentials registered, I want to re-authenticate using my fingerprint or device biometric, so that I can enter admin mode quickly and securely.

#### Acceptance Criteria

1. WHEN the user initiates biometric re-authentication, THE Reauth_Gate SHALL request WebAuthn assertion options from the Reauth_Service.
2. WHEN the browser returns a valid WebAuthn assertion, THE Reauth_Service SHALL verify the assertion against the user's registered credentials.
3. WHEN WebAuthn verification succeeds, THE Reauth_Service SHALL return a success response and THE Reauth_Gate SHALL activate the requested admin mode.
4. IF WebAuthn verification fails, THEN THE Reauth_Service SHALL return a generic authentication error that does not reveal the specific failure cause.
5. IF WebAuthn verification fails, THEN THE Reauth_Modal SHALL display a generic error message and remain open for retry.
6. IF the browser does not support WebAuthn or the user cancels the biometric prompt, THEN THE Reauth_Modal SHALL display an informative message and allow the user to try again or use password authentication instead.

### Requirement 5: Loading and Submission States

**User Story:** As a user, I want clear feedback during the re-authentication process, so that I know the system is processing my credentials.

#### Acceptance Criteria

1. WHEN the user submits credentials (password or initiates WebAuthn), THE Reauth_Modal SHALL display a loading indicator and disable all form inputs until the Reauth_Service responds.
2. WHILE the Reauth_Modal is in a loading state, THE Reauth_Gate SHALL prevent duplicate submissions.
3. WHEN the Reauth_Service responds with success, THE Reauth_Modal SHALL close and admin mode SHALL activate without additional user action.
4. WHEN the Reauth_Service responds with failure, THE Reauth_Modal SHALL re-enable form inputs and display the error state.

### Requirement 6: Accessibility and Keyboard Navigation

**User Story:** As a user relying on keyboard navigation or assistive technology, I want the re-authentication modal to be fully accessible, so that I can complete the re-authentication flow without a mouse.

#### Acceptance Criteria

1. WHEN the Reauth_Modal opens, THE Reauth_Gate SHALL trap keyboard focus within the modal until the modal is closed.
2. THE Reauth_Modal SHALL set initial focus on the primary action element (password input field when visible, or the biometric button when password is not available).
3. THE Reauth_Modal SHALL support closing via the Escape key, treating it as a cancel action.
4. THE Reauth_Modal SHALL use appropriate ARIA roles (`role="dialog"`, `aria-modal="true"`, `aria-labelledby`) for screen reader compatibility.
5. THE Reauth_Modal SHALL ensure all interactive elements are reachable via Tab key navigation in a logical order.

### Requirement 7: Localization

**User Story:** As a user of the application, I want the re-authentication modal to appear in my preferred language, so that I can understand the prompts and instructions.

#### Acceptance Criteria

1. THE Reauth_Modal SHALL render all user-facing text (labels, messages, buttons, errors) in the user's preferred locale as stored in the Auth_Store `preferredLocale` field.
2. THE Reauth_Modal SHALL support Hebrew (he), English (en), and Russian (ru) locales.
3. WHEN the locale is Hebrew, THE Reauth_Modal SHALL render in right-to-left (RTL) layout direction.
4. THE Reauth_Modal SHALL use the existing application localization infrastructure for all translatable strings.

### Requirement 8: Security Constraints

**User Story:** As a system administrator, I want the re-authentication gate to follow security best practices, so that the feature cannot be exploited.

#### Acceptance Criteria

1. THE Reauth_Service SHALL verify passwords using BCrypt with a work factor of 12 or higher, consistent with the existing authentication system.
2. THE Reauth_Service SHALL not reveal whether a re-authentication failure is due to an incorrect password, a non-existent credential, or any other specific cause.
3. THE Reauth_Gate SHALL not store the submitted password or any derived secret in client-side storage (localStorage, sessionStorage, cookies, or component state beyond the current submission lifecycle).
4. THE Reauth_Service SHALL enforce rate limiting on re-authentication attempts using the existing "auth" rate limiting policy.
5. THE Reauth_Service SHALL record all re-authentication attempts (successful and failed) in the audit log including actor_user_id, space_id, timestamp, method (password/webauthn), and success/failure status.
6. IF 5 or more failed re-authentication attempts occur from the same user within a 5-minute window, THEN THE Reauth_Service SHALL apply progressive delays consistent with the existing auth rate limiting policy.

### Requirement 9: Integration with Existing Auth System

**User Story:** As a developer, I want the re-authentication gate to use the existing auth infrastructure, so that the feature is consistent and maintainable.

#### Acceptance Criteria

1. THE Reauth_Service SHALL use the existing `POST /auth/re-authenticate` endpoint for credential verification.
2. THE Reauth_Gate SHALL determine available Credential_Methods by checking the authenticated user's profile data (password presence and WebAuthn credential registration).
3. THE Reauth_Gate SHALL use the existing WebAuthn login options flow (`POST /auth/webauthn/login/options`) to obtain assertion challenge data for biometric re-authentication.
4. WHEN re-authentication succeeds, THE Reauth_Gate SHALL call the existing `enterAdminMode(groupId)` action in the Auth_Store to activate admin mode.
5. THE Reauth_Gate SHALL not issue new JWT tokens upon re-authentication; the existing access token remains valid for the session.

