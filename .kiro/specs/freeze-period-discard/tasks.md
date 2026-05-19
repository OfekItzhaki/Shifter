# Implementation Plan: Freeze Period Discard

## Overview

This plan implements the optional discard of schedule changes made during an emergency freeze period. When an admin deactivates the freeze, they can choose to revert all freeze-period modifications by creating a new immutable draft version from the pre-freeze baseline. The implementation adds a change-count preview query, a dedicated deactivation command with discard logic, permission enforcement, audit logging, and a frontend deactivation dialog with discard toggle.

## Tasks

- [x] 1. Implement freeze-period change count query
  - [x] 1.1 Create `GetFreezePeriodChangesCountQuery` and handler
    - Create `Jobuler.Application/HomeLeave/Queries/GetFreezePeriodChangesCountQuery.cs`
    - Define `GetFreezePeriodChangesCountQuery` record with `SpaceId`, `GroupId`, `RequestingUserId`
    - Define `FreezePeriodChangesCountResult` record with `OverrideCount`, `ManualAssignmentCount`, `SwapCount`, `TotalCount`
    - Implement handler: load `HomeLeaveConfig` for the group, return zeros if freeze not active or `FreezeStartedAt` is null
    - Query assignments in draft versions where `Source = Override` and `CreatedAt >= FreezeStartedAt` scoped to the space
    - Categorize counts by type (overrides, manual assignments, swaps)
    - Ensure query completes within 3 seconds for up to 10,000 assignments
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 7.1, 7.2, 7.3_

  - [x] 1.2 Create `GetFreezePeriodChangesCountQueryValidator` with FluentValidation
    - Validate `SpaceId` and `GroupId` are non-empty GUIDs
    - Validate `RequestingUserId` is non-empty
    - _Requirements: 7.4_

  - [x] 1.3 Add `GetFreezePeriodChangesCount` action to `HomeLeaveConfigController`
    - Add `[HttpGet("freeze-period-changes-count")]` action accepting `spaceId` and `groupId`
    - Verify caller is authenticated and has space access
    - Dispatch `GetFreezePeriodChangesCountQuery` via MediatR
    - Return categorized counts as JSON response
    - Return 404 if group does not exist, 403 if caller lacks space access
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x]* 1.4 Write unit tests for freeze-period change count query
    - Test returns zeros when freeze is not active
    - Test returns zeros when `FreezeStartedAt` is null
    - Test correctly counts overrides created during freeze period
    - Test correctly categorizes manual assignments and swaps
    - Test scopes query to the correct space and draft versions only
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 7.2, 7.3_

- [x] 2. Implement deactivate freeze with discard command
  - [x] 2.1 Create `DeactivateFreezeWithDiscardCommand` and result records
    - Create `Jobuler.Application/HomeLeave/Commands/DeactivateFreezeWithDiscardCommand.cs`
    - Define command record with `SpaceId`, `GroupId`, `RequestingUserId`, `DiscardFreezeChanges`
    - Define `DeactivateFreezeResult` record with `ConfigId`, `DiscardPerformed`, `DiscardVersionId`, `DiscardedChangeCount`, `Config`
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 2.2 Implement `DeactivateFreezeWithDiscardCommandHandler`
    - Require `constraints.manage` permission for all deactivation
    - If `DiscardFreezeChanges` is true, additionally require `schedule.rollback` permission via `IPermissionService`
    - Load `HomeLeaveConfig` — throw `InvalidOperationException` if freeze is not active
    - If `DiscardFreezeChanges` is true:
      - Find most recent published version with `PublishedAt < FreezeStartedAt`
      - Throw `InvalidOperationException` if no pre-freeze baseline exists
      - Count freeze-period changes; if zero, skip version creation
      - Create new draft version via `ScheduleVersion.CreateRollback()` with `RollbackSourceVersionId` pointing to pre-freeze baseline
      - Copy all assignments from pre-freeze version into new draft as atomic operation
      - Recompute cumulative hours for affected persons via `ICumulativeTracker`
      - Invalidate cache via `ICacheService`
    - Call `config.DeactivateEmergencyFreeze()` to clear freeze state
    - Write audit log entry
    - Return result with config and discard metadata
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [x] 2.3 Create `DeactivateFreezeWithDiscardCommandValidator` with FluentValidation
    - Validate `SpaceId` and `GroupId` are non-empty GUIDs
    - Validate `RequestingUserId` is non-empty
    - _Requirements: 6.5_

  - [x]* 2.4 Write unit tests for deactivate freeze with discard command
    - Test standard deactivation (discard=false) clears freeze state without creating versions
    - Test discard creates new draft version from pre-freeze baseline
    - Test discard rejected when no pre-freeze published version exists
    - Test discard rejected when caller lacks `schedule.rollback` permission
    - Test deactivation rejected when freeze is not active (400)
    - Test discard with zero freeze-period changes skips version creation
    - Test atomic operation: partial failure does not leave orphaned versions
    - Test audit log entry created for both discard and non-discard paths
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 6.4, 6.5, 6.6_

