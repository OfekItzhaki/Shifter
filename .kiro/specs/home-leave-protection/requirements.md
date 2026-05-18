# Requirements Document

## Introduction

Home Leave Protection ensures that approved home leave is treated as a high-priority, stable commitment within the Shifter scheduling system. Currently, publishing a new schedule can silently revoke home leave by overwriting AtHome presence windows or assigning tasks to people who are physically away. This feature introduces solver-level exclusion, publish-time protection, and a controlled emergency recall flow with mandatory notifications, so that home leave can only be revoked through an explicit admin action — never automatically.

## Glossary

- **Solver**: The Python CP-SAT constraint solver that generates task assignment schedules
- **SolverPayloadNormalizer**: The C# service that builds the solver input payload (people, slots, constraints, presence windows)
- **PresenceWindow**: A time-bounded record indicating where a person physically is (AtHome, OnMission, FreeInBase)
- **AtHome_Window**: A PresenceWindow with State = AtHome, representing an approved home leave period
- **PublishVersionCommand**: The command handler that publishes a draft schedule version, creating assignments and derived presence windows
- **CancelHomeLeaveCommand**: The existing command that truncates or deletes an AtHome presence window for a specific person
- **HomeLeaveConfig**: The group-level configuration entity controlling home leave scheduling parameters
- **Emergency_Freeze**: A mode where the HomeLeaveConfig.EmergencyFreezeActive flag is true, indicating a crisis situation
- **Live_Status**: The real-time view showing each group member's current physical location (at_home, on_mission, free_in_base)
- **Recall_Notification**: A multi-channel notification (push + email) sent to a person being recalled from home leave
- **Audit_Log**: The append-only record of administrative actions for accountability and traceability
- **Admin**: A user with the SchedulePublish permission in the relevant space

## Requirements

### Requirement 1: Solver Exclusion of People on Home Leave

**User Story:** As a schedule administrator, I want people with active or future home leave to be automatically excluded from the solver's available pool, so that the solver never assigns tasks to people who are physically away or have approved leave.

#### Acceptance Criteria

1. WHEN the SolverPayloadNormalizer builds a solver input payload, THE SolverPayloadNormalizer SHALL exclude from the people list any person who has an active AtHome_Window (StartsAt <= now AND EndsAt > now) that overlaps with the solver horizon
2. WHEN the SolverPayloadNormalizer builds a solver input payload, THE SolverPayloadNormalizer SHALL exclude from the people list any person who has a future AtHome_Window (StartsAt > now) that overlaps with the solver horizon
3. WHILE Emergency_Freeze is active AND EmergencyUseForScheduling is true, THE SolverPayloadNormalizer SHALL include all people in the solver payload regardless of their AtHome_Window status
4. THE SolverPayloadNormalizer SHALL treat excluded people as if they do not exist for the purpose of task assignment — no slots SHALL reference excluded person IDs

### Requirement 2: Publish-Time Protection of Existing Home Leave Windows

**User Story:** As a person with approved home leave, I want my AtHome presence windows to be preserved when a new schedule is published, so that my leave is never silently revoked by an automated process.

#### Acceptance Criteria

1. WHEN the PublishVersionCommand creates new derived AtHome presence windows, THE PublishVersionCommand SHALL NOT delete or overwrite any existing AtHome_Window that was NOT produced by the current publish operation
2. WHEN the PublishVersionCommand removes stale derived AtHome windows, THE PublishVersionCommand SHALL only remove derived AtHome windows belonging to people who appear in the new version's home_leave_assignments list
3. THE PublishVersionCommand SHALL preserve all manually-created AtHome windows (IsDerived = false) regardless of the new schedule content
4. WHEN a new schedule version is published, THE PublishVersionCommand SHALL preserve all existing AtHome windows for people who are NOT included in the new version's home_leave_assignments

### Requirement 3: Emergency Recall as Explicit Admin Action

**User Story:** As a schedule administrator, I want to recall a person from home leave only through an explicit action with mandatory notification, so that home leave is never revoked silently and the recalled person is informed immediately.

#### Acceptance Criteria

1. WHEN an Admin initiates a recall of a person with an active AtHome_Window, THE System SHALL require the Admin to confirm the action through a confirmation step
2. WHEN an Admin initiates a recall, THE System SHALL display a warning indicating that the person is currently at home and needs travel time to return
3. WHILE Emergency_Freeze is NOT active, THE System SHALL require an explicit admin override confirmation before allowing a recall of active home leave
4. THE CancelHomeLeaveCommand SHALL only execute when invoked by a user with the SchedulePublish permission
5. THE System SHALL NOT allow any automated process to cancel or truncate an active AtHome_Window without explicit admin invocation

