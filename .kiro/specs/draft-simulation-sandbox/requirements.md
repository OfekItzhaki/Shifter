# Requirements Document

## Introduction

This feature adds a "what-if" simulation sandbox to Shifter's scheduling system. After a draft schedule is created, an admin or group owner can enter a simulation mode where they temporarily modify scheduling parameters — tasks, constraints, members, rest time, minimum people at base, home-leave settings, and qualifications — and re-run the solver to preview different results. No changes persist to the database until the admin explicitly publishes. Discarding the draft rolls back all sandbox modifications. The sandbox maintains reactive UI behavior where only the schedule preview re-renders on parameter changes, keeping the settings panel stable.

## Glossary

- **Simulation_Sandbox**: The transient workspace where an admin modifies scheduling parameters and previews solver results without persisting changes to the database
- **Sandbox_State**: The collection of all parameter overrides (tasks, constraints, members, settings) held in frontend memory during a simulation session
- **Simulation_Run**: A solver execution triggered from the sandbox using overridden parameters instead of database-stored values
- **Solver**: The CP-SAT constraint solver that generates schedules based on a SolverInputDto payload
- **SolverPayloadNormalizer**: The backend service that builds a SolverInputDto by querying database tables for tasks, people, constraints, and settings
- **Schedule_Preview**: The UI component that displays the solver result (assignments table) within the sandbox
- **Draft_Version**: A ScheduleVersion with status Draft, created by the solver and awaiting admin review before publishing
- **Admin**: A user with group owner or space owner permissions who can access the simulation sandbox
- **Publish_Flow**: The process of persisting all sandbox changes to the database and publishing the draft version
- **Discard_Flow**: The process of discarding the draft version and abandoning all sandbox changes without persisting anything
- **Override_Payload**: A modified SolverInputDto constructed from Sandbox_State, sent directly to the solver bypassing the normalizer's database queries
- **Home_Leave_Preview**: The section of the Schedule_Preview that shows home-leave assignments when the group has home-leave enabled

## Requirements

### Requirement 1: Sandbox Entry

**User Story:** As an Admin, I want to enter a simulation sandbox after a draft schedule is created, so that I can experiment with different scheduling parameters before committing.

#### Acceptance Criteria

1. WHEN a Draft_Version exists for the group, THE Simulation_Sandbox SHALL display an "Enter Simulation" action in the draft review panel
2. WHEN the Admin activates the simulation action, THE Simulation_Sandbox SHALL initialize Sandbox_State with the current group parameters (tasks, constraints, members, settings, qualifications) loaded from the existing draft's source data
3. THE Simulation_Sandbox SHALL display a settings panel alongside the Schedule_Preview, allowing the Admin to modify parameters without navigating away
4. IF no Draft_Version exists for the group, THEN THE Simulation_Sandbox SHALL hide the simulation entry action

### Requirement 2: Task Overrides

**User Story:** As an Admin, I want to add, edit, and remove tasks in the sandbox, so that I can see how task changes affect the schedule without modifying the real task list.

#### Acceptance Criteria

1. WHILE the Simulation_Sandbox is active, THE Simulation_Sandbox SHALL allow the Admin to add new tasks to the Sandbox_State with name, time window, headcount, burden level, required roles, and required qualifications
2. WHILE the Simulation_Sandbox is active, THE Simulation_Sandbox SHALL allow the Admin to edit existing tasks in the Sandbox_State (modify any task field)
3. WHILE the Simulation_Sandbox is active, THE Simulation_Sandbox SHALL allow the Admin to remove tasks from the Sandbox_State
4. THE Simulation_Sandbox SHALL visually distinguish overridden tasks (added, modified, or removed) from unmodified tasks using color coding or badges
5. THE Simulation_Sandbox SHALL NOT persist task changes to the database until the Publish_Flow executes

### Requirement 3: Constraint Overrides

**User Story:** As an Admin, I want to add, edit, and remove constraints in the sandbox, so that I can test how different constraint configurations affect the schedule.

#### Acceptance Criteria

