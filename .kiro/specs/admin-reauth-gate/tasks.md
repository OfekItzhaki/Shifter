# Implementation Plan: Admin Re-Authentication Gate

## Overview

This plan integrates existing components (ReAuthDialog, ReAuthenticateCommand handler, AuthController endpoint, adminSessionStore) into a complete re-authentication security gate. Most infrastructure already exists — the focus is on filling gaps (missing Russian locale, credential availability check hardening, disabled-button tooltip for no-credentials state), ensuring all acceptance criteria are met, and adding property-based tests to validate correctness properties from the design document.

## Tasks

- [x] 1. Backend hardening and validation
  - [x] 1.1 Verify and harden ReAuthenticateCommand handler
    - Confirm BCrypt work factor ≥ 12 is enforced at registration (existing invariant)
    - Ensure the handler returns generic 401 for all failure paths (inactive user, wrong password, disabled WebAuthn credential, missing user)
    - Verify password > 128 char early rejection path does NOT invoke BCrypt.Verify
    - Confirm audit log entry is created for every attempt (success and failure) with actor_user_id, space_id, method, success fields
    - Review `ReAuthenticateCommandValidator` ensures either password OR (WebAuthnChallengeId + WebAuthnAssertionJson) is provided
    - _Requirements: 3.1, 3.3, 3.5, 8.1, 8.2, 8.4, 8.5_

  - [x] 1.2 Verify AuthController re-authenticate endpoint response shape
    - Confirm `POST /auth/re-authenticate` returns exactly `{ "success": true }` on success (HTTP 200)
    - Confirm it returns exactly `{ "error": "Authentication failed." }` on failure (HTTP 401)
    - Confirm `[Authorize]` and `[EnableRateLimiting("auth")]` attributes are present
    - Confirm no additional fields or varying messages are returned on failure
    - _Requirements: 3.2, 3.3, 4.3, 4.4, 8.2, 8.4, 9.1_

  - [ ]* 1.3 Write property test: Password length boundary rejection (Property 4)
    - **Property 4: Password length boundary rejection**
    - Generate random strings of length 1–256; verify that strings > 128 chars return failure without BCrypt invocation, and strings ≤ 128 chars proceed to BCrypt verification
    - Use xUnit + FsCheck or custom test data generators in C#
    - **Validates: Requirements 3.5**

  - [ ]* 1.4 Write property test: Generic error response (Property 3)
    - **Property 3: Generic error response (no information leakage)**
    - Generate various failure scenarios (wrong password, non-existent user, inactive user, disabled WebAuthn credential) and verify the response is structurally identical: HTTP 401 with body `{ "error": "Authentication failed." }`
    - **Validates: Requirements 3.3, 4.4, 8.2**

  - [ ]* 1.5 Write property test: Credential verification correctness (Property 2)
    - **Property 2: Credential verification correctness**
    - Generate random passwords, hash them with BCrypt, then verify that `BCrypt.Verify(submitted, hash)` returns true only when submitted matches the original
    - **Validates: Requirements 3.1, 4.2**

  - [ ]* 1.6 Write property test: Audit log completeness (Property 9)
    - **Property 9: Audit log completeness**
    - Generate random re-auth attempts (password/WebAuthn, success/failure), execute the handler, and verify exactly one audit log entry is created with correct actor_user_id, space_id, method, and success fields
    - **Validates: Requirements 8.5**

