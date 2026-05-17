# Implementation Plan: Admin Session Timeout

## Overview

This plan implements session timeout and re-authentication controls for elevated privilege modes (Management Mode and Super Platform Mode). The implementation follows the existing Clean Architecture patterns: backend changes in the Application/Domain/Infrastructure layers (C#), and frontend changes using Zustand stores and React components (TypeScript). Tasks are ordered to build foundational pieces first (schema, domain, commands), then API endpoints, then frontend modules, and finally integration wiring.

## Tasks

- [x] 1. Database schema and domain model changes
  - [x] 1.1 Create EF Core migration adding `management_timeout_minutes` column to groups table
    - Add `management_timeout_minutes` integer column, NOT NULL, DEFAULT 15
    - Add CHECK constraint ensuring value is between 5 and 120 inclusive
    - Migration sets existing groups to default value of 15
    - _Requirements: 12.1, 12.2, 12.3_

  - [x] 1.2 Create `platform_settings` table via EF Core migration
    - Create table with columns: `id` (uuid PK), `key` (varchar(100) UNIQUE NOT NULL), `value` (text NOT NULL), `created_at` (timestamptz), `updated_at` (timestamptz)
    - Seed row: key = `"platform_timeout_minutes"`, value = `"15"`
    - _Requirements: 4.6_

  - [x] 1.3 Extend `Group` domain entity with `ManagementTimeoutMinutes` property
    - Add `ManagementTimeoutMinutes` property with default 15
    - Add `SetManagementTimeout(int minutes)` method with range validation [5, 120]
    - Update EF Core configuration in Infrastructure layer for the new column
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 1.4 Create `PlatformSettings` domain entity
    - Create `PlatformSettings` entity with `Key` and `Value` properties
    - Add `Create(string key, string value)` factory and `UpdateValue(string value)` method
    - Register entity in `AppDbContext` and add EF Core configuration
    - _Requirements: 4.1, 4.6_

- [x] 2. Backend re-authentication command and handler
  - [x] 2.1 Create `ReAuthenticateCommand` and handler
    - Create `Application/Auth/Commands/ReAuthenticateCommand.cs` with record: `UserId`, `Password?`, `WebAuthnChallengeId?`, `WebAuthnAssertionJson?`, `SpaceId?`, `IpAddress?`
    - Create `ReAuthenticateResult` record with `Success` boolean
    - Implement handler logic: reject password > 128 chars, load user, verify via BCrypt or `IWebAuthnService.CompleteAuthenticationAsync`
    - Create audit log entry on both success and failure (actor_user_id, space_id, timestamp, method, success/failure)
    - Return generic error on failure (no cause distinction)
    - _Requirements: 1.2, 1.3, 1.4, 1.5, 1.7, 1.10, 2.2, 2.3, 2.4, 2.5, 2.7, 9.1, 9.2, 9.5_

  - [x] 2.2 Create `ReAuthenticateCommandValidator`
    - Validate `UserId` is not empty
    - Validate that either password or WebAuthn assertion (challengeId + assertionJson) is provided
    - _Requirements: 8.3_

  - [x]* 2.3 Write property test for password verification round-trip
    - **Property 1: Password Verification Round-Trip**
    - **Validates: Requirements 1.2, 2.2**

  - [x]* 2.4 Write property test for non-leaking error response
    - **Property 2: Non-Leaking Error Response**
    - **Validates: Requirements 1.5, 2.5, 9.2**

  - [x]* 2.5 Write property test for audit log completeness
    - **Property 3: Audit Log Completeness**
    - **Validates: Requirements 1.7, 2.7, 7.5, 9.5**

- [x] 3. Backend session timeout event and group settings extension
  - [x] 3.1 Create `RecordSessionTimeoutCommand` and handler
    - Create `Application/Auth/Commands/RecordSessionTimeoutCommand.cs` with record: `UserId`, `SpaceId?`, `Mode` (management/platform)
    - Handler creates audit log entry with actor_user_id, space_id, timestamp, and mode
    - _Requirements: 7.5_

  - [x] 3.2 Extend `UpdateGroupSettingsCommand` with `ManagementTimeoutMinutes` parameter
    - Add optional `int? ManagementTimeoutMinutes` parameter to existing command record
    - In handler, call `group.SetManagementTimeout(value)` when provided
    - Ensure existing group admin permission check covers this field
    - _Requirements: 3.2, 3.7, 8.1, 8.6_

  - [x] 3.3 Create `UpdatePlatformSettingsCommand` and handler
    - Create command with `PlatformTimeoutMinutes` parameter
    - Handler loads `PlatformSettings` by key `"platform_timeout_minutes"` and updates value
    - Validate range [5, 120]
    - Require platform admin (super-admin) permission
    - _Requirements: 4.1, 4.3, 4.4_

  - [x]* 3.4 Write property test for timeout duration range validation
    - **Property 5: Timeout Duration Range Validation**
    - **Validates: Requirements 3.2, 3.3, 3.4, 3.5, 4.4, 8.4, 8.5**

- [x] 4. API controller endpoints
  - [x] 4.1 Add `POST /auth/re-authenticate` endpoint to `AuthController`
    - `[Authorize]` attribute, `[EnableRateLimiting("auth")]` (already on controller)
    - Accept `ReAuthenticateRequest` body (password?, webAuthnChallengeId?, webAuthnAssertionJson?, spaceId?)
    - Dispatch `ReAuthenticateCommand` with `CurrentUserId` and request IP
    - Return 200 `{ success: true }` on success, 401 `{ error: "Authentication failed." }` on failure
    - _Requirements: 1.6, 2.6, 8.3, 9.6_

  - [x] 4.2 Add `POST /auth/session-timeout-event` endpoint to `AuthController`
    - `[Authorize]` attribute
    - Accept body with `spaceId?` and `mode` (management/platform)
    - Dispatch `RecordSessionTimeoutCommand`
    - Return 204 NoContent
    - _Requirements: 7.5_

  - [x] 4.3 Extend group settings PATCH to include `managementTimeoutMinutes`
    - Update the existing `PATCH /spaces/{spaceId}/groups/{groupId}/settings` request model to accept `managementTimeoutMinutes`
    - Update the GET response to include `managementTimeoutMinutes` in the returned settings
    - _Requirements: 8.1, 8.2_

  - [x] 4.4 Add `PATCH /platform/settings` and `GET /platform/settings` endpoints to `PlatformController`
    - `[Authorize]` with platform admin check
    - PATCH accepts `{ platformTimeoutMinutes: int }`, dispatches `UpdatePlatformSettingsCommand`
    - GET returns current platform settings including `platformTimeoutMinutes`
    - _Requirements: 4.3, 4.5_

  - [x]* 4.5 Write property test for progressive delay enforcement
    - **Property 10: Progressive Delay Enforcement**
    - **Validates: Requirements 9.6**

- [x] 5. Checkpoint - Ensure all backend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Frontend admin session store and inactivity timer
  - [x] 6.1 Create `adminSessionStore` Zustand store
    - Create `apps/web/lib/store/adminSessionStore.ts`
    - State: `isElevated`, `elevatedMode`, `elevatedGroupId`, `timeoutDuration`, `remainingMs`, `isPromptVisible`, `promptCountdownMs`
    - Actions: `enterElevatedMode`, `exitElevatedMode`, `resetTimer`, `showPrompt`, `dismissPrompt`
    - Store is NOT persisted (resets on page load)
    - _Requirements: 5.1, 5.2, 5.5, 7.1_

  - [x] 6.2 Create `InactivityTimer` module
    - Create `apps/web/lib/session/inactivityTimer.ts`
    - Implement `start(timeoutMs)`, `reset()`, `stop()`, `reconcileAfterVisibilityChange()` methods
    - Use `setInterval` (1-second ticks) for countdown
    - On `visibilitychange`, calculate actual elapsed time using `Date.now() - lastActivityTimestamp`
    - Register activity listeners (click, keypress, scroll) that call `reset()`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.6_

  - [x] 6.3 Create `MultiTabSync` module
    - Create `apps/web/lib/session/multiTabSync.ts`
    - Use `BroadcastChannel` with `localStorage` + `storage` event fallback
    - Message types: `activity_reset`, `session_exit`, `prompt_shown`, `prompt_dismissed`
    - `broadcast(message)`, `subscribe(handler)`, `destroy()` methods
    - _Requirements: 11.1, 11.2, 11.3_

  - [x]* 6.4 Write property test for timer initialization and reset
    - **Property 6: Timer Initialization and Reset**
    - **Validates: Requirements 5.1, 5.3**

  - [x]* 6.5 Write property test for tab visibility time reconciliation
    - **Property 7: Tab Visibility Time Reconciliation**
    - **Validates: Requirements 5.6**

  - [x]* 6.6 Write property test for timeout state cleanup
    - **Property 8: Timeout State Cleanup**
    - **Validates: Requirements 7.1**

  - [x]* 6.7 Write property test for multi-tab state synchronization
    - **Property 9: Multi-Tab State Synchronization**
    - **Validates: Requirements 11.1, 11.2, 11.3**

- [x] 7. Frontend re-authentication dialog
  - [x] 7.1 Create `ReAuthDialog` component
    - Create `apps/web/components/admin/ReAuthDialog.tsx`
    - Props: `open`, `onSuccess`, `onCancel`, `mode` (management/platform), `spaceId?`
    - Fetch user credential availability from `/auth/me` response (`hasPassword`, `hasWebAuthn` derived from user data)
    - Render password input with `autocomplete="current-password"`, visible label, and ARIA attributes
    - Render WebAuthn/fingerprint button when user has registered credentials
    - Show loading state during verification, disable form inputs
    - Display generic error on failure, clear password field, keep dialog open
    - Focus trap within modal, submit button receives initial focus
    - Support keyboard submission (Enter key) for password form
    - Show explanatory message indicating identity confirmation is required
    - _Requirements: 1.1, 1.8, 1.9, 2.1, 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7_

  - [x]* 7.2 Write property test for credential method display correctness
    - **Property 4: Credential Method Display Correctness**
    - **Validates: Requirements 1.9, 10.2, 10.7, 1.8**

- [x] 8. Frontend activity prompt modal
  - [x] 8.1 Create `ActivityPromptModal` component
    - Create `apps/web/components/admin/ActivityPromptModal.tsx`
    - Props: `open`, `countdownSeconds`, `onYes`, `onNo`
    - Display "Are you still active?" message with "Yes" and "No" buttons
    - "Yes" button receives initial focus
    - Visible countdown timer (60 seconds)
    - Modal overlay preventing background interaction
    - Focus trap within modal, keyboard navigable (Tab cycles between buttons, Enter activates)
    - When countdown reaches zero, treat as "No" and call `onNo`
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

- [x] 9. Frontend integration wiring
  - [x] 9.1 Wire `ReAuthDialog` into management mode entry flow
    - Intercept the existing management mode toggle in the group admin UI
    - Before activating management mode, show `ReAuthDialog`
    - On success: call `adminSessionStore.enterElevatedMode('management', groupId, timeoutMinutes)` with group's configured timeout
    - On cancel: remain in standard view
    - If user has no credentials configured, disable management mode button with tooltip message
    - _Requirements: 1.1, 1.4, 1.8, 3.7, 5.1_

  - [x] 9.2 Wire `ReAuthDialog` into super platform mode entry flow
    - Intercept the existing platform tool access in the platform UI
    - Before activating platform mode, show `ReAuthDialog`
    - On success: call `adminSessionStore.enterElevatedMode('platform', null, platformTimeoutMinutes)` with system-level timeout
    - On cancel: remain in standard view
    - _Requirements: 2.1, 2.4, 4.2, 5.2_

  - [x] 9.3 Wire `ActivityPromptModal` to `adminSessionStore`
    - When `isPromptVisible` becomes true, render `ActivityPromptModal`
    - "Yes" → call `adminSessionStore.dismissPrompt('yes')` (resets timer)
    - "No" → call `adminSessionStore.dismissPrompt('no')` (exits elevated mode)
    - _Requirements: 6.1, 6.3, 6.4, 6.5_

  - [x] 9.4 Wire timeout exit behavior
    - On `exitElevatedMode` with reason `'timeout'` or `'prompt_no'`:
      - Clear all management mode state from Zustand store
      - If management mode: redirect to group page in standard view
      - If platform mode: redirect to application home page
      - Display toast notification "Session expired due to inactivity" (visible 5+ seconds)
      - Send `POST /auth/session-timeout-event` to backend
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 9.5 Wire activity event listeners and multi-tab sync
    - Register click, keypress, scroll, and API call listeners within elevated mode that reset the inactivity timer
    - Connect `MultiTabSync` to `adminSessionStore` so activity resets, session exits, and prompt events propagate across tabs
    - On manual exit, broadcast `session_exit` to other tabs
    - _Requirements: 5.3, 11.1, 11.2, 11.3_

- [x] 10. Frontend timeout configuration UI
  - [x] 10.1 Add timeout duration setting to group settings form
    - Add `managementTimeoutMinutes` number input to the existing group settings page
    - Validate input is integer between 5 and 120 on the client side
    - Include in the existing PATCH request to group settings endpoint
    - Display validation error if value is out of range
    - _Requirements: 3.2, 3.5, 8.1, 8.2_

  - [x] 10.2 Add platform timeout setting to platform settings page
    - Add `platformTimeoutMinutes` number input to the platform settings UI
    - Fetch current value from `GET /platform/settings`
    - Submit via `PATCH /platform/settings`
    - Validate range [5, 120] client-side
    - _Requirements: 4.3, 4.5_

- [x] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- Backend uses C# with .NET (MediatR, FluentValidation, EF Core, BCrypt)
- Frontend uses TypeScript with Next.js, Zustand, and fast-check for property tests
- The `ReAuthDialog` and `ActivityPromptModal` follow existing modal patterns in the codebase (`components/Modal.tsx`)
- Multi-tab sync uses BroadcastChannel with localStorage fallback for broad browser support
- Inactivity tracking is entirely client-side to avoid server load; JWT expiry is the ultimate session boundary

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4"] },
    { "id": 2, "tasks": ["2.1", "2.2", "3.1"] },
    { "id": 3, "tasks": ["2.3", "2.4", "2.5", "3.2", "3.3"] },
    { "id": 4, "tasks": ["3.4", "4.1", "4.2", "4.3", "4.4"] },
    { "id": 5, "tasks": ["4.5", "6.1"] },
    { "id": 6, "tasks": ["6.2", "6.3"] },
    { "id": 7, "tasks": ["6.4", "6.5", "6.6", "6.7", "7.1"] },
    { "id": 8, "tasks": ["7.2", "8.1"] },
    { "id": 9, "tasks": ["9.1", "9.2", "9.3"] },
    { "id": 10, "tasks": ["9.4", "9.5"] },
    { "id": 11, "tasks": ["10.1", "10.2"] }
  ]
}
```
