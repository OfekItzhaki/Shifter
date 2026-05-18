# Implementation Plan: Home Leave Protection

## Overview

This plan implements solver-level exclusion of people on home leave, publish-time protection of existing AtHome windows, enhanced emergency recall with notifications and audit logging, and live status priority hierarchy. The implementation builds on existing infrastructure (`SolverPayloadNormalizer`, `PublishVersionCommand`, `CancelHomeLeaveCommand`, notification services, and audit logger) using C# with the established Clean Architecture layering.

## Tasks

- [x] 1. Solver exclusion of people on home leave
  - [x] 1.1 Implement `GetExcludedPersonIdsAsync` in SolverPayloadNormalizer
    - Add a private method that queries `PresenceWindows` for AtHome windows overlapping the solver horizon
    - Return a `HashSet<Guid>` of person IDs to exclude
    - When `EmergencyFreezeActive && EmergencyUseForScheduling` is true, return an empty set (bypass)
    - Filter `peopleDto` to remove excluded person IDs
    - Filter `slotsDto` to remove any slots referencing excluded person IDs
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [ ]* 1.2 Write property test for solver exclusion completeness
    - **Property 1: Solver exclusion completeness**
    - Use FsCheck to generate random people lists and AtHome windows with varying overlap positions relative to the horizon
    - Assert that no person with an overlapping AtHome window appears in the output payload
    - Assert that no slot references an excluded person ID
    - **Validates: Requirements 1.1, 1.2, 1.4**

  - [ ]* 1.3 Write property test for emergency bypass
    - **Property 2: Emergency bypass includes all people**
    - Use FsCheck to generate random people with AtHome windows, set emergency flags to true
    - Assert that the exclusion set is empty and all people remain in the payload
    - **Validates: Requirements 1.3**

- [x] 2. Publish-time protection of existing home leave windows
  - [x] 2.1 Harden `CreateHomeLeavePresenceWindowsAsync` in PublishVersionCommand
    - Ensure the stale-window removal query only targets derived AtHome windows (`IsDerived == true`)
    - Scope removal to only people who appear in the new version's `home_leave_assignments` list
    - Never modify `StartsAt` or `EndsAt` of any existing AtHome window not produced by the current publish
    - Preserve all manually-created AtHome windows regardless of schedule content
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 7.3_

  - [ ]* 2.2 Write property test for publish preserves non-target AtHome windows
    - **Property 3: Publish preserves non-target AtHome windows**
    - Use FsCheck to generate random existing windows and random home_leave_assignments (subset of people)
    - Assert that all AtHome windows for people NOT in home_leave_assignments remain unchanged
    - **Validates: Requirements 2.1, 2.2, 2.4, 7.3**

  - [ ]* 2.3 Write property test for manual windows preservation
    - **Property 4: Manual AtHome windows are never removed by publish**
    - Use FsCheck to generate random manual + derived windows with random assignments
    - Assert that no manual window (`IsDerived = false`) is ever deleted or modified
    - **Validates: Requirements 2.3**

- [x] 3. Checkpoint - Ensure solver exclusion and publish protection work correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Enhanced CancelHomeLeaveCommand with recall parameters
  - [x] 4.1 Add `Reason`, `ExpectedReturnAt`, and `Confirmed` parameters to CancelHomeLeaveCommand
    - Update the command record to include `Confirmed` (bool), `Reason` (string?, max 500), `ExpectedReturnAt` (DateTime?)
    - Update `CancelHomeLeaveResult` to include `NotificationSent` (bool)
    - _Requirements: 8.1, 8.2, 3.1_

  - [x] 4.2 Create `CancelHomeLeaveCommandValidator` with FluentValidation
    - Validate `Reason` max length of 500 characters when not null
    - Validate `Confirmed` must be true
    - Return validation error if reason exceeds 500 characters
    - _Requirements: 3.1, 8.5_

  - [x] 4.3 Update CancelHomeLeaveCommand handler with confirmation and permission checks
    - Reject execution if `Confirmed` is false (validation layer)
    - Verify the requesting user has `SchedulePublish` permission via `IPermissionService`
    - Block any automated invocation — only explicit admin action allowed
    - When `EmergencyFreeze` is NOT active, require explicit override confirmation
    - _Requirements: 3.1, 3.3, 3.4, 3.5_

  - [ ]* 4.4 Write property test for reason length validation
    - **Property 8: Reason length validation**
    - Use FsCheck to generate random strings of length 0–1000
    - Assert that strings > 500 chars are rejected, strings ≤ 500 chars (or null) are accepted
    - **Validates: Requirements 8.1, 8.5**

  - [ ]* 4.5 Write unit tests for recall command edge cases
    - Test recall rejected when `Confirmed = false`
    - Test recall rejected without SchedulePublish permission
    - Test recall of past window throws `InvalidOperationException`
    - Test future window is fully deleted (not truncated)
    - Test in-progress window is truncated to now
    - _Requirements: 3.1, 3.3, 3.4, 3.5_

