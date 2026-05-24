# Implementation Plan: Schedule Regeneration

## Overview

This plan implements the "Regenerate Schedule" feature, allowing admins to re-run the constraint solver from today forward and produce a new draft version. Implementation follows Clean Architecture layers (Domain → Infrastructure → Application → API → Worker → Frontend), starting with domain model changes, then the EF migration, the command handler with all guards, the API endpoint, worker integration, and finally the frontend components.

## Tasks

- [x] 1. Domain layer — Extend ScheduleRun and ScheduleVersion entities
  - [x] 1.1 Add `Regeneration` value to `ScheduleRunTrigger` enum and new fields to `ScheduleRun`
    - Add `Regeneration` to `ScheduleRunTrigger` enum: `{ Standard, Emergency, Manual, Rollback, Regeneration }`
    - Add `GroupId` (nullable Guid) property to `ScheduleRun`
    - Add `ResultVersionId` (nullable Guid) property to `ScheduleRun`
    - Add `SetResultVersion(Guid versionId)` method
    - Update `Create` factory to accept optional `Guid? groupId` parameter
    - _Requirements: 1.3, 2.3, 8.3, 9.1_

  - [x] 1.2 Add `SupersedesVersionId` and `SourceType` fields to `ScheduleVersion`
    - Add `SupersedesVersionId` (nullable Guid) property
    - Add `SourceType` (nullable string) property
    - Add `CreateRegenerationDraft` factory method accepting `spaceId`, `versionNumber`, `sourceRunId`, `supersedesVersionId`, `createdByUserId`, optional `summaryJson`
    - Factory sets `Status = Draft`, `SourceType = "regeneration"`, `SupersedesVersionId` to the published version ID
    - _Requirements: 3.1, 4.1, 4.3_

- [x] 2. Infrastructure layer — EF migration for new columns and index
  - [x] 2.1 Update EF configurations and create migration
    - Update `ScheduleRunConfiguration` to map `GroupId` and `ResultVersionId` columns (snake_case: `group_id`, `result_version_id`)
    - Update `ScheduleVersionConfiguration` to map `SupersedesVersionId` and `SourceType` columns (snake_case: `supersedes_version_id`, `source_type`)
    - Configure FK from `schedule_runs.group_id` → `groups.id`
    - Configure FK from `schedule_runs.result_version_id` → `schedule_versions.id`
    - Configure FK from `schedule_versions.supersedes_version_id` → `schedule_versions.id`
    - Add partial index `ix_schedule_runs_group_regeneration` on `(space_id, group_id, status)` WHERE `trigger_type = 'Regeneration' AND status IN ('Queued', 'Running')`
    - Generate migration via `dotnet ef migrations add AddScheduleRegeneration`
    - _Requirements: 9.1, 9.2_

- [x] 3. Checkpoint — Domain and migration complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Application layer — TriggerRegenerationCommand and handler
  - [x] 4.1 Create `TriggerRegenerationCommand` record and handler
    - Create `TriggerRegenerationCommand(Guid SpaceId, Guid GroupId, Guid RequestedByUserId) : IRequest<Guid>`
    - Handler logic:
      1. Set RLS session variables (`app.current_space_id`, `app.current_user_id`)
      2. Check group subscription status — reject with `PaymentRequiredException` (402) if trial expired and no active subscription
      3. Check for in-progress regeneration runs for this group (status Queued or Running) — reject with `ConflictException` (409) if exists
      4. Handle stale runs: if a run has been "Running" longer than solver timeout + grace period, mark it failed
      5. Find current published version for the group — reject with `InvalidOperationException` (400) if none exists
      6. Create `ScheduleRun` with `TriggerType = Regeneration`, `BaselineVersionId = publishedVersion.Id`, `GroupId = groupId`
      7. Enqueue `SolverJobMessage` with `triggerMode = "regeneration"`, `startTime = today in space timezone`
      8. Return `run.Id`
    - _Requirements: 1.3, 9.1, 9.2, 9.3, 10.1, 10.2, 10.3_

  - [x] 4.2 Create `TriggerRegenerationValidator` with FluentValidation
    - Validate `SpaceId` is not empty
    - Validate `GroupId` is not empty
    - Validate `RequestedByUserId` is not empty
    - _Requirements: 1.3_

  - [ ]* 4.3 Write property test for concurrent regeneration rejection
    - **Property 5: Concurrent regeneration rejection**
    - For any group that has a regeneration run with status "Queued" or "Running", a new regeneration request SHALL be rejected with 409 and no new ScheduleRun created
    - **Validates: Requirements 9.1**

  - [ ]* 4.4 Write property test for subscription gating
    - **Property 9: Subscription gating**
    - For any group whose trial has expired and has no active subscription, a regeneration request SHALL be rejected with 402. For any group with active subscription or within trial, the request SHALL proceed
    - **Validates: Requirements 10.2, 10.3**

  - [ ]* 4.5 Write property test for stale run timeout recovery
    - **Property 6: Stale run timeout recovery**
    - For any regeneration run in "Running" status longer than (solver_timeout + grace_period), the system SHALL treat it as failed and allow new regeneration requests
    - **Validates: Requirements 9.3**

