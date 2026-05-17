# Requirements Document

## Introduction

This feature adds session timeout and re-authentication controls for elevated privilege modes (management mode and super-admin platform). When an admin enters management mode or a super-admin accesses the platform tool, the system enforces an inactivity timeout that prompts the user to confirm continued activity. If the user does not respond, the system automatically logs them out and exits the elevated mode. Additionally, entering management mode requires re-authentication via password to prevent unauthorized use of an unattended session.

The timeout duration is configurable per group (default 15 minutes), stored in group settings. The super-admin platform uses a system-level default. This feature mitigates the risk of unattended admin sessions being exploited.

## Glossary

- **Session_Timeout_Service**: The backend service responsible for managing timeout configuration, validating re-authentication requests, and tracking management mode session state.
- **Management_Mode**: The elevated privilege state that a group admin enters on the group page to perform administrative actions (member management, scheduling, settings changes).
- **Super_Platform_Mode**: The elevated privilege state that the super-admin enters when accessing the platform administration tool.
- **Timeout_Duration**: The configurable period of inactivity (in minutes) after which the system prompts the admin to confirm continued activity. Stored per group in group settings.
- **Activity_Prompt**: The frontend modal dialog that asks "Are you still active?" when the timeout period elapses, offering "Yes" and "No" options with a countdown timer.
- **Re_Authentication**: The process of verifying the admin's identity before granting access to management mode, using the same credential methods available at login (password or WebAuthn/fingerprint).
- **Inactivity_Timer**: The client-side countdown timer that tracks elapsed time since the last meaningful user interaction within management mode.
- **Frontend_Session_Module**: The Next.js client-side module (Zustand store + components) responsible for tracking inactivity, displaying the Activity_Prompt, and enforcing session exit.
- **Group_Settings**: The existing per-group configuration record where the Timeout_Duration is stored alongside other group-level settings.

## Requirements

### Requirement 1: Re-Authentication on Management Mode Entry

**User Story:** As a group admin, I want to re-enter my credentials (password or fingerprint) before entering management mode, so that an unattended session cannot be used to perform admin actions.

#### Acceptance Criteria

1. WHEN a group admin requests to enter Management_Mode, THE Frontend_Session_Module SHALL display a re-authentication dialog offering the same credential methods available at login (password and/or WebAuthn/fingerprint).
2. WHEN the admin submits a password for re-authentication, THE Session_Timeout_Service SHALL verify the password against the stored BCrypt hash for the authenticated user within 3 seconds.
3. WHEN the admin uses WebAuthn/fingerprint for re-authentication, THE Session_Timeout_Service SHALL verify the WebAuthn assertion against the user's registered credentials.
4. WHEN credential verification succeeds (password or WebAuthn), THE Session_Timeout_Service SHALL return a success response and the frontend SHALL activate Management_Mode.
5. IF credential verification fails, THEN THE Session_Timeout_Service SHALL return an authentication error that does not reveal the specific cause, and Management_Mode SHALL remain inactive.
6. THE Session_Timeout_Service SHALL enforce rate limiting on re-authentication attempts using the existing "auth" rate limiting policy (fixed window of 10 requests per minute per IP in production).
7. THE Session_Timeout_Service SHALL record all re-authentication attempts (both successful and failed) in the audit log including actor_user_id, space_id, timestamp, method (password/webauthn), and success/failure status.
8. IF the authenticated user has neither a password nor WebAuthn credentials configured, THEN THE Frontend_Session_Module SHALL not offer Management_Mode entry and SHALL display a message indicating that credentials must be configured before using management mode.
9. THE Frontend_Session_Module SHALL show the WebAuthn option only when the user has registered WebAuthn credentials, and the password option only when the user has a password set.
10. THE Session_Timeout_Service SHALL reject re-authentication password requests where the submitted password exceeds 128 characters without performing hash verification.

### Requirement 2: Re-Authentication on Super Platform Mode Entry