### Requirement 4: Recall Notification Delivery

**User Story:** As a person on home leave, I want to receive immediate notification when I am recalled, so that I know I need to return and understand the reason.

#### Acceptance Criteria

1. WHEN the CancelHomeLeaveCommand successfully truncates or deletes an AtHome_Window, THE System SHALL send a push notification to the recalled person within 5 seconds of the operation completing
2. WHEN the CancelHomeLeaveCommand successfully truncates or deletes an AtHome_Window, THE System SHALL send an email to the recalled person's registered email address
3. THE Recall_Notification SHALL include the name of the Admin who initiated the recall
4. THE Recall_Notification SHALL include the reason for recall if the Admin provided one
5. THE Recall_Notification SHALL include the expected return time if the Admin specified one
6. IF the push notification delivery fails, THEN THE System SHALL retry delivery up to 3 times with exponential backoff
7. IF the email delivery fails, THEN THE System SHALL log the failure and continue without blocking the recall operation

### Requirement 5: Recall Audit Logging

**User Story:** As a system administrator, I want every home leave recall to be recorded in the audit log, so that there is a complete accountability trail for all recall actions.

#### Acceptance Criteria

1. WHEN the CancelHomeLeaveCommand completes successfully, THE System SHALL create an Audit_Log entry with action "cancel_home_leave"
2. THE Audit_Log entry SHALL include: the Admin's user ID, the space ID, the recalled person's ID, the presence window ID, the operation type (deleted or truncated), and the timestamp
3. THE Audit_Log entry SHALL include the recall reason if one was provided by the Admin
4. THE Audit_Log entry SHALL include a before-snapshot containing the original AtHome_Window start and end times

### Requirement 6: Priority Hierarchy in Live Status

**User Story:** As a schedule viewer, I want the live status to always reflect the actual physical location of each person with correct priority ordering, so that I can trust the displayed status.

#### Acceptance Criteria

1. WHEN a person has both an active AtHome_Window and an active assignment, THE Live_Status SHALL display the person's status as "at_home"
2. WHEN a person has an active AtHome_Window and no active assignment, THE Live_Status SHALL display the person's status as "at_home"
3. WHEN a person has an active assignment and no AtHome_Window, THE Live_Status SHALL display the person's status as "on_mission"
4. WHEN a person has no active AtHome_Window and no active assignment, THE Live_Status SHALL display the person's status as "free_in_base"
5. THE Live_Status SHALL evaluate presence windows before assignment-based status, ensuring AtHome_Window takes precedence over OnMission derived from assignments

### Requirement 7: Home Leave Stability Guarantee

**User Story:** As a person with approved future home leave, I want my scheduled leave to remain unchanged when new schedules are generated, so that I can rely on my leave dates for personal planning.

#### Acceptance Criteria

1. WHEN the Solver generates a new schedule, THE Solver SHALL produce assignments that do not conflict with any existing AtHome_Window for any person
2. WHEN the SolverPayloadNormalizer builds the solver input, THE SolverPayloadNormalizer SHALL include existing AtHome windows in the presence windows list so the solver is aware of them as constraints
3. THE PublishVersionCommand SHALL NOT modify the StartsAt or EndsAt of any existing AtHome_Window that was not produced by the current publish operation
4. WHEN a scheduling conflict exists between a task assignment need and an existing AtHome_Window, THE Solver SHALL find an alternative assignment that does not affect the existing home leave

### Requirement 8: Recall Command Enhancement

**User Story:** As a schedule administrator, I want the recall command to accept a reason and expected return time, so that the recalled person receives complete information about why they are being recalled and when they should return.

#### Acceptance Criteria

1. THE CancelHomeLeaveCommand SHALL accept an optional Reason parameter (free-text string, maximum 500 characters)
2. THE CancelHomeLeaveCommand SHALL accept an optional ExpectedReturnAt parameter (DateTime)
3. WHEN a Reason is provided, THE System SHALL include the reason text in both the push notification and the email
4. WHEN an ExpectedReturnAt is provided, THE System SHALL include the formatted return time in both the push notification and the email
5. IF the Reason exceeds 500 characters, THEN THE System SHALL reject the command with a validation error
