# Requirements Document

## Introduction

This feature adds biometric login (fingerprint/face recognition) to the application using the Web Authentication API (WebAuthn) / Passkeys standard. Users can register biometric credentials after logging in with their existing email+password, and then use those credentials for passwordless authentication on subsequent visits. This is a mobile-first feature targeting the majority of users who access the application on phones.

The implementation uses public-key cryptography: the server stores only the public key, while the private key remains on the user's device, unlocked by the biometric. The biometric data never leaves the device.

## Glossary

- **WebAuthn_Service**: The backend service responsible for generating WebAuthn challenges, verifying attestation responses during registration, and verifying assertion responses during authentication. Implemented using Fido2NetLib.
- **Credential_Store**: The PostgreSQL table (`webauthn_credentials`) that persists registered WebAuthn credential data (credential ID, public key, sign count, transports) per user.
- **Relying_Party**: The server-side identity (origin + RP ID) that WebAuthn credentials are scoped to. Corresponds to the application's domain.
- **Authenticator**: The user's device or platform authenticator (e.g., Touch ID, Face ID, Windows Hello, Android biometric) that creates and uses passkey credentials.
- **Challenge**: A server-generated cryptographically random value used to prevent replay attacks during both registration and authentication ceremonies.
- **Attestation**: The response from the Authenticator during credential registration, containing the new public key and proof of creation.
- **Assertion**: The response from the Authenticator during login, containing a signature proving possession of the private key.
- **Sign_Count**: A counter incremented by the Authenticator on each use, used to detect cloned credentials.
- **Frontend_Auth_Module**: The Next.js client-side module responsible for calling `navigator.credentials.create()` and `navigator.credentials.get()` and communicating with the backend API.

## Requirements

### Requirement 1: Credential Registration Initiation

**User Story:** As an authenticated user, I want to initiate biometric credential registration, so that I can set up passwordless login for future visits.

#### Acceptance Criteria

1. WHEN an authenticated user requests credential registration options, THE WebAuthn_Service SHALL generate a cryptographically random challenge of at least 16 bytes.
2. WHEN generating registration options, THE WebAuthn_Service SHALL include the user's ID, email, and display name as the WebAuthn user entity.
3. WHEN generating registration options, THE WebAuthn_Service SHALL set the authenticator selection criteria to prefer platform authenticators with user verification required.
4. WHEN generating registration options, THE WebAuthn_Service SHALL include all existing credential IDs for the user in the exclude list to prevent duplicate registrations.
5. WHEN generating registration options, THE WebAuthn_Service SHALL set the Relying_Party ID and name from server configuration.
6. THE WebAuthn_Service SHALL store the generated challenge in a server-side session or cache with a maximum lifetime of 5 minutes.

### Requirement 2: Credential Registration Completion

**User Story:** As an authenticated user, I want to complete biometric credential registration by providing my fingerprint or face, so that my device is linked to my account.

#### Acceptance Criteria

1. WHEN the Frontend_Auth_Module submits an attestation response, THE WebAuthn_Service SHALL verify the response against the stored challenge.
2. WHEN attestation verification succeeds, THE Credential_Store SHALL persist the credential ID, public key, sign count, transport hints, and creation timestamp associated with the user.
3. WHEN attestation verification succeeds, THE WebAuthn_Service SHALL return a success response containing the credential ID and a confirmation message.
4. IF the attestation response fails verification, THEN THE WebAuthn_Service SHALL return a descriptive error and discard the challenge.
5. IF the challenge has expired or does not exist, THEN THE WebAuthn_Service SHALL reject the registration attempt with an expiration error.
6. WHEN a credential is registered, THE Credential_Store SHALL associate the credential with the authenticated user's ID.
7. THE Credential_Store SHALL allow a single user to have multiple registered credentials.

### Requirement 3: Biometric Authentication Initiation

**User Story:** As a returning user, I want to initiate biometric login, so that I can authenticate without typing my email and password.

#### Acceptance Criteria

