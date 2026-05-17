# Implementation Plan: Draft Simulation Sandbox

## Overview

This plan implements a "what-if" simulation sandbox for Shifter's scheduling system. The architecture follows a thin-backend/thick-frontend pattern: a Zustand store owns all sandbox state, a pure payload builder merges overrides with the baseline `SolverInputDto`, and a stateless backend endpoint forwards the payload to the solver. Publish persists all overrides in a single transaction; discard clears frontend state.

## Tasks

- [x] 1. Backend — Simulation endpoint and baseline query
  - [x] 1.1 Create `SimulationController` with `POST /spaces/{spaceId}/groups/{groupId}/simulate` endpoint
    - Add `SimulationController.cs` in `Jobuler.Api/Controllers/`
    - Accept `SimulateRequest(SolverInputDto Payload)` body
    - Call `IPermissionService.RequirePermissionAsync` for group owner / space owner
    - Call `ISolverClient.SolveAsync` directly (synchronous, no job queue) — this is an intentional exception to the architecture rule for admin-only simulation
    - Return `SolverOutputDto` in response body
    - Create NO database records
    - Add FluentValidation validator for `SimulateRequest` (required fields, non-negative headcounts)
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 7.5, 11.3_

  - [x] 1.2 Create `GET /spaces/{spaceId}/groups/{groupId}/solver-baseline` endpoint
    - Add action to `SimulationController` (or `ScheduleRunsController`)
    - Call `IPermissionService.RequirePermissionAsync` for group owner / space owner
    - Call `ISolverPayloadNormalizer.BuildAsync` to construct the current `SolverInputDto`
    - Return the full `SolverInputDto` as JSON response
    - _Requirements: 1.2, 11.1_

  - [x] 1.3 Create DTOs for publish sandbox request
    - Add `SimulateRequest`, `PublishSandboxRequest`, `TaskOverrideDto`, `ConstraintOverrideDto`, `SettingsOverrideDto` records in `Jobuler.Application/Scheduling/Models/`
    - Add FluentValidation validators for each DTO (action enum validation, range checks for settings 0–24 hours)
    - _Requirements: 5.5, 9.1, 9.2, 9.3, 9.4_

- [x] 2. Backend — Publish sandbox command
  - [x] 2.1 Implement `PublishSandboxCommand` handler
    - Create `PublishSandboxCommand.cs` in `Jobuler.Application/Scheduling/Commands/`
    - Accept `PublishSandboxRequest` with VersionId, TaskOverrides, ConstraintOverrides, MemberExclusions, SettingsOverrides
    - Verify group owner / space owner permissions
    - Persist task overrides (add/edit/remove) to tasks table within a single transaction
    - Persist constraint overrides (add/edit/remove) to constraints table
    - Persist member exclusions as opt-out records
    - Persist settings overrides (rest hours, home-leave params, min people at base) to group record
    - Delegate to existing `PublishVersionCommand` for the actual version publish
    - Produce audit log entry with before/after snapshot
    - Roll back entire transaction on any failure
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 11.4_

  - [x] 2.2 Add `POST /spaces/{spaceId}/groups/{groupId}/publish-sandbox` endpoint to `SimulationController`
    - Wire `PublishSandboxCommand` via MediatR
    - Validate request body with FluentValidation
    - Return 409 Conflict if version already published/discarded
    - _Requirements: 9.5, 9.6_