**User Story:** As a super-admin, I want to re-enter my credentials (password or fingerprint) before accessing the platform tool, so that an unattended session cannot be used to perform platform-level actions.

#### Acceptance Criteria

1. WHEN a super-admin requests to enter Super_Platform_Mode, THE Frontend_Session_Module SHALL display a re-authentication dialog offering the same credential methods available at login (password and/or WebAuthn/fingerprint).
2. WHEN the super-admin submits a password for re-authentication, THE Session_Timeout_Service SHALL verify the password against the stored BCrypt hash for the authenticated user.
3. WHEN the super-admin uses WebAuthn/fingerprint for re-authentication, THE Session_Timeout_Service SHALL verify the WebAuthn assertion against the user's registered credentials.
4. WHEN credential verification succeeds (password or WebAuthn), THE Session_Timeout_Service SHALL return a success response and the frontend SHALL activate Super_Platform_Mode.
5. IF credential verification fails, THEN THE Session_Timeout_Service SHALL return an authentication error, Super_Platform_Mode SHALL remain inactive, and the dialog SHALL remain open for retry.
6. THE Session_Timeout_Service SHALL enforce rate limiting on re-authentication attempts using the existing "auth" rate limiting policy.
7. THE Session_Timeout_Service SHALL record all re-authentication attempts (both successful and failed) in the audit log including actor_user_id, timestamp, method (password/webauthn), and success/failure status.

### Requirement 3: Configurable Timeout Duration per Group

**User Story:** As a group admin, I want to configure the session timeout duration for my group, so that I can balance security and convenience for my team's workflow.

#### Acceptance Criteria

1. THE Group_Settings SHALL include a timeout_duration_minutes field of type integer with a default value of 15.
2. WHEN a group admin updates the timeout duration, THE Session_Timeout_Service SHALL validate that the submitted value is a whole integer between 5 and 120 inclusive, and persist the new value in Group_Settings.
3. THE Session_Timeout_Service SHALL enforce a minimum timeout duration of 5 minutes.
4. THE Session_Timeout_Service SHALL enforce a maximum timeout duration of 120 minutes.
5. IF the requested timeout duration is outside the allowed range or is not a whole integer, THEN THE Session_Timeout_Service SHALL reject the request with a validation error indicating the allowed range.
6. WHEN a group has no explicit timeout duration configured, THE Session_Timeout_Service SHALL use the default value of 15 minutes.
7. WHEN the timeout duration is updated for a group, THE Session_Timeout_Service SHALL apply the new value only to Management_Mode sessions started after the change; active sessions SHALL continue using the timeout duration that was in effect when they started.

### Requirement 4: Super Platform Timeout Duration

**User Story:** As a system owner, I want to configure the super-admin platform timeout duration, so that I can balance security and convenience for platform-level sessions.

#### Acceptance Criteria

1. THE Session_Timeout_Service SHALL apply a configurable timeout for Super_Platform_Mode with a default value of 15 minutes.
2. WHILE in Super_Platform_Mode, THE Inactivity_Timer SHALL use the configured platform timeout duration.
3. THE Session_Timeout_Service SHALL expose the platform timeout configuration through a `PATCH /platform/settings` endpoint accessible only to the super-admin.
4. THE Session_Timeout_Service SHALL enforce the same range constraints (5–120 minutes) for the platform timeout as for group timeouts.
5. WHEN the platform settings are retrieved, THE Session_Timeout_Service SHALL include the current platform_timeout_minutes value in the response.
6. THE platform timeout setting SHALL be stored in a system-level settings table (not per-group).

### Requirement 5: Inactivity Timer Tracking

**User Story:** As a group admin in management mode, I want the system to track my inactivity, so that I am prompted before the session expires.

#### Acceptance Criteria