1. WHEN a user requests biometric authentication options, THE WebAuthn_Service SHALL generate a cryptographically random challenge of at least 16 bytes.
2. WHEN generating authentication options, THE WebAuthn_Service SHALL set user verification to required.
3. WHEN generating authentication options, THE WebAuthn_Service SHALL include the Relying_Party ID from server configuration.
4. THE WebAuthn_Service SHALL store the authentication challenge in a server-side session or cache with a maximum lifetime of 5 minutes.
5. WHEN generating authentication options without a specified user, THE WebAuthn_Service SHALL allow discoverable credentials (resident keys) so the Authenticator can identify the user.

### Requirement 4: Biometric Authentication Completion

**User Story:** As a returning user, I want to complete biometric login by providing my fingerprint or face, so that I receive access and refresh tokens without a password.

#### Acceptance Criteria

1. WHEN the Frontend_Auth_Module submits an assertion response, THE WebAuthn_Service SHALL verify the signature against the stored public key for the credential ID.
2. WHEN assertion verification succeeds, THE WebAuthn_Service SHALL issue a JWT access token and a refresh token identical in format and expiry to the email+password login flow.
3. WHEN assertion verification succeeds, THE Credential_Store SHALL update the sign count for the credential to the value reported by the Authenticator.
4. IF the assertion response fails signature verification, THEN THE WebAuthn_Service SHALL reject the authentication attempt with an invalid credential error.
5. IF the credential ID does not exist in the Credential_Store, THEN THE WebAuthn_Service SHALL reject the authentication attempt.
6. IF the reported sign count is less than or equal to the stored sign count, THEN THE WebAuthn_Service SHALL reject the authentication and flag the credential as potentially cloned.
7. WHEN authentication succeeds, THE WebAuthn_Service SHALL record the login event on the user entity identical to the email+password login flow.

### Requirement 5: Credential Management

**User Story:** As an authenticated user, I want to view and remove my registered biometric credentials, so that I can manage which devices have access to my account.

#### Acceptance Criteria

1. WHEN an authenticated user requests their credential list, THE Credential_Store SHALL return all credentials for that user including credential ID, a user-provided nickname, creation timestamp, and last used timestamp.
2. WHEN an authenticated user requests deletion of a credential, THE Credential_Store SHALL remove the specified credential if it belongs to the requesting user.
3. IF a user attempts to delete a credential that does not belong to the requesting user, THEN THE WebAuthn_Service SHALL reject the request with a forbidden error.
4. WHEN a credential is deleted, THE Credential_Store SHALL permanently remove the credential record from the database.
5. THE Credential_Store SHALL allow a user to delete all their credentials, reverting to password-only authentication.

### Requirement 6: Credential Naming

**User Story:** As an authenticated user, I want to assign a friendly name to my biometric credentials, so that I can distinguish between devices (e.g., "My iPhone", "Work Laptop").

#### Acceptance Criteria

1. WHEN registering a credential, THE WebAuthn_Service SHALL accept an optional nickname parameter with a maximum length of 100 characters.
2. WHEN an authenticated user updates a credential nickname, THE Credential_Store SHALL persist the new nickname for the specified credential.
3. IF a nickname exceeds 100 characters, THEN THE WebAuthn_Service SHALL reject the request with a validation error.

### Requirement 7: Frontend Registration Flow

**User Story:** As an authenticated user on a mobile device, I want a clear UI prompt to enable biometric login, so that I can easily opt in to passwordless authentication.

#### Acceptance Criteria

1. WHEN an authenticated user has no registered credentials, THE Frontend_Auth_Module SHALL display a prompt suggesting biometric login setup.
2. WHEN the user initiates registration, THE Frontend_Auth_Module SHALL call `navigator.credentials.create()` with the options received from the WebAuthn_Service.
3. IF the browser does not support WebAuthn, THEN THE Frontend_Auth_Module SHALL hide all biometric login UI elements.
4. IF the user cancels the Authenticator prompt, THEN THE Frontend_Auth_Module SHALL display a dismissible message and allow retry.
5. WHEN registration completes successfully, THE Frontend_Auth_Module SHALL display a success confirmation with the credential nickname.

### Requirement 8: Frontend Authentication Flow

**User Story:** As a returning user on a mobile device, I want to see a "Login with biometric" option on the login page, so that I can authenticate quickly.

#### Acceptance Criteria