- [x] 3. Checkpoint - Ensure query and command logic work correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Add API endpoint and permission enforcement
  - [x] 4.1 Add `DeactivateFreeze` action to `HomeLeaveConfigController`
    - Add `[HttpPost("deactivate-freeze")]` action accepting `spaceId`, `groupId`, and `DeactivateFreezeRequest` body
    - Define `DeactivateFreezeRequest` record with `DiscardFreezeChanges` defaulting to false
    - Dispatch `DeactivateFreezeWithDiscardCommand` via MediatR
    - Return discard version ID and discarded count when discard performed
    - Return updated home-leave config when no discard
    - Return 403 when caller lacks `schedule.rollback` and requests discard
    - Return 400 when freeze is not active
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 6.4, 6.5_

  - [x] 4.2 Implement server-side permission enforcement for discard
    - Verify `schedule.rollback` permission via `IPermissionService.RequirePermissionAsync` before executing discard
    - If permission check fails, return 403 with error message and record denied attempt in audit log
    - Standard deactivation (discard=false) requires only `constraints.manage`
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x]* 4.3 Write unit tests for API endpoint permission enforcement
    - Test 403 returned when discard requested without `schedule.rollback` permission
    - Test standard deactivation succeeds with only `constraints.manage` permission
    - Test denied attempt recorded in audit log
    - Test 400 returned when freeze not active
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 6.5_

- [x] 5. Implement audit logging for discard and deactivation
  - [x] 5.1 Add audit log entries for freeze deactivation actions
    - Log `discard_freeze_changes` action with: actor, space, group, change count, new version ID, baseline version ID
    - Log `deactivate_freeze` action with: actor, space, group, `discard_performed: false` flag
    - Include before-snapshot with `freeze_started_at` and group ID
    - Include after-snapshot with new version ID (if discard) or deactivation confirmation
    - _Requirements: 3.4, 4.3, 4.4_

  - [x]* 5.2 Write unit tests for audit log entries
    - Test discard action produces audit entry with all required fields
    - Test non-discard deactivation produces audit entry with correct flag
    - Test denied permission attempt produces audit entry
    - _Requirements: 3.4, 4.3, 4.4, 5.4_

- [x] 6. Checkpoint - Ensure API layer and audit logging work correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Implement frontend deactivation dialog
  - [x] 7.1 Add API client functions for freeze deactivation
    - Add `getFreezePeriodChangesCount(spaceId, groupId)` function calling GET endpoint
    - Add `deactivateFreeze(spaceId, groupId, discardFreezeChanges)` function calling POST endpoint
    - Place in `lib/api/homeLeave.ts` or equivalent API module
    - _Requirements: 6.1, 7.1_

  - [x] 7.2 Create `FreezeDeactivationDialog` component
    - Create dialog component shown when admin clicks "Deactivate Freeze"
    - On dialog open, fetch freeze-period change counts via API
    - Display categorized change counts (overrides, manual assignments, swaps)
    - Show discard toggle/checkbox with label explaining it will revert freeze-period changes
    - Default discard toggle to unchecked (keep changes)
    - Hide discard toggle if user lacks `schedule.rollback` permission (do not indicate feature exists)
    - Hide discard toggle and show "no changes" message if all counts are zero
    - Disable discard toggle and show error if count fetch fails
    - Include confirm and cancel buttons
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 5.2, 7.2_

  - [x] 7.3 Integrate dialog into `EmergencyFreezeBanner` component
    - Wire "Deactivate Freeze" button to open `FreezeDeactivationDialog`
    - On confirm, call `deactivateFreeze` with the admin's discard choice
    - Handle success: close dialog, refresh config state, show success toast
    - Handle errors: display appropriate error messages (403, 400, 500)
    - _Requirements: 1.1, 4.1, 4.2, 6.1_

  - [x]* 7.4 Write unit tests for frontend deactivation dialog
    - Test dialog displays change counts correctly
    - Test discard toggle hidden when user lacks permission
    - Test discard toggle hidden when no changes exist
    - Test discard toggle disabled on count fetch error
    - Test confirm calls API with correct discard flag
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 5.2_

- [x] 8. Final checkpoint - Full integration verification
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- No schema migrations required — the feature leverages existing tables and columns
- The discard operation reuses `ScheduleVersion.CreateRollback()` following the existing immutability pattern
- All permission checks use `IPermissionService` per architecture rules
- All validation uses FluentValidation per security rules
- Audit log entries are append-only per immutability rules
- The frontend hides the discard option entirely from users without `schedule.rollback` permission

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "2.2", "2.3"] },
    { "id": 2, "tasks": ["1.4", "2.4", "4.1", "4.2"] },
    { "id": 3, "tasks": ["4.3", "5.1"] },
    { "id": 4, "tasks": ["5.2", "7.1"] },
    { "id": 5, "tasks": ["7.2"] },
    { "id": 6, "tasks": ["7.3"] },
    { "id": 7, "tasks": ["7.4"] }
  ]
}
```