- [x] 3. Checkpoint — Backend compilation and validation
  - Ensure all backend code compiles without errors
  - Ensure FluentValidation rules cover all edge cases
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Frontend — Sandbox Zustand store and payload builder
  - [x] 4.1 Create `useSandboxStore` Zustand store
    - Add `apps/web/lib/store/sandboxStore.ts`
    - Implement full `SandboxState` interface: `isActive`, `groupId`, `draftVersionId`, `baseline`, `taskOverrides`, `constraintOverrides`, `memberExclusions`, `settingsOverrides`, `lastSimulationResult`, `isSimulating`, `simulationError`
    - Implement actions: `enterSandbox`, `exitSandbox`, `addTask`, `editTask`, `removeTask`, `addConstraint`, `editConstraint`, `removeConstraint`, `toggleMember`, `updateSettings`, `setSimulationResult`, `setSimulating`, `setSimulationError`
    - Do NOT persist store to localStorage (state lost on tab close by design)
    - _Requirements: 7.1, 7.2, 7.4, 1.2_

  - [x] 4.2 Implement `buildOverridePayload` pure function
    - Add `apps/web/lib/store/sandboxPayloadBuilder.ts`
    - Merge baseline `SolverInputDto` with all sandbox overrides:
      - Apply task additions (append to `TaskSlots`), modifications (replace matching slot), and removals (filter out)
      - Apply constraint additions/modifications/removals to `HardConstraints` and `SoftConstraints`
      - Filter `People` list by removing excluded member IDs
      - Apply settings overrides: inject/update `min_rest_between_assignments` hard constraint, update `HomeLeaveConfig` fields
    - Return a new `SolverInputDto` (no mutation of baseline)
    - _Requirements: 6.1, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 4.2, 4.3, 5.1, 5.2, 5.3, 5.4_

  - [ ]* 4.3 Write property test: Payload builder preserves unmodified fields
    - **Property 1: Payload builder preserves unmodified fields**
    - **Validates: Requirements 6.1, 7.1**
    - Use fast-check to generate random valid baselines and empty override sets
    - Assert all fields in output are identical to baseline when no overrides are applied

  - [ ]* 4.4 Write property test: Task removal reduces slot count
    - **Property 2: Task removal reduces slot count**
    - **Validates: Requirements 2.3, 2.5**
    - Use fast-check to generate baselines with N slots and random subsets for removal
    - Assert output has exactly N - |removed| slots and none of the removed IDs appear

  - [ ]* 4.5 Write property test: Task addition increases slot count
    - **Property 3: Task addition increases slot count**
    - **Validates: Requirements 2.1, 2.5**
    - Use fast-check to generate baselines with N slots and K new valid slots
    - Assert output has exactly N + K slots and all added slots appear with correct field values

  - [ ]* 4.6 Write property test: Member exclusion removes from people list
    - **Property 4: Member exclusion removes from people list**
    - **Validates: Requirements 4.2, 4.5**
    - Use fast-check to generate baselines with P people and random exclusion subsets
    - Assert output has exactly P - |excluded| people and none of the excluded IDs appear

  - [ ]* 4.7 Write property test: Member re-inclusion restores original data
    - **Property 5: Member re-inclusion restores original data**
    - **Validates: Requirements 4.3**
    - Use fast-check to generate baselines, exclude then re-include a member
    - Assert the member's `PersonEligibilityDto` is identical to the original baseline entry

  - [ ]* 4.8 Write property test: Settings override round-trip
    - **Property 6: Settings override round-trip**
    - **Validates: Requirements 5.1, 5.5**
    - Use fast-check to generate `minRestBetweenShiftsHours` in [0, 24]
    - Assert output's `min_rest_between_assignments` constraint payload `min_hours` equals the overridden value

  - [ ]* 4.9 Write property test: Constraint removal reduces constraint count
    - **Property 7: Constraint removal reduces constraint count**
    - **Validates: Requirements 3.3, 3.5**
    - Use fast-check to generate baselines with H hard constraints and random removal subsets
    - Assert output has exactly H - |removed| hard constraints and none of the removed IDs appear

  - [ ]* 4.10 Write property test: Override payload is a valid SolverInputDto
    - **Property 8: Override payload is a valid SolverInputDto**
    - **Validates: Requirements 6.1, 6.2**
    - Use fast-check to generate random valid baselines and random valid overrides
    - Assert output passes SolverInputDto schema validation (required fields present, correct types, non-negative headcounts)