1. WHEN Management_Mode is activated, THE Frontend_Session_Module SHALL start the Inactivity_Timer with the configured Timeout_Duration for the current group.
2. WHEN Super_Platform_Mode is activated, THE Frontend_Session_Module SHALL start the Inactivity_Timer with the system-level default timeout duration.
3. WHEN the user performs a meaningful interaction (click, keypress, scroll, or API call) within the elevated mode, THE Frontend_Session_Module SHALL reset the Inactivity_Timer to the full Timeout_Duration.
4. WHILE the Inactivity_Timer is running, THE Frontend_Session_Module SHALL track remaining time without requiring server communication.
5. WHEN the user manually exits Management_Mode or Super_Platform_Mode, THE Frontend_Session_Module SHALL stop and clear the Inactivity_Timer.
6. WHEN the browser tab or window loses visibility while the Inactivity_Timer is running, THE Frontend_Session_Module SHALL continue counting elapsed time and reconcile the timer against actual elapsed time when the tab regains visibility.

### Requirement 6: Activity Prompt Display

**User Story:** As a group admin, I want to be asked if I am still active before being logged out, so that I have a chance to continue my session.

#### Acceptance Criteria

1. WHEN the Inactivity_Timer reaches zero, THE Frontend_Session_Module SHALL display the Activity_Prompt modal with the message "Are you still active?" and "Yes" and "No" buttons.
2. WHEN the Activity_Prompt is displayed, THE Frontend_Session_Module SHALL start a secondary countdown timer of 60 seconds visible to the user.
3. WHEN the user selects "Yes" on the Activity_Prompt, THE Frontend_Session_Module SHALL dismiss the prompt and reset the Inactivity_Timer to the full Timeout_Duration.
4. WHEN the user selects "No" on the Activity_Prompt, THE Frontend_Session_Module SHALL immediately exit the elevated mode and redirect the user to the standard view.
5. WHEN the secondary countdown timer reaches zero without user interaction, THE Frontend_Session_Module SHALL treat the response as "No" and exit the elevated mode.
6. WHILE the Activity_Prompt is displayed, THE Frontend_Session_Module SHALL prevent interaction with the underlying management interface.
7. THE Activity_Prompt modal SHALL be keyboard-navigable, with focus trapped within the modal and the "Yes" button receiving initial focus.

### Requirement 7: Session Exit on Timeout

**User Story:** As a system administrator, I want timed-out admin sessions to be fully terminated, so that no elevated actions can be performed after inactivity.

#### Acceptance Criteria

1. WHEN the elevated mode session is terminated due to timeout, THE Frontend_Session_Module SHALL clear all management mode state from the Zustand store.
2. WHEN the Management_Mode session is terminated due to timeout, THE Frontend_Session_Module SHALL redirect the user to the group page in standard (non-admin) view.
3. WHEN Super_Platform_Mode is terminated due to timeout, THE Frontend_Session_Module SHALL redirect the user to the application home page.
4. WHEN the elevated mode session is terminated due to timeout, THE Frontend_Session_Module SHALL display a toast notification informing the user that the session expired due to inactivity, visible for at least 5 seconds.
5. WHEN the elevated mode session is terminated due to timeout, THE Frontend_Session_Module SHALL send a timeout event to the Session_Timeout_Service, which SHALL record it in the audit log including actor_user_id, space_id, and timestamp.

### Requirement 8: Timeout Configuration API

**User Story:** As a developer, I want clearly defined API endpoints for timeout configuration, so that the frontend can manage timeout settings predictably.

#### Acceptance Criteria

1. THE Session_Timeout_Service SHALL expose timeout duration configuration through the existing `PATCH /spaces/{spaceId}/groups/{groupId}/settings` endpoint by adding the timeout_duration_minutes field.
2. WHEN the group settings are retrieved, THE Session_Timeout_Service SHALL include the current timeout_duration_minutes value in the response.
3. THE Session_Timeout_Service SHALL expose `POST /auth/re-authenticate` as an authorized endpoint that accepts either a password or a WebAuthn assertion and returns a success or failure response without distinguishing between incorrect credentials and non-existent account.
4. THE Session_Timeout_Service SHALL validate that the timeout_duration_minutes field is an integer between 5 and 120 inclusive.
5. IF the timeout_duration_minutes value fails validation, THEN THE Session_Timeout_Service SHALL reject the request with a validation error indicating the allowed range of 5 to 120.
6. THE Session_Timeout_Service SHALL require group admin permission to update the timeout_duration_minutes field via the group settings endpoint.