- [x] 5. API layer — Regenerate endpoint on ScheduleRunsController
  - [x] 5.1 Add `POST /spaces/{spaceId}/schedule-runs/regenerate` endpoint
    - Add `[HttpPost("regenerate")]` action method to `ScheduleRunsController`
    - Require `ScheduleRecalculate` permission via `_permissions.RequirePermissionAsync`
    - Accept `RegenerateRequest(Guid GroupId)` from body
    - Dispatch `TriggerRegenerationCommand(spaceId, request.GroupId, CurrentUserId)`
    - Return `202 Accepted` with `{ runId }`
    - _Requirements: 1.3, 7.1, 7.2, 7.4, 8.1_

  - [ ]* 5.2 Write property test for permission enforcement
    - **Property 8: Permission enforcement**
    - For any user without ScheduleRecalculate permission, a regeneration request SHALL be rejected with HTTP 403 and no ScheduleRun created
    - **Validates: Requirements 7.1, 7.2**

- [x] 6. Checkpoint — Backend command and API complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Worker — Handle regeneration trigger mode in background worker
  - [x] 7.1 Update solver background worker to handle regeneration runs
    - When `triggerMode == "regeneration"`:
      - Build solver payload using `ISolverPayloadNormalizer` with `startTime = today` in space timezone
      - Include stability weights consistent with standard runs
      - On solver success: create `ScheduleVersion` via `CreateRegenerationDraft` factory with `SupersedesVersionId` set to baseline version ID
      - Insert assignments from solver output into the new draft version
      - Call `run.SetResultVersion(newVersion.Id)` and mark run completed
      - Do NOT auto-discard existing drafts (regeneration drafts coexist with other drafts)
    - On solver failure (timeout, infeasibility, error):
      - Mark run as failed with error summary
      - Leave published version unchanged
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3_

  - [ ]* 7.2 Write property test for successful regeneration creating a correctly linked draft
    - **Property 2: Successful regeneration creates a correctly linked draft**
    - For any valid solver output, the system SHALL create exactly one ScheduleVersion with status=Draft, SourceRunId matching run ID, SupersedesVersionId matching published version ID, SourceType="regeneration"
    - **Validates: Requirements 2.3, 3.1, 4.3, 8.3**

  - [ ]* 7.3 Write property test for failed regeneration recording error without side effects
    - **Property 3: Failed regeneration records error without side effects**
    - For any solver failure, the run SHALL have status=Failed, non-empty ErrorSummary, and no new ScheduleVersion created
    - **Validates: Requirements 3.3, 3.4, 8.4**

  - [ ]* 7.4 Write property test for regeneration period assignment bounds
    - **Property 4: All regeneration draft assignments are within the regeneration period**
    - For any draft version created by regeneration with start date S, every assignment SHALL have slot start date >= S
    - **Validates: Requirements 2.2, 4.2**

  - [ ]* 7.5 Write property test for published version immutability
    - **Property 1: Published version immutability during regeneration lifecycle**
    - For any regeneration lifecycle event, the published version's status SHALL remain "Published" and assignment rows unchanged
    - **Validates: Requirements 3.2, 3.3, 3.5, 5.4, 6.2**

