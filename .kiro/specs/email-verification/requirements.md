# Requirements Document

## Introduction

Email verification adds a trust-building mechanism to the Shifter scheduling application. After registration, users receive a verification email containing a unique token link. Clicking the link marks their email as verified. Verification is **non-blocking** — unverified users retain full access to the application, but a subtle banner encourages them to verify. The system stores only hashed tokens with 24-hour expiry and single-use enforcement.

## Glossary

- **System**: The Shifter scheduling application (backend API + frontend)
- **EmailVerificationToken**: A domain entity storing a SHA-256 hashed token with expiry and usage tracking
- **User**: A registered user of the application with an EmailVerified flag
- **VerifyEmailCommand**: The application-layer command that validates a raw token and marks the user as verified
- **ResendVerificationCommand**: The application-layer command that invalidates old tokens, generates a new one, and sends a verification email
- **RegisterCommand**: The existing registration command, modified to generate a verification token and send a verification email
- **AuthController**: The API controller exposing authentication-related HTTP endpoints
- **VerificationBanner**: A non-blocking UI banner shown to unverified users encouraging verification
- **TokenHash**: The SHA-256 hash of a raw verification token (64-character hex string)
- **RawToken**: The plaintext 64-character hex token sent to the user via email (never stored in the database)

## Requirements

### Requirement 1: Token Generation

**User Story:** As a system operator, I want verification tokens to be cryptographically secure and unique, so that tokens cannot be guessed or forged.

#### Acceptance Criteria

1. WHEN a verification token is generated, THE System SHALL produce a 64-character hexadecimal string derived from 32 cryptographically random bytes
2. WHEN a verification token is generated, THE System SHALL store only the SHA-256 hash of the token in the database
3. THE System SHALL never persist the raw token value in any data store

### Requirement 2: Token Lifecycle

**User Story:** As a system operator, I want verification tokens to expire and be single-use, so that compromised tokens have limited exposure.

#### Acceptance Criteria

1. WHEN an EmailVerificationToken is created, THE System SHALL set the expiry to exactly 24 hours from creation time
2. WHEN a token has passed its expiry time, THE System SHALL reject verification attempts using that token
3. WHEN a token has been successfully used, THE System SHALL reject all subsequent verification attempts using that token
4. WHEN a token is consumed, THE System SHALL record the usage timestamp in the UsedAt field

### Requirement 3: Email Verification on Registration

**User Story:** As a new user, I want to receive a verification email after registration, so that I can verify my email address.

#### Acceptance Criteria

1. WHEN a user successfully registers, THE RegisterCommand SHALL create a new EmailVerificationToken for that user
2. WHEN a user successfully registers, THE RegisterCommand SHALL send a verification email containing a link with the raw token
3. WHEN the verification email fails to send during registration, THE System SHALL still complete the registration successfully
4. WHEN a user is created, THE System SHALL set the EmailVerified flag to false by default

### Requirement 4: Verify Email

**User Story:** As a user who received a verification email, I want to click the link and have my email marked as verified, so that my account shows a verified status.

#### Acceptance Criteria

1. WHEN a valid raw token is submitted to the verify-email endpoint, THE VerifyEmailCommand SHALL hash the token, locate the matching EmailVerificationToken, mark it as used, and set the associated User's EmailVerified flag to true
2. WHEN an invalid, expired, or already-used token is submitted, THE VerifyEmailCommand SHALL return a 400 Bad Request error
3. THE verify-email endpoint SHALL be accessible without authentication (AllowAnonymous)
4. WHEN a token is submitted for a user whose email is already verified, THE VerifyEmailCommand SHALL still mark the token as used and return success

### Requirement 5: Resend Verification Email

**User Story:** As an unverified user, I want to request a new verification email, so that I can verify my email if the original link expired or was lost.

#### Acceptance Criteria

1. WHEN an authenticated unverified user requests a resend, THE ResendVerificationCommand SHALL invalidate all existing active tokens for that user
2. WHEN an authenticated unverified user requests a resend, THE ResendVerificationCommand SHALL generate a new token and send a verification email
3. WHEN an already-verified user requests a resend, THE System SHALL return a 400 Bad Request error with message "Email already verified"
4. THE resend-verification endpoint SHALL require authentication (Authorize)
5. WHEN multiple resend requests are made in rapid succession, THE System SHALL enforce rate limiting and return 429 Too Many Requests

### Requirement 6: Non-Blocking Verification

**User Story:** As an unverified user, I want to access all application features without restriction, so that email verification does not block my workflow.

#### Acceptance Criteria

1. WHILE a user's EmailVerified flag is false, THE System SHALL allow full access to all application features
2. WHILE a user's EmailVerified flag is false, THE System SHALL display a dismissible verification banner on protected pages
3. WHEN a user dismisses the verification banner, THE System SHALL hide the banner for the remainder of the session

### Requirement 7: Frontend Verify Email Page

**User Story:** As a user clicking the verification link, I want to see clear feedback about the verification result, so that I know whether my email was verified successfully.

#### Acceptance Criteria

1. WHEN the verify-email page loads with a token parameter, THE System SHALL automatically submit the token to the verify-email API endpoint
2. WHEN verification succeeds, THE System SHALL display a success message to the user
3. WHEN verification fails, THE System SHALL display an error message and offer a resend option
4. WHEN the verify-email page loads without a token parameter, THE System SHALL display an error state

### Requirement 8: Security and Anti-Enumeration

**User Story:** As a system operator, I want the verification system to prevent user enumeration and token leakage, so that attackers cannot exploit the verification flow.

#### Acceptance Criteria

1. WHEN an invalid token is submitted, THE System SHALL return the same error response regardless of whether the token never existed, is expired, or is already used
2. THE System SHALL use 256-bit entropy (32 random bytes) for token generation to prevent brute-force attacks
3. THE System SHALL transmit raw tokens only over HTTPS in production

### Requirement 9: Data Model and Persistence

**User Story:** As a developer, I want the email verification data model to be well-defined and indexed, so that token lookups are performant and the schema is consistent.

#### Acceptance Criteria

1. THE System SHALL store EmailVerificationTokens in a dedicated database table with columns: id, user_id, token_hash, expires_at, used_at, created_at
2. THE System SHALL maintain an index on the token_hash column for fast lookups
3. THE System SHALL add an email_verified boolean column (default false) to the users table
4. WHEN a user_id references a non-existent user, THE System SHALL enforce referential integrity via a foreign key constraint