- [x] 2. Checkpoint - Ensure all backend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Frontend localization and credential availability
  - [x] 3.1 Add Russian (ru) locale translations for reAuth namespace
    - Add `reAuth` section to `apps/web/messages/ru.json` with all keys matching the Hebrew and English locale files
    - Keys: title, description, passwordLabel, passwordPlaceholder, webAuthnButton, submitButton, cancelButton, authFailed, rateLimited, networkError, webAuthnCancelled, noCredentials
    - _Requirements: 7.1, 7.2_

  - [x] 3.2 Verify credential availability check in ReAuthDialog
    - Confirm the dialog fetches WebAuthn credential availability via `listCredentials()`
    - Confirm password is always shown (system invariant: all registered users have a password)
    - Confirm WebAuthn button is only rendered when `isWebAuthnSupported()` returns true AND user has registered credentials
    - Confirm the dialog handles the case where WebAuthn is not supported by the browser (button not rendered)
    - _Requirements: 2.3, 2.4, 2.5, 2.6, 4.6, 9.2_

  - [x] 3.3 Implement disabled button with tooltip for no-credentials state
    - In the group detail page, when `hasCredentials === false`, ensure the admin mode toggle button is visually disabled
    - Add a tooltip explaining that credentials must be configured first
    - Ensure the button cannot be clicked when disabled (guard already exists, add visual feedback)
    - _Requirements: 2.7, 1.4_

  - [x] 3.4 Verify ReAuthDialog accessibility compliance
    - Confirm `role="dialog"`, `aria-modal="true"`, `aria-labelledby`, `aria-describedby` attributes are present
    - Confirm focus trap is active (Tab/Shift+Tab cycles within modal)
    - Confirm initial focus lands on password input (or WebAuthn button if password unavailable)
    - Confirm Escape key calls `onCancel`
    - Confirm RTL layout when locale is Hebrew (`direction: "rtl"`)
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [x] 3.5 Verify loading and submission state handling
    - Confirm `isSubmitting` state disables all form inputs and prevents duplicate submissions
    - Confirm loading indicator is displayed during API call
    - Confirm on success: dialog closes, `onSuccess` is called
    - Confirm on failure: error message shown, password cleared, inputs re-enabled, dialog stays open
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [x] 4. Frontend integration verification
  - [x] 4.1 Verify group page re-auth integration
    - Confirm clicking "Enter Admin Mode" opens ReAuthDialog when `hasCredentials` is true
    - Confirm on success: `enterAdminMode(groupId)` is called, then `enterElevatedMode("management", groupId, timeoutMinutes)` is called
    - Confirm on cancel: dialog closes, user remains in standard view
    - Confirm `managementTimeoutMinutes` is read from group settings (default 15)
    - _Requirements: 1.1, 1.3, 1.4, 1.5, 9.4_

  - [x] 4.2 Verify platform page re-auth integration
    - Confirm accessing platform page shows ReAuthDialog before content loads
    - Confirm on success: `enterElevatedMode("platform", undefined, platformTimeoutMinutes)` is called
    - Confirm on cancel: user is redirected away from platform page
    - Confirm platform timeout is fetched from `GET /platform/settings`
    - Confirm if already in elevated platform mode, re-auth is skipped
    - _Requirements: 1.2, 1.3, 1.5, 9.4, 9.5_

  - [x] 4.3 Verify password form keyboard submission
    - Confirm Enter key in password input triggers form submission
    - Confirm Tab navigation works correctly between password input, WebAuthn button, submit button, and cancel button
    - _Requirements: 3.6, 6.5_

  - [x] 4.4 Verify error handling and recovery
    - Confirm API 401 shows localized "Authentication failed" message
    - Confirm API 429 shows localized "Too many attempts" message
    - Confirm network errors show localized "Connection error" message
    - Confirm WebAuthn user cancellation (NotAllowedError) shows appropriate message
    - Confirm after any error: password input is cleared and re-focused, dialog remains open, `isSubmitting` is reset
    - _Requirements: 3.4, 4.5, 4.6, 5.4_