1. WHILE the Simulation_Sandbox is active, THE Simulation_Sandbox SHALL allow the Admin to add new hard or soft constraints to the Sandbox_State with rule type, scope, and payload
2. WHILE the Simulation_Sandbox is active, THE Simulation_Sandbox SHALL allow the Admin to edit existing constraints in the Sandbox_State
3. WHILE the Simulation_Sandbox is active, THE Simulation_Sandbox SHALL allow the Admin to remove constraints from the Sandbox_State
4. THE Simulation_Sandbox SHALL visually distinguish overridden constraints from unmodified constraints
5. THE Simulation_Sandbox SHALL NOT persist constraint changes to the database until the Publish_Flow executes

### Requirement 4: Member Overrides

**User Story:** As an Admin, I want to include or exclude specific members from the schedule in the sandbox, so that I can see how personnel changes affect coverage.

#### Acceptance Criteria

1. WHILE the Simulation_Sandbox is active, THE Simulation_Sandbox SHALL display a member list with toggle controls to include or exclude each member from the Simulation_Run
2. WHEN the Admin excludes a member, THE Simulation_Sandbox SHALL remove that member from the Override_Payload's People list
3. WHEN the Admin re-includes a previously excluded member, THE Simulation_Sandbox SHALL restore that member to the Override_Payload's People list with their original eligibility data
4. THE Simulation_Sandbox SHALL display the count of active members versus total members
5. THE Simulation_Sandbox SHALL NOT persist member exclusions to the database until the Publish_Flow executes

### Requirement 5: Settings Overrides

**User Story:** As an Admin, I want to modify scheduling settings (rest time, minimum people at base, home-leave parameters, qualifications) in the sandbox, so that I can test different configurations.

#### Acceptance Criteria

1. WHILE the Simulation_Sandbox is active, THE Simulation_Sandbox SHALL allow the Admin to modify the minimum rest hours between shifts (0–24 hours)
2. WHILE the Simulation_Sandbox is active AND the group has home-leave enabled, THE Simulation_Sandbox SHALL allow the Admin to modify home-leave parameters: eligibility threshold hours, leave duration hours, leave capacity, and balance value
3. WHILE the Simulation_Sandbox is active AND the group is a closed base, THE Simulation_Sandbox SHALL allow the Admin to modify the minimum people at base (leave capacity)
4. WHILE the Simulation_Sandbox is active, THE Simulation_Sandbox SHALL allow the Admin to modify qualification requirements for task slots
5. THE Simulation_Sandbox SHALL validate setting values within their allowed ranges before including them in the Override_Payload
6. THE Simulation_Sandbox SHALL NOT persist settings changes to the database until the Publish_Flow executes

### Requirement 6: Simulation Run Execution

**User Story:** As an Admin, I want to re-run the solver with my sandbox modifications, so that I can preview the resulting schedule.

#### Acceptance Criteria

1. WHEN the Admin triggers a Simulation_Run, THE Simulation_Sandbox SHALL construct an Override_Payload from the current Sandbox_State
2. THE Simulation_Sandbox SHALL send the Override_Payload to a dedicated simulation endpoint that accepts a complete SolverInputDto directly, bypassing the SolverPayloadNormalizer's database queries for overridden fields
3. WHEN the Simulation_Run is processing, THE Simulation_Sandbox SHALL display a loading indicator in the Schedule_Preview area
4. WHEN the Simulation_Run completes, THE Schedule_Preview SHALL update to display the new solver results (assignments)
5. IF the Simulation_Run fails (solver timeout or infeasibility), THEN THE Simulation_Sandbox SHALL display a localized error message explaining the failure reason
6. THE Simulation_Sandbox SHALL allow the Admin to trigger multiple Simulation_Runs within the same session, each reflecting the latest Sandbox_State

### Requirement 7: Non-Destructive State Management

**User Story:** As an Admin, I want sandbox changes to exist only in memory until I explicitly publish, so that I can experiment freely without risk.

#### Acceptance Criteria

1. THE Simulation_Sandbox SHALL store all parameter overrides exclusively in frontend state (component state or client-side store) during the simulation session
2. THE Simulation_Sandbox SHALL NOT write Sandbox_State to the database at any point before the Publish_Flow or Discard_Flow
3. WHEN the Admin navigates away from the sandbox without publishing or discarding, THE Simulation_Sandbox SHALL warn the Admin that unsaved simulation changes will be lost
4. WHEN the browser tab is closed or refreshed during an active simulation session, THE Simulation_Sandbox SHALL discard all Sandbox_State without persisting changes
5. THE Simulation_Run endpoint SHALL NOT create or modify any database records — solver results from simulation runs are returned in the response body only

