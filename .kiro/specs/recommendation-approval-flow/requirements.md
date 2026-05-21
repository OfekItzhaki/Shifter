# Requirements Document

## Introduction

This feature addresses two related needs in the Shifter/Jobuler scheduling application:

1. **Recommendation as Informational Suggestion** — The current `AcceptRecommendationCommand` silently enables `AllowsDoubleShift` on a task. This is replaced with a passive, informational recommendation displayed above the emergency freeze section. The recommendation reminds the admin that enabling double shift is an option and links to the Tasks tab where the admin can manually toggle the setting.

2. **Task Info Badges in Schedule View** — Admins lack visibility into task configuration when viewing the schedule grid. An info icon next to each task name in the schedule view will show a popover with the task's special settings (double shift, overlap, time window, burden, qualifications, split count), helping admins understand solver decisions.

## Glossary

- **Admin**: A user with `TasksManage` permission in the current space
- **Recommendation_Engine**: The `RecommendationEngine` service that analyzes solver output and produces double-shift recommendations
- **Recommendation_Card**: An informational UI card displayed above the emergency freeze section, showing the recommendation with a link to the Tasks tab
- **Task_Info_Badge**: An "ℹ" icon displayed next to each task name in the schedule grid that reveals task configuration on interaction
- **Task_Info_Popover**: A tooltip or popover component showing task configuration details
- **Schedule_Grid**: The 2D table view (`ScheduleTable2D`) displaying assignments by time slot and task
- **GroupTask**: The domain entity representing a recurring scheduling task with configuration properties
- **Double_Shift**: A configuration allowing a person to be assigned to two consecutive shifts on the same task
- **Tasks_Tab**: The task management tab where admins can edit task settings including the double shift toggle

## Requirements

### Requirement 1: Remove Auto-Enable from Accept Flow

**User Story:** As an admin, I want the recommendation acceptance to NOT automatically enable double shift, so that I maintain explicit control over task configuration changes.

#### Acceptance Criteria

1. WHEN an admin interacts with a double-shift recommendation, THE system SHALL NOT automatically modify the `AllowsDoubleShift` property of the GroupTask
2. THE `AcceptRecommendationCommand` SHALL be removed or refactored to no longer call `task.EnableDoubleShift()`
3. WHEN a recommendation is dismissed, THE system SHALL mark the recommendation as Dismissed without modifying the GroupTask

### Requirement 2: Informational Recommendation Card

**User Story:** As an admin, I want to see a recommendation suggestion above the emergency freeze section, so that I am reminded that enabling double shift is an option when there are uncovered slots.

#### Acceptance Criteria

1. WHEN active double-shift recommendations exist for the group, THE Recommendation_Card SHALL be displayed above the emergency freeze section in the group page
2. THE Recommendation_Card SHALL display the task name(s) and the number of additional slots that could be covered by enabling double shift
3. THE Recommendation_Card SHALL include a localized link or button labeled "Go to Tasks" (or equivalent) that navigates the admin to the Tasks_Tab
4. WHEN the admin clicks "Dismiss" on the Recommendation_Card, THE system SHALL mark the recommendation as Dismissed and hide the card
5. IF no active recommendations exist for the group, THEN THE Recommendation_Card SHALL not be rendered

### Requirement 3: Navigation to Tasks Tab

**User Story:** As an admin, I want the recommendation to link directly to the Tasks tab, so that I can quickly find and toggle the double shift setting myself.

#### Acceptance Criteria

1. WHEN the admin clicks the "Go to Tasks" link on the Recommendation_Card, THE system SHALL navigate to the Tasks_Tab within the same group page
2. THE Tasks_Tab SHALL highlight or scroll to the relevant task referenced by the recommendation (if feasible)
3. THE admin SHALL be able to enable double shift via the existing checkbox in the task edit form

### Requirement 4: Backend Simplification

**User Story:** As a developer, I want the recommendation accept endpoint to be simplified to only dismiss/acknowledge recommendations without modifying task state, so that the API is consistent with the passive recommendation approach.

#### Acceptance Criteria

1. THE accept endpoint (`POST /spaces/{spaceId}/recommendations/{id}/accept`) SHALL be removed or repurposed as a dismiss-only action
2. THE dismiss endpoint SHALL mark the recommendation as Dismissed without modifying the GroupTask
3. THE Recommendation_Engine SHALL continue to generate recommendations based on solver output (no changes to detection logic)
4. THE existing task update endpoint SHALL remain the only way to enable double shift on a GroupTask

### Requirement 5: Task Info Badge Display

**User Story:** As an admin, I want to see an info icon next to each task name in the schedule grid, so that I can quickly check task configuration without navigating away.

#### Acceptance Criteria

1. THE Schedule_Grid SHALL display a Task_Info_Badge (ℹ icon) adjacent to each task name in the column header
2. THE Task_Info_Badge SHALL be visually subtle (small, muted color) so it does not distract from the schedule data
3. THE Task_Info_Badge SHALL be accessible with an `aria-label` describing its purpose (e.g., "Task configuration info")

### Requirement 6: Task Info Popover Content

**User Story:** As an admin, I want the task info popover to show all relevant task configuration, so that I understand why the solver made certain assignment decisions.

#### Acceptance Criteria

1. WHEN the admin clicks or hovers over the Task_Info_Badge, THE Task_Info_Popover SHALL appear showing the following GroupTask properties:
   - Whether double shift is enabled (AllowsDoubleShift)
   - Whether overlap is allowed (AllowsOverlap)
   - Daily time window (DailyStartTime – DailyEndTime), or "24/7" if not set
   - Burden level (BurdenLevel)
   - Required qualifications (QualificationRequirements where Mandatory is true)
   - Split count (SplitCount), displayed only when greater than 1
2. THE Task_Info_Popover SHALL display labels and values using localized strings (via next-intl)
3. WHEN the admin clicks outside the Task_Info_Popover or moves the cursor away, THE Task_Info_Popover SHALL close
4. IF a GroupTask has no special configuration (all defaults), THEN THE Task_Info_Popover SHALL display a message indicating default settings are in use

### Requirement 7: Task Info Data Fetching

**User Story:** As a developer, I want task configuration data to be available in the schedule view without additional API calls, so that the popover loads instantly.

#### Acceptance Criteria

1. THE Schedule_Grid SHALL receive task configuration data as part of the existing schedule data fetch (no separate API call for popover content)
2. WHEN the schedule data is loaded, THE frontend SHALL have access to AllowsDoubleShift, AllowsOverlap, DailyStartTime, DailyEndTime, BurdenLevel, QualificationRequirements, and SplitCount for each task displayed in the grid
3. IF task configuration data is unavailable for a task, THEN THE Task_Info_Badge SHALL be hidden for that task