- [x] 8. Checkpoint — Worker integration complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Application layer — Publish audit log for regeneration
  - [x] 9.1 Update `PublishVersionCommand` handler to include regeneration audit metadata
    - When publishing a version with `SourceType == "regeneration"`:
      - Include `supersedes_version_id` in audit log `afterJson`
      - Include `regeneration_run_id` (from `SourceRunId`) in audit log `afterJson`
      - Include `published_by_user_id` in audit log entry
    - Existing publish flow (archive old version, set new as Published) already handles the version transition
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [ ]* 9.2 Write property test for audit log completeness on regeneration publish
    - **Property 10: Audit log completeness on regeneration publish**
    - For any regeneration draft that is published, the audit log entry SHALL contain superseded version ID, regeneration run ID, and publishing user ID
    - **Validates: Requirements 5.3**

  - [ ]* 9.3 Write property test for regeneration not blocking standard runs
    - **Property 7: Regeneration does not block standard runs**
    - For any group with an in-progress regeneration run, triggering a standard or emergency solver run SHALL succeed without conflict
    - **Validates: Requirements 9.4**

- [x] 10. Frontend — RegenerateButton component
  - [x] 10.1 Create `RegenerateButton` component in the schedule management panel
    - Display "Regenerate Schedule" button only when:
      - User has `ScheduleRecalculate` permission
      - A published version exists for the selected group
    - Hide button when no published version exists
    - Disable button and show status indicator when a regeneration run is in progress
    - On click, open `RegenerateConfirmDialog`
    - _Requirements: 1.1, 1.4, 1.5, 7.3_

  - [x] 10.2 Create `RegenerateConfirmDialog` component
    - Display confirmation dialog explaining that a new draft will be created for the period from today forward
    - Include "Confirm" and "Cancel" action buttons
    - On confirm: call `triggerRegeneration(spaceId, groupId)` API function
    - On success (202): close dialog, start polling run status
    - On 402 error: show subscription expired message
    - On 409 error: show "regeneration already in progress" message
    - On 403 error: show permission denied message
    - _Requirements: 1.2, 1.3, 10.2_

  - [x] 10.3 Create `RegenerationStatusIndicator` component
    - Poll `GET /spaces/{spaceId}/schedule-runs/{runId}` for status updates
    - Display states: queued (spinner), running (progress), completed (success + navigate), failed (error message)
    - On completed: navigate admin to draft review panel with the new `resultVersionId`
    - On failed: display localized error message from `errorSummary`
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 4.4_

  - [x] 10.4 Add `triggerRegeneration` API client function
    - Add `triggerRegeneration(spaceId: string, groupId: string): Promise<{ runId: string }>` to schedule API module
    - POST to `/spaces/${spaceId}/schedule-runs/regenerate` with `{ groupId }` body
    - Reuse existing `getRunStatus(spaceId, runId)` for polling
    - _Requirements: 1.3, 8.1_

- [x] 11. Final checkpoint — Full integration
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use **FsCheck** for backend (C#/.NET, minimum 100 iterations per property) and **fast-check** for frontend (TypeScript)
- Unit tests validate specific examples and edge cases
- The existing `PublishVersionCommand` already handles archiving the old published version — regeneration publish reuses this flow
- The existing `DiscardVersionCommand` already handles discarding drafts — regeneration discard reuses this flow (Requirement 6)
- The existing polling mechanism via `GET /schedule-runs/{runId}` is reused for regeneration status tracking
- Step documentation under `docs/steps/` should be created alongside each implementation task per workspace conventions
- All commands require FluentValidation validators per architecture rules
- All endpoints require `[Authorize]` and permission checks per security rules
- The solver is never called synchronously — always via the job queue per architecture rules

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["4.1", "4.2"] },
    { "id": 3, "tasks": ["4.3", "4.4", "4.5", "5.1"] },
    { "id": 4, "tasks": ["5.2", "7.1"] },
    { "id": 5, "tasks": ["7.2", "7.3", "7.4", "7.5"] },
    { "id": 6, "tasks": ["9.1"] },
    { "id": 7, "tasks": ["9.2", "9.3", "10.4"] },
    { "id": 8, "tasks": ["10.1", "10.2", "10.3"] }
  ]
}
```
