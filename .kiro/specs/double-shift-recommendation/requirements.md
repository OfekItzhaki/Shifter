# Requirements Document

## Introduction

When a group is short-staffed — meaning home leave or other absences reduce available personnel below the configured `minPeopleAtBase` threshold — the system currently has no way to proactively suggest that enabling double shifts on certain tasks could resolve the coverage gap. This feature introduces a contextual recommendation mechanism that detects staffing shortfalls after a solver run and surfaces actionable suggestions to the admin, recommending which tasks could benefit from enabling `AllowsDoubleShift`.

## Glossary

- **Solver**: The CP-SAT scheduling engine that produces draft schedule assignments from a `SolverInput` payload
- **Staffing_Shortfall**: A condition where the number of available personnel at base during a scheduling horizon falls below the group's configured `minPeopleAtBase` threshold
- **Double_Shift**: A configuration on a task (`AllowsDoubleShift`) that permits the solver to assign a single person to two consecutive shifts on that task
- **Recommendation_Engine**: The component that analyzes solver results and staffing data to produce double-shift recommendations
- **Admin**: A user with group management permissions who can modify task settings
- **Uncovered_Slot**: A task slot in the solver output where fewer people were assigned than the `required_headcount`
- **Min_People_At_Base**: The configured minimum number of personnel that must remain at base at any time, set in the HomeLeaveConfig

## Requirements

### Requirement 1: Detect Staffing Shortfall

**User Story:** As an admin, I want the system to automatically detect when my group is short-staffed, so that I am aware of coverage gaps without manually checking.

#### Acceptance Criteria

1. WHEN the Solver completes a run and the solver output contains at least one uncovered slot (UncoveredSlotIds is non-empty), THE Recommendation_Engine SHALL calculate whether enabling double shifts on specific tasks would reduce the number of uncovered slots
2. WHEN the number of available personnel at base during any day in the scheduling horizon falls below Min_People_At_Base, THE Recommendation_Engine SHALL flag that day as having a Staffing_Shortfall, where available personnel at base equals total group members minus those assigned to home leave on that day according to the solver output
3. THE Recommendation_Engine SHALL only evaluate active tasks where `AllowsDoubleShift` is currently set to false
4. WHEN the Recommendation_Engine evaluates a candidate task, THE Recommendation_Engine SHALL simulate the effect by calculating how many currently-uncovered slots for that task could be filled if a single person were permitted to serve consecutive shifts, and SHALL consider the task beneficial only if at least one additional slot would be covered

### Requirement 2: Generate Double-Shift Recommendations

**User Story:** As an admin, I want the system to recommend specific tasks where enabling double shifts could help cover staffing gaps, so that I can make informed decisions without trial-and-error.

#### Acceptance Criteria

1. WHEN a Staffing_Shortfall is detected, THE Recommendation_Engine SHALL produce a list of task names where enabling Double_Shift would reduce the number of uncovered slots by at least 1
2. THE Recommendation_Engine SHALL rank recommended tasks in descending order by the number of additional slots that would be covered if Double_Shift were enabled on each task, using task name alphabetical order as a tiebreaker when two tasks would cover the same number of additional slots
3. THE Recommendation_Engine SHALL include in each recommendation the task name, the number of uncovered slots it would address, and the affected date range expressed as the earliest and latest dates containing uncovered slots for that task
4. IF no tasks exist where enabling Double_Shift would reduce uncovered slots, THEN THE Recommendation_Engine SHALL return an empty recommendation list for that solver run
5. THE Recommendation_Engine SHALL include at most 10 tasks in a single recommendation list, selecting the highest-ranked tasks when more than 10 are eligible

### Requirement 3: Surface Recommendation to Admin

**User Story:** As an admin, I want to see double-shift recommendations in context after a solver run, so that I can act on them immediately.

#### Acceptance Criteria

1. WHEN the Recommendation_Engine produces recommendations, THE System SHALL create a Notification for each Admin in the group with event type `double_shift_recommendation`, including the solver run identifier and the total number of uncovered slots in the notification metadata
2. WHEN the Admin views the solver results page and recommendations exist for that run, THE System SHALL display an inline banner containing: the total number of uncovered slots, the recommended task names (up to 5, with a count of remaining tasks if more exist), the affected date range, and a button that navigates the Admin to the group task settings page
3. WHEN the Admin views the group task settings and a recommendation exists for a specific task, THE System SHALL display an inline suggestion next to that task's Double_Shift toggle showing the number of uncovered slots that enabling double shifts on that task would address and the affected date range
4. IF the Recommendation_Engine produces recommendations but no Admins exist in the group, THEN THE System SHALL skip notification creation without producing an error

### Requirement 4: Admin Actions on Recommendation

**User Story:** As an admin, I want to accept or dismiss double-shift recommendations, so that I maintain control over task configuration.

#### Acceptance Criteria

1. WHEN the Admin clicks the enable action on a recommendation, THE System SHALL set `AllowsDoubleShift` to true on the specified task and display a success confirmation indicating the task name and updated setting
2. WHEN the Admin enables Double_Shift via a recommendation, THE System SHALL display a confirmation prompt asking whether to trigger a new solver run with the updated configuration; IF the Admin confirms, THEN THE System SHALL enqueue a new solver run for the group; IF the Admin declines, THEN THE System SHALL close the prompt without triggering a solver run
3. WHEN the Admin dismisses a recommendation, THE System SHALL mark the recommendation as dismissed and stop displaying it for that solver run
4. IF the Admin dismisses a recommendation, THEN THE System SHALL not display a recommendation for the same task and solver run combination again until a new solver run produces a new Staffing_Shortfall
5. IF the Admin clicks the enable action on a recommendation but `AllowsDoubleShift` is already true on the specified task, THEN THE System SHALL display an informational message indicating the task already has double shifts enabled and mark the recommendation as resolved

### Requirement 5: Recommendation Lifecycle

**User Story:** As an admin, I want recommendations to stay relevant and not clutter my interface with stale suggestions, so that I only see actionable information.

#### Acceptance Criteria

1. WHEN a new solver run completes without a Staffing_Shortfall, THE System SHALL transition all active recommendations for that group to status "cleared" within the same operation that records the solver run result
2. WHEN the Admin manually enables Double_Shift on a task that has one or more active recommendations, THE System SHALL mark all active recommendations referencing that task as resolved
3. THE System SHALL retain dismissed, resolved, and cleared recommendations in the database for a minimum of 90 days for audit purposes and SHALL not display them in the active recommendations UI
4. WHEN a new solver run produces a Staffing_Shortfall, THE Recommendation_Engine SHALL generate fresh recommendations regardless of previously dismissed recommendations
5. THE System SHALL consider a recommendation "active" only if its status is neither dismissed, resolved, nor cleared

### Requirement 6: Contextual Display Conditions

**User Story:** As an admin, I want recommendations to appear only when relevant, so that I am not distracted by unnecessary suggestions.

#### Acceptance Criteria

1. THE System SHALL display double-shift recommendations only to users whose group role has a PermissionLevel of ViewAndEdit or Owner
2. THE System SHALL not display recommendations for tasks where `AllowsDoubleShift` is already true
3. WHILE `EmergencyFreezeActive` is true on the group's HomeLeaveConfig, THE System SHALL suppress all double-shift recommendations, including hiding any previously displayed active recommendations for that group
4. THE System SHALL not generate recommendations when the group has fewer than 2 tasks with `AllowsDoubleShift` set to false
5. WHEN a user's group PermissionLevel changes to View or their role is removed, THE System SHALL stop displaying double-shift recommendations to that user on their next page load