1. WHEN the login page loads and WebAuthn is supported by the browser, THE Frontend_Auth_Module SHALL display a "Login with biometric" button.
2. WHEN the user activates the biometric login button, THE Frontend_Auth_Module SHALL call `navigator.credentials.get()` with the options received from the WebAuthn_Service.
3. WHEN biometric authentication succeeds, THE Frontend_Auth_Module SHALL store the received tokens and redirect the user to the application.
4. IF biometric authentication fails, THEN THE Frontend_Auth_Module SHALL display an error message and keep the email+password form available as fallback.
5. THE Frontend_Auth_Module SHALL render the biometric login button prominently above the email+password form on mobile viewports.

### Requirement 9: Security Constraints

**User Story:** As a system administrator, I want biometric authentication to follow security best practices, so that the system remains protected against credential theft and replay attacks.

#### Acceptance Criteria

1. THE WebAuthn_Service SHALL require user verification (UV flag) for both registration and authentication ceremonies.
2. THE WebAuthn_Service SHALL validate the origin and Relying_Party ID in all attestation and assertion responses.
3. THE WebAuthn_Service SHALL reject any response where the origin does not match the configured application origin.
4. THE Credential_Store SHALL store only the public key, credential ID, sign count, and transport hints — the private key and biometric data never reach the server.
5. THE WebAuthn_Service SHALL use challenges that are single-use — each challenge is invalidated after one verification attempt regardless of outcome.
6. WHEN a credential is flagged as potentially cloned due to sign count regression, THE WebAuthn_Service SHALL disable the credential and require the user to re-register.
7. THE WebAuthn_Service SHALL enforce rate limiting on authentication attempts consistent with the existing auth rate limiting policy.

### Requirement 10: Database Schema

**User Story:** As a developer, I want a well-defined database schema for WebAuthn credentials, so that credential data is stored reliably and efficiently.

#### Acceptance Criteria

1. THE Credential_Store SHALL store each credential with: id (UUID primary key), user_id (foreign key to users), credential_id (byte array, unique), public_key (byte array), sign_count (unsigned integer), transports (text array), nickname (text, nullable), created_at (timestamp), last_used_at (timestamp, nullable).
2. THE Credential_Store SHALL enforce a unique constraint on the credential_id column.
3. THE Credential_Store SHALL enforce a foreign key constraint from user_id to the users table with cascade delete.
4. THE Credential_Store SHALL create an index on user_id for efficient lookup of credentials by user.
5. THE Credential_Store SHALL create an index on credential_id for efficient lookup during authentication.

### Requirement 11: API Endpoint Design

**User Story:** As a developer, I want clearly defined API endpoints for WebAuthn operations, so that the frontend can integrate predictably.

#### Acceptance Criteria

1. THE WebAuthn_Service SHALL expose `POST /auth/webauthn/register/options` as an authorized endpoint that returns registration options.
2. THE WebAuthn_Service SHALL expose `POST /auth/webauthn/register/complete` as an authorized endpoint that completes registration.
3. THE WebAuthn_Service SHALL expose `POST /auth/webauthn/login/options` as an anonymous endpoint that returns authentication options.
4. THE WebAuthn_Service SHALL expose `POST /auth/webauthn/login/complete` as an anonymous endpoint that completes authentication and returns tokens.
5. THE WebAuthn_Service SHALL expose `GET /auth/webauthn/credentials` as an authorized endpoint that lists the user's credentials.
6. THE WebAuthn_Service SHALL expose `DELETE /auth/webauthn/credentials/{id}` as an authorized endpoint that removes a credential.
7. THE WebAuthn_Service SHALL expose `PATCH /auth/webauthn/credentials/{id}` as an authorized endpoint that updates a credential nickname.

### Requirement 12: Account Deletion Cleanup

**User Story:** As a user deleting my account, I want all my biometric credentials removed, so that no orphaned credential data remains.

#### Acceptance Criteria

1. WHEN a user account is deleted, THE Credential_Store SHALL remove all WebAuthn credentials associated with that user via cascade delete.
2. WHEN a user account is deleted, THE Frontend_Auth_Module SHALL not attempt biometric login for that user on subsequent visits.