- [x] 5. Checkpoint — Store and payload builder tests
  - Ensure all property tests pass with `vitest --run`
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Frontend — Sandbox entry and settings panel
  - [x] 6.1 Create sandbox entry UI in draft review panel
    - In the existing schedule/draft review component, add "Enter Simulation" button
    - Show button only when a `Draft_Version` exists for the group
    - Hide button when no draft exists
    - On click: fetch solver baseline via `GET /solver-baseline`, then call `enterSandbox` store action
    - _Requirements: 1.1, 1.2, 1.4_

  - [x] 6.2 Create `SandboxSettingsPanel` component
    - Add `apps/web/components/sandbox/SandboxSettingsPanel.tsx`
    - Render tabs: Tasks, Constraints, Members, Settings
    - Each tab reads from and writes to the Zustand store
    - Do NOT subscribe to `lastSimulationResult` (prevents re-renders on simulation completion)
    - Include "Run Simulation" button that triggers the simulation API call
    - _Requirements: 1.3, 8.1, 8.4_

  - [x] 6.3 Implement Tasks tab in settings panel
    - Display task list from sandbox state with add/edit/remove controls
    - Visually distinguish overridden tasks (added, modified, removed) with color coding or badges
    - Task form fields: name, time window, headcount, burden level, required roles, required qualifications
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 6.4 Implement Constraints tab in settings panel
    - Display constraint list from sandbox state with add/edit/remove controls
    - Visually distinguish overridden constraints from unmodified ones
    - Constraint form fields: rule type, severity (hard/soft), scope type, scope ID, payload
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 6.5 Implement Members tab in settings panel
    - Display member list with toggle controls to include/exclude each member
    - Show count of active members vs total members
    - When excluded, member is removed from override payload's People list
    - When re-included, member is restored with original eligibility data
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 6.6 Implement Settings tab in settings panel
    - Minimum rest hours slider/input (0–24 range with client-side validation)
    - Home-leave parameters (eligibility threshold, leave duration, leave capacity, balance value) — shown only when group has home-leave enabled
    - Minimum people at base — shown only when group is a closed base
    - Qualification requirements editor for task slots
    - Validate all values within allowed ranges before including in override payload
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

- [x] 7. Frontend — Schedule preview and simulation execution
  - [x] 7.1 Create `SandboxSchedulePreview` component
    - Add `apps/web/components/sandbox/SandboxSchedulePreview.tsx`
    - Subscribe to `lastSimulationResult` from the Zustand store
    - Render assignment table (reuse existing `ScheduleTaskTable` component)
    - Display home-leave preview section when group has home-leave enabled
    - Show loading indicator during simulation (`isSimulating` state)
    - Show localized error messages on failure (timeout, infeasibility)
    - _Requirements: 6.4, 6.5, 8.2, 8.3, 12.1, 12.3_

  - [x] 7.2 Implement simulation run trigger
    - On "Run Simulation" click: call `buildOverridePayload`, then `POST /simulate` with the payload
    - Set `isSimulating(true)` before request, `isSimulating(false)` after
    - On success: call `setSimulationResult` with the response
    - On solver timeout: display localized "solver timed out" message
    - On infeasibility: display localized "schedule infeasible" message with hard conflicts
    - On network error: display error, keep settings panel state intact
    - Allow multiple runs within the same session
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [x] 7.3 Implement home-leave preview in schedule preview
    - When group has home-leave enabled, display home-leave assignments alongside task assignments
    - Show which members are assigned home-leave and during which time windows
    - Omit home-leave section when group does not have home-leave enabled
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

- [x] 8. Frontend — Publish and discard flows
  - [x] 8.1 Implement publish flow UI
    - Add "Publish" button in sandbox panel
    - On click: construct `PublishSandboxRequest` from sandbox state (task overrides, constraint overrides, member exclusions, settings overrides, version ID)
    - Call `POST /publish-sandbox` endpoint
    - On success: call `exitSandbox`, navigate to published schedule view
    - On failure (409 conflict, 500 error): display error message, preserve sandbox state
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 8.2 Implement discard flow UI
    - Add "Discard" button in sandbox panel
    - On click: call existing discard version endpoint
    - On success: call `exitSandbox`, clear all sandbox state, navigate to group schedule view
    - Do NOT persist any sandbox changes to the database
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

  - [x] 8.3 Implement navigation guards
    - Add `beforeunload` event listener when sandbox is active
    - Show in-app confirmation dialog when navigating away without publish/discard
    - Warn admin that unsaved simulation changes will be lost
    - On browser tab close/refresh: sandbox state is naturally discarded (non-persisted Zustand)
    - _Requirements: 7.3, 7.4_