- [ ] 5. Frontend property-based tests
  - [ ]* 5.1 Write property test: Elevation only on successful verification (Property 1)
    - **Property 1: Elevation only on successful verification**
    - Using fast-check, generate sequences of API responses (success/failure/error) and verify that `adminGroupId` transitions from null to a value ONLY when the API returns `{ success: true }`
    - **Validates: Requirements 1.3, 3.2, 4.3**

  - [ ]* 5.2 Write property test: No duplicate submissions during loading (Property 5)
    - **Property 5: No duplicate submissions during loading**
    - Using fast-check, generate rapid sequences of submit actions while `isSubmitting` is true and verify exactly zero additional API requests are dispatched
    - **Validates: Requirements 5.2**

  - [ ]* 5.3 Write property test: Focus trap containment (Property 6)
    - **Property 6: Focus trap containment**
    - Using fast-check, generate sequences of Tab/Shift+Tab key presses and verify the focused element is always a descendant of the dialog container
    - **Validates: Requirements 6.1, 6.5**

  - [ ]* 5.4 Write property test: Locale-correct text rendering (Property 7)
    - **Property 7: Locale-correct text rendering**
    - Using fast-check, generate locale selections from ["he", "en", "ru"] and verify all visible text elements render content from the corresponding translation file with no missing keys
    - **Validates: Requirements 7.1, 7.2**

  - [ ]* 5.5 Write property test: No client-side password persistence (Property 8)
    - **Property 8: No client-side password persistence**
    - Using fast-check, generate random password strings, simulate submission lifecycle (success and failure), and verify the password does not exist in localStorage, sessionStorage, cookies, or any component state after completion
    - **Validates: Requirements 8.3**

- [x] 6. Checkpoint - Ensure all frontend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. End-to-end integration wiring verification
  - [x] 7.1 Verify full management mode re-auth flow end-to-end
    - Confirm the complete flow: user clicks admin toggle → ReAuthDialog opens → user submits password → API verifies → dialog closes → admin mode activates → elevated session timer starts
    - Confirm exiting admin mode does NOT require re-auth
    - Confirm all admin role levels (management, admin, super-admin) are gated
    - _Requirements: 1.1, 1.3, 1.4, 1.5, 3.2, 9.4_

  - [x] 7.2 Verify full platform mode re-auth flow end-to-end
    - Confirm the complete flow: user navigates to platform → ReAuthDialog opens → user submits credentials → API verifies → dialog closes → platform tools appear → elevated session timer starts
    - Confirm cancellation redirects user away from platform page
    - _Requirements: 1.2, 1.3, 4.3, 9.4_

  - [ ]* 7.3 Write integration test: Rate limiting enforcement
    - Send 5+ failed re-authentication attempts and verify 429 response is returned
    - Verify the existing "auth" rate limiting policy applies to the re-authenticate endpoint
    - _Requirements: 8.4, 8.6_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Most infrastructure already exists — tasks focus on verification, gap-filling, and testing
- Backend uses C# with .NET (MediatR, FluentValidation, EF Core, BCrypt, xUnit)
- Frontend uses TypeScript with Next.js, Zustand, and fast-check for property tests
- The ReAuthDialog component already exists at `apps/web/components/admin/ReAuthDialog.tsx`
- The ReAuthenticateCommand handler already exists at `apps/api/Jobuler.Application/Auth/Commands/ReAuthenticateCommand.cs`
- The AuthController endpoint already exists at `apps/api/Jobuler.Api/Controllers/AuthController.cs`
- The adminSessionStore already exists at `apps/web/lib/store/adminSessionStore.ts`
- The group page integration is already wired at `apps/web/app/groups/[groupId]/page.tsx`
- The platform page integration is already wired at `apps/web/app/platform/page.tsx`
- Russian locale (`ru.json`) is missing the `reAuth` translation section — this is the primary gap

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "3.1"] },
    { "id": 1, "tasks": ["1.3", "1.4", "1.5", "1.6", "3.2", "3.3"] },
    { "id": 2, "tasks": ["3.4", "3.5"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3", "4.4"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3", "5.4", "5.5"] },
    { "id": 5, "tasks": ["7.1", "7.2"] },
    { "id": 6, "tasks": ["7.3"] }
  ]
}
```
