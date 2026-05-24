# Requirements Document

## Introduction

This feature adds a "Regenerate Schedule" action that allows an admin to re-run the constraint solver for the period from today forward, producing a new draft version with fresh assignments. The regeneration is atomic: existing future assignments are only superseded after the solver successfully produces a valid result. This ensures the system never enters a state where future assignments have been deleted but no replacement exists. The new draft follows the standard review/publish flow — it is never auto-published.

## Glossary

- **Regeneration_Service**: The backend service that orchestrates the schedule regeneration workflow: triggering the solver, validating the result, creating a new draft version, and superseding old future assignments
- **Solver**: The stateless CP-SAT constraint solver that generates shift assignments based on a SolverInputDto payload, invoked via the job queue
- **Schedule_Version**: An immutable record representing a set of assignments for a scheduling period, with status Draft, Published, Archived, or Discarded
- **Draft_Version**: A Schedule_Version with status Draft, created by the solver and awaiting admin review before publishing
- **Published_Version**: The currently active Schedule_Version with status Published, containing the assignments visible to all members
- **Future_Assignments**: Assignment rows within a Published_Version whose scheduled date is today or later (relative to the space's local time)
- **Regeneration_Run**: A solver execution triggered specifically by the regeneration action, tracked as a schedule run with trigger mode "regeneration"
- **Admin**: A user with ScheduleRecalculate permission (group owner or space owner) who can trigger schedule regeneration
- **Supersede**: The act of marking future assignments in the old Published_Version as replaced by a new version, without mutating or deleting the original version rows
- **Regeneration_Period**: The date range from today (inclusive) forward for which the solver generates new assignments during regeneration
- **Job_Queue**: The asynchronous processing queue through which solver executions are dispatched and tracked

## Requirements

### Requirement 1: Regeneration Trigger

**User Story:** As an Admin, I want to trigger a schedule regeneration from the management UI, so that I can create a fresh schedule from today forward when circumstances have changed.

#### Acceptance Criteria

1. WHEN a Published_Version exists for the group, THE System SHALL display a "Regenerate Schedule" button in the admin schedule management panel
2. WHEN the Admin activates the "Regenerate Schedule" button, THE System SHALL display a confirmation dialog explaining that a new draft will be created for the period from today forward
3. WHEN the Admin confirms the regeneration, THE Regeneration_Service SHALL dispatch a Regeneration_Run to the Job_Queue with trigger mode "regeneration" and start time set to today in the space's local time
4. IF no Published_Version exists for the group, THEN THE System SHALL hide the "Regenerate Schedule" button
5. WHILE a Regeneration_Run is already in progress for the group, THE System SHALL disable the "Regenerate Schedule" button and display a status indicator showing the run is in progress

### Requirement 2: Solver Execution

**User Story:** As a system, I want the solver to generate new assignments for the regeneration period using current group data, so that the new schedule reflects the latest constraints, members, and tasks.

#### Acceptance Criteria

1. WHEN the Regeneration_Run executes, THE Solver SHALL receive a payload built from the current database state (tasks, people, constraints, settings, qualifications) for the Regeneration_Period
2. THE Solver SHALL generate assignments only for dates within the Regeneration_Period (today forward), preserving no assignments from the old version
3. WHEN the Solver completes successfully, THE Regeneration_Service SHALL store the solver result as a new Draft_Version linked to the Regeneration_Run
4. THE Regeneration_Service SHALL include stability weights in the solver payload based on historical assignment data, consistent with standard solver runs
5. THE Regeneration_Run SHALL use the same solver timeout and configuration as standard schedule runs

### Requirement 3: Conditional Supersession

**User Story:** As an Admin, I want old future assignments to be superseded only after the solver successfully produces a new schedule, so that the system never loses coverage without a replacement ready.

#### Acceptance Criteria

1. WHEN the Solver completes successfully AND a Draft_Version is created, THE Regeneration_Service SHALL mark the new Draft_Version with a reference to the Published_Version it is intended to replace (supersedes_version_id)
2. THE Regeneration_Service SHALL NOT modify, delete, or invalidate Future_Assignments in the Published_Version until the Admin publishes the new Draft_Version through the standard publish flow
3. IF the Solver fails (timeout, infeasibility, or error), THEN THE Regeneration_Service SHALL leave the Published_Version and all Future_Assignments unchanged
4. IF the Solver fails, THEN THE Regeneration_Service SHALL record the failure reason in the Regeneration_Run record and notify the Admin of the failure
5. THE Published_Version SHALL remain the active schedule visible to all members until the new Draft_Version is explicitly published by the Admin

### Requirement 4: Draft Version Creation

**User Story:** As a system, I want the regeneration result stored as a standard draft version, so that it goes through the normal review and publish workflow.

#### Acceptance Criteria

1. THE Regeneration_Service SHALL create the new Draft_Version with status Draft, following the same schema as solver-generated versions from standard runs
2. THE Draft_Version SHALL contain assignments only for the Regeneration_Period (today forward)
3. THE Draft_Version SHALL include metadata indicating it was created via regeneration (source_type = "regeneration") and reference the Regeneration_Run ID
4. WHEN the Draft_Version is created, THE System SHALL navigate the Admin to the draft review panel where the Admin can inspect, simulate, or publish the new schedule
5. THE Draft_Version SHALL be publishable, discardable, and editable via the existing sandbox and override mechanisms

### Requirement 5: Publish Completes Regeneration

**User Story:** As an Admin, I want publishing the regeneration draft to archive the old version's future assignments, so that the new schedule becomes the active one.

#### Acceptance Criteria

1. WHEN the Admin publishes the Draft_Version created by regeneration, THE System SHALL archive the old Published_Version using the standard publish mechanism (old version status changes to Archived)
2. WHEN the Admin publishes the Draft_Version, THE new version SHALL become the Published_Version visible to all members
3. THE publish action SHALL produce an audit log entry recording that the regeneration draft was published, including the superseded version ID and the regeneration run ID
4. THE System SHALL NOT delete or mutate assignment rows in the archived version — archival preserves the historical record intact

### Requirement 6: Discard Regeneration Draft

**User Story:** As an Admin, I want to discard the regeneration draft if the new schedule is unsatisfactory, so that the current published schedule remains unchanged.

#### Acceptance Criteria

1. WHEN the Admin discards the Draft_Version created by regeneration, THE System SHALL set the draft status to Discarded using the existing discard mechanism
2. WHEN the Draft_Version is discarded, THE Published_Version SHALL remain active and unchanged with all Future_Assignments intact
3. THE discard action SHALL produce an audit log entry recording that the regeneration draft was discarded
4. WHEN the discard completes, THE System SHALL re-enable the "Regenerate Schedule" button, allowing the Admin to trigger a new regeneration

### Requirement 7: Access Control

**User Story:** As a system, I want only authorized admins to trigger schedule regeneration, so that unauthorized users cannot disrupt the active schedule.

#### Acceptance Criteria

1. THE Regeneration_Service SHALL require the requesting user to hold the ScheduleRecalculate permission for the target space before dispatching the Regeneration_Run
2. IF a user without ScheduleRecalculate permission attempts to trigger regeneration, THEN THE System SHALL deny the request and return a 403 status
3. THE "Regenerate Schedule" button SHALL only be visible to users who hold the ScheduleRecalculate permission
4. THE Regeneration_Service SHALL verify permission at the API layer before dispatching the job, consistent with existing schedule run permission checks

### Requirement 8: Progress Tracking

**User Story:** As an Admin, I want to see the status of my regeneration run, so that I know when the new draft is ready for review.

#### Acceptance Criteria

1. WHEN a Regeneration_Run is dispatched, THE System SHALL return the run ID to the client immediately (HTTP 202 Accepted)
2. WHILE the Regeneration_Run is in progress, THE System SHALL allow the Admin to poll the run status via the existing schedule-runs endpoint
3. WHEN the Regeneration_Run completes successfully, THE System SHALL update the run status to "completed" and include the new Draft_Version ID in the run record
4. WHEN the Regeneration_Run fails, THE System SHALL update the run status to "failed" and include a localized error message describing the failure reason
5. THE System SHALL display a real-time status indicator in the UI showing the regeneration progress (queued, running, completed, failed)

### Requirement 9: Concurrency Protection

**User Story:** As a system, I want to prevent concurrent regeneration runs for the same group, so that conflicting drafts are not created simultaneously.

#### Acceptance Criteria

1. WHILE a Regeneration_Run with status "queued" or "running" exists for the group, THE Regeneration_Service SHALL reject new regeneration requests for that group with a 409 Conflict response
2. THE Regeneration_Service SHALL check for in-progress runs atomically before dispatching a new run to prevent race conditions
3. IF a Regeneration_Run has been in "running" status for longer than the solver timeout plus a grace period, THEN THE Regeneration_Service SHALL treat the run as failed and allow new regeneration requests
4. THE System SHALL NOT prevent standard solver runs or manual overrides while a Regeneration_Run is in progress — only concurrent regeneration runs for the same group are blocked

### Requirement 10: Subscription Validation

**User Story:** As a system, I want to verify the group's subscription status before running regeneration, so that expired trial groups cannot consume solver resources.

#### Acceptance Criteria

1. WHEN the Admin triggers regeneration, THE Regeneration_Service SHALL check the group's subscription status before dispatching the Regeneration_Run
2. IF the group's trial has expired and no active subscription exists, THEN THE Regeneration_Service SHALL reject the request with a 402 status and a localized message indicating the subscription requirement
3. IF the group has an active subscription or is within the trial period, THEN THE Regeneration_Service SHALL proceed with the regeneration