- [x] 5. Recall notification service
  - [x] 5.1 Create `IRecallNotificationService` interface in Application layer
    - Define `SendRecallNotificationAsync` method accepting spaceId, recalledPersonId, adminName, reason, expectedReturnAt
    - Place in `Jobuler.Application/HomeLeave/Services/`
    - _Requirements: 4.1, 4.2_

  - [x] 5.2 Implement `RecallNotificationService` in Infrastructure layer
    - Resolve the person's linked user ID
    - Send push notification via `IPushNotificationSender.SendPushToUserAsync`
    - Implement retry logic: up to 3 retries with exponential backoff (1s, 2s, 4s)
    - Send email via `IEmailSender.SendAsync`; on failure, log and continue without blocking
    - Build notification payload with admin name, reason (if provided), and expected return time (if provided)
    - Place in `Jobuler.Infrastructure/Notifications/`
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7_

  - [ ]* 5.3 Write property test for recall notification content
    - **Property 5: Recall notification contains all provided information**
    - Use FsCheck to generate random admin names, reasons (including unicode, empty), and return times
    - Assert payload always contains admin name, contains reason when provided, contains return time when provided
    - **Validates: Requirements 4.3, 4.4, 4.5, 8.3, 8.4**

  - [ ]* 5.4 Write unit tests for notification delivery behavior
    - Test push notification retries 3 times on failure
    - Test email failure doesn't block recall operation
    - Test notification sent within expected timeframe
    - _Requirements: 4.1, 4.6, 4.7_

- [x] 6. Recall audit logging
  - [x] 6.1 Integrate audit logging into CancelHomeLeaveCommand handler
    - After successful truncation/deletion, create audit log entry with action "cancel_home_leave"
    - Include: admin user ID, space ID, recalled person's ID, presence window ID, operation type (deleted/truncated), timestamp
    - Include recall reason in audit entry if provided
    - Include before-snapshot with original AtHome window StartsAt and EndsAt
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [ ]* 6.2 Write property test for audit log completeness
    - **Property 6: Audit log entry contains complete recall information**
    - Use FsCheck to generate random window states, reasons, and operation types
    - Assert all required fields are present in the audit entry
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4**

- [x] 7. Checkpoint - Ensure recall flow (command, notifications, audit) works end-to-end
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Live status priority hierarchy verification
  - [x] 8.1 Verify and add explicit test for priority hierarchy in GetGroupLiveStatusQuery
    - Confirm existing code evaluates presence windows before assignment-based status
    - Confirm AtHome window takes precedence over OnMission derived from assignments
    - Add explicit unit test covering all priority combinations
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [ ]* 8.2 Write property test for live status priority hierarchy
    - **Property 7: Live status priority hierarchy**
    - Use FsCheck to generate random combinations of AtHome windows and assignments
    - Assert: AtHome window → "at_home", assignment only → "on_mission", neither → "free_in_base"
    - Assert: AtHome window + assignment → "at_home" (AtHome takes precedence)
    - **Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5**

- [x] 9. Wire recall notification dispatch into CancelHomeLeaveCommand handler
  - [x] 9.1 Integrate RecallNotificationService call after successful recall
    - After truncation/deletion succeeds, call `IRecallNotificationService.SendRecallNotificationAsync`
    - Pass admin name, reason, and expected return time
    - Set `NotificationSent` on the result based on dispatch success
    - Register `IRecallNotificationService` in DI container
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

- [x] 10. API layer updates for recall confirmation UX
  - [x] 10.1 Update HomeLeaveController endpoint to accept enhanced recall parameters
    - Accept `Confirmed`, `Reason`, and `ExpectedReturnAt` in the request body
    - Return warning message when recalling a person with an active AtHome window (travel time notice)
    - Ensure `[Authorize]` attribute is present
    - _Requirements: 3.1, 3.2, 8.1, 8.2_

- [x] 11. Home leave stability in solver payload
  - [x] 11.1 Include existing AtHome windows in solver presence windows list
    - When building the solver payload, include existing AtHome windows in the presence windows DTO
    - This ensures the solver is aware of home leave as a constraint even for non-excluded people
    - _Requirements: 7.1, 7.2, 7.4_

- [x] 12. Final checkpoint - Full integration verification
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using FsCheck with xUnit
- Unit tests validate specific examples and edge cases
- The design confirms GetGroupLiveStatusQuery already implements correct priority hierarchy — task 8 adds explicit test coverage
- All permission checks use `IPermissionService` per architecture rules
- All validation uses FluentValidation per security rules
- Audit log entries are append-only per immutability rules

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "4.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "2.1", "4.2", "4.3", "5.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "4.4", "4.5", "5.2", "8.1"] },
    { "id": 3, "tasks": ["5.3", "5.4", "6.1", "8.2", "11.1"] },
    { "id": 4, "tasks": ["6.2", "9.1"] },
    { "id": 5, "tasks": ["10.1"] }
  ]
}
```