- [x] 9. Frontend — Access control and reactive UI boundaries
  - [x] 9.1 Implement frontend access control
    - Hide "Enter Simulation" button for users without group owner / space owner permissions
    - If unauthorized user somehow reaches sandbox route, redirect with access denied message
    - _Requirements: 11.1, 11.2_

  - [x] 9.2 Implement reactive UI split rendering
    - Ensure settings panel and schedule preview are separate React components with independent state subscriptions
    - Settings panel subscribes only to override state (tasks, constraints, members, settings)
    - Schedule preview subscribes only to simulation results
    - Verify that modifying parameters does NOT re-render the settings panel or reset scroll position
    - Verify that simulation completion does NOT re-render the settings panel
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [x] 10. Checkpoint — Full integration verification
  - Ensure all frontend and backend code compiles
  - Ensure all property tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ]* 11. Backend unit tests
  - [ ]* 11.1 Write unit tests for `SimulationController` permission checks
    - Test 403 for non-owner users attempting to simulate
    - Test 403 for non-owner users attempting to access solver baseline
    - _Requirements: 11.1, 11.2, 11.3_

  - [ ]* 11.2 Write unit tests for `PublishSandboxCommand` transaction behavior
    - Test all-or-nothing: if any write fails, entire publish is rolled back
    - Test audit log creation with before/after snapshot
    - Test 409 Conflict when version already published/discarded
    - _Requirements: 9.6, 9.7_

  - [ ]* 11.3 Write unit tests for payload validation
    - Test FluentValidation rules for `SimulateRequest` (required fields, valid types)
    - Test settings range validation (0–24 for rest hours, non-negative values)
    - Test task override action enum validation
    - _Requirements: 5.5, 6.2_

- [ ]* 12. Frontend unit tests
  - [ ]* 12.1 Write unit tests for sandbox store actions
    - Test `enterSandbox` initializes state correctly
    - Test `exitSandbox` clears all state
    - Test add/edit/remove task overrides
    - Test add/edit/remove constraint overrides
    - Test `toggleMember` inclusion/exclusion
    - Test `updateSettings` with valid and invalid values
    - _Requirements: 1.2, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 4.2, 4.3, 5.1_

  - [ ]* 12.2 Write unit tests for settings validation
    - Test range checks for rest hours (0–24)
    - Test range checks for home-leave parameters
    - Test rejection of out-of-range values
    - _Requirements: 5.5_

  - [ ]* 12.3 Write unit tests for navigation guard
    - Test `beforeunload` event fires when sandbox is active
    - Test confirmation dialog on in-app navigation
    - Test no warning when sandbox is inactive
    - _Requirements: 7.3_

- [x] 13. Final checkpoint — Ensure all tests pass
  - Run full test suite (backend + frontend)
  - Ensure all property tests pass
  - Ensure all unit tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties of the `buildOverridePayload` pure function
- Unit tests validate specific examples and edge cases
- The simulation endpoint intentionally calls the solver synchronously (exception to architecture rule) because simulation is admin-only with low concurrency and requires immediate feedback
- All sandbox state is frontend-only — no backend session management needed
- The `beforeunload` guard and non-persisted Zustand store together satisfy the non-destructive state management requirements

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "4.1"] },
    { "id": 2, "tasks": ["2.2", "4.2"] },
    { "id": 3, "tasks": ["4.3", "4.4", "4.5", "4.6", "4.7", "4.8", "4.9", "4.10"] },
    { "id": 4, "tasks": ["6.1", "6.2"] },
    { "id": 5, "tasks": ["6.3", "6.4", "6.5", "6.6"] },
    { "id": 6, "tasks": ["7.1", "7.2"] },
    { "id": 7, "tasks": ["7.3", "8.1", "8.2", "8.3"] },
    { "id": 8, "tasks": ["9.1", "9.2"] },
    { "id": 9, "tasks": ["11.1", "11.2", "11.3", "12.1", "12.2", "12.3"] }
  ]
}
```