### Requirement 9: Security Constraints

**User Story:** As a system administrator, I want the session timeout feature to follow security best practices, so that elevated sessions cannot be exploited.

#### Acceptance Criteria

1. THE Session_Timeout_Service SHALL verify re-authentication passwords using BCrypt with a work factor of 12 or higher, consistent with the existing password verification.
2. THE Session_Timeout_Service SHALL not reveal whether a re-authentication failure is due to an incorrect password versus a non-existent account.
3. THE Frontend_Session_Module SHALL not store the re-authentication password or any derived token in client-side storage.
4. THE Frontend_Session_Module SHALL enforce timeout tracking entirely client-side to avoid unnecessary server load, with the server relying on existing JWT expiry for ultimate session control.
5. THE Session_Timeout_Service SHALL log all re-authentication attempts (success and failure) for security audit purposes.
6. IF 5 or more failed re-authentication attempts occur from the same user within a 5-minute window, THEN THE Session_Timeout_Service SHALL apply progressive delays consistent with the existing auth rate limiting policy.

### Requirement 10: Frontend Re-Authentication Dialog

**User Story:** As a group admin, I want a clear and accessible credential dialog when entering management mode, so that I understand why re-authentication is required and can use my preferred method.

#### Acceptance Criteria

1. WHEN the re-authentication dialog is displayed, THE Frontend_Session_Module SHALL show an explanatory message indicating that identity confirmation is required for security.
2. THE Frontend_Session_Module SHALL render the available authentication methods: password input (with `autocomplete="current-password"`, visible label, and ARIA attributes) and/or a WebAuthn/fingerprint button, based on the user's configured credentials.
3. WHEN the user submits credentials (password or initiates WebAuthn), THE Frontend_Session_Module SHALL display a loading state and disable the form inputs until the server responds.
4. IF re-authentication fails, THEN THE Frontend_Session_Module SHALL display an error message, clear the password input field (if applicable), and keep the dialog open for retry.
5. WHEN the user dismisses the re-authentication dialog without submitting, THE Frontend_Session_Module SHALL cancel the management mode entry and return to the standard view.
6. THE Frontend_Session_Module SHALL support keyboard submission (Enter key) for the password form.
7. WHEN the user has both password and WebAuthn configured, THE Frontend_Session_Module SHALL display both options and allow the user to choose either method.

### Requirement 11: Multi-Tab Behavior

**User Story:** As a group admin with multiple browser tabs open, I want timeout behavior to be consistent, so that I am not unexpectedly logged out in one tab while active in another.

#### Acceptance Criteria

1. WHEN the user is active in one tab in Management_Mode, THE Frontend_Session_Module SHALL synchronize the Inactivity_Timer reset across all tabs using browser storage events.
2. WHEN the elevated mode session (Management_Mode or Super_Platform_Mode) is terminated in one tab, THE Frontend_Session_Module SHALL exit the elevated mode in all other tabs for the same session scope.
3. WHEN the Activity_Prompt is displayed in one tab, THE Frontend_Session_Module SHALL suppress duplicate prompts in other tabs and defer to the tab that first displayed the prompt.

### Requirement 12: Database Schema Extension

**User Story:** As a developer, I want the group settings schema to include timeout configuration, so that the feature has persistent storage.

#### Acceptance Criteria

1. THE Group_Settings table SHALL include a `management_timeout_minutes` column of type integer, NOT NULL, with a default value of 15.
2. THE Group_Settings table SHALL enforce a CHECK constraint ensuring management_timeout_minutes is between 5 and 120 inclusive.
3. WHEN migrating existing groups, THE migration SHALL set management_timeout_minutes to 15 for all existing groups.