### Requirement 8: Reactive UI Behavior

**User Story:** As an Admin, I want only the schedule preview to update when I change parameters, so that the settings panel remains stable and I do not lose my place.

#### Acceptance Criteria

1. WHEN the Admin modifies parameters in the settings panel, THE Simulation_Sandbox SHALL NOT re-render the settings panel or reset scroll position
2. WHEN a Simulation_Run completes, THE Schedule_Preview SHALL update independently of the settings panel
3. THE Simulation_Sandbox SHALL maintain separate rendering boundaries between the settings panel and the Schedule_Preview
4. WHEN a Simulation_Run is in progress, THE Simulation_Sandbox SHALL keep the settings panel interactive (the Admin can continue editing parameters for the next run)

### Requirement 9: Publish Flow

**User Story:** As an Admin, I want publishing the draft to persist all my sandbox changes to the database, so that the final configuration matches what I previewed.

#### Acceptance Criteria

1. WHEN the Admin publishes the Draft_Version from the sandbox, THE Publish_Flow SHALL persist all new or modified tasks from Sandbox_State to the tasks table
2. WHEN the Admin publishes the Draft_Version from the sandbox, THE Publish_Flow SHALL persist all new or modified constraints from Sandbox_State to the constraints table
3. WHEN the Admin publishes the Draft_Version from the sandbox, THE Publish_Flow SHALL persist member exclusions from Sandbox_State (opt-out records)
4. WHEN the Admin publishes the Draft_Version from the sandbox, THE Publish_Flow SHALL persist modified settings (rest hours, home-leave parameters) to the group record
5. WHEN the Admin publishes the Draft_Version from the sandbox, THE Publish_Flow SHALL publish the schedule version using the existing PublishVersionCommand
6. THE Publish_Flow SHALL execute all database writes within a single transaction — if any write fails, the entire publish is rolled back
7. THE Publish_Flow SHALL produce an audit log entry recording the sandbox changes applied during publish

### Requirement 10: Discard Flow

**User Story:** As an Admin, I want discarding the draft to throw away all sandbox changes, so that nothing persists from an abandoned simulation.

#### Acceptance Criteria

1. WHEN the Admin discards the Draft_Version from the sandbox, THE Discard_Flow SHALL discard the schedule version using the existing discard mechanism
2. WHEN the Admin discards the Draft_Version from the sandbox, THE Discard_Flow SHALL clear all Sandbox_State from frontend memory
3. THE Discard_Flow SHALL NOT persist any sandbox parameter changes to the database
4. WHEN the Discard_Flow completes, THE Simulation_Sandbox SHALL close and return the Admin to the group's schedule view

### Requirement 11: Access Control

**User Story:** As a system, I want only group owners and space owners to access the simulation sandbox, so that unauthorized users cannot modify scheduling parameters.

#### Acceptance Criteria

1. THE Simulation_Sandbox SHALL require the requesting user to hold group owner or space owner permissions for the target group
2. IF a user without group owner or space owner permissions attempts to access the sandbox, THEN THE Simulation_Sandbox SHALL deny access and return a 403 status
3. THE Simulation_Run endpoint SHALL verify group owner or space owner permissions before executing the solver
4. THE Publish_Flow SHALL verify group owner or space owner permissions before persisting changes

### Requirement 12: Home-Leave Preview

**User Story:** As an Admin, I want the simulation to show home-leave assignment previews when the group has home-leave enabled, so that I can see the full scheduling picture.

#### Acceptance Criteria

1. WHILE the group has home-leave enabled, THE Schedule_Preview SHALL display home-leave assignments alongside task assignments in the Simulation_Run results
2. WHEN the Admin modifies home-leave parameters in the sandbox, THE next Simulation_Run SHALL reflect the updated home-leave configuration in the Home_Leave_Preview
3. THE Home_Leave_Preview SHALL display which members are assigned home-leave and during which time windows
4. IF the group does not have home-leave enabled, THEN THE Schedule_Preview SHALL omit the Home_Leave_Preview section

