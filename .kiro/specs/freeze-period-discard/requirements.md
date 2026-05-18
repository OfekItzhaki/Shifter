# Requirements Document

## Introduction

When an emergency freeze is deactivated, schedule changes (overrides, manual swaps, manual assignments) made during the freeze period are often temporary measures that should not persist. This feature gives the admin an explicit choice to discard all schedule modifications made between `freeze_started_at` and the deactivation timestamp, creating a clean rollback via the existing immutable versioning system.

## Glossary

- **Freeze_Deactivation_Flow**: The UI and API sequence triggered when an admin deactivates the emergency freeze on a group.
- **Freeze_Period**: The time window between `freeze_started_at` and the moment the admin deactivates the freeze.
- **Freeze_Period_Changes**: Schedule overrides, manual assignments, and manual swaps created during the Freeze_Period.
- **Discard_Option**: A user-facing toggle or checkbox presented during the Freeze_Deactivation_Flow that controls whether Freeze_Period_Changes are discarded.
- **Discard_Version**: A new immutable schedule version created by reverting all Freeze_Period_Changes, following the existing versioning pattern.
- **Deactivation_Dialog**: The confirmation dialog presented to the admin when deactivating the emergency freeze.
- **Schedule_Versioning_System**: The existing system that stores schedule snapshots as immutable versions, supporting rollback via new version creation.
- **Admin**: A user with `schedule.rollback` permission in the space.

## Requirements

### Requirement 1: Discard Option Presentation

**User Story:** As an admin, I want to see a discard option when deactivating the emergency freeze, so that I can choose whether temporary schedule changes persist.

#### Acceptance Criteria

1. WHEN the Admin initiates freeze deactivation, THE Deactivation_Dialog SHALL display the Discard_Option as a toggle or checkbox with a label stating that enabling it will revert all schedule changes made during the Freeze_Period.
2. THE Deactivation_Dialog SHALL default the Discard_Option to unchecked (keep changes).
3. THE Deactivation_Dialog SHALL display the count of Freeze_Period_Changes categorized by type (overrides, manual assignments, and swaps) before the Admin confirms.
4. IF no Freeze_Period_Changes exist for the Freeze_Period, THEN THE Deactivation_Dialog SHALL hide the Discard_Option and display a message indicating no changes were made during the freeze.
5. IF the retrieval of Freeze_Period_Changes count fails, THEN THE Deactivation_Dialog SHALL disable the Discard_Option and display an error message indicating that the change summary is unavailable.

### Requirement 2: Freeze Period Change Identification

**User Story:** As an admin, I want the system to identify all schedule changes made during the freeze period, so that I can make an informed decision about discarding them.

#### Acceptance Criteria

1. WHEN the Admin opens the Deactivation_Dialog, THE System SHALL query all assignments with Source = Override and CreatedAt between `freeze_started_at` and the current timestamp, scoped to the group's schedule versions within the space.
2. WHEN the Admin opens the Deactivation_Dialog, THE System SHALL query all manual assignments (solver-generated assignments replaced during the freeze) with CreatedAt between `freeze_started_at` and the current timestamp, scoped to the group's schedule versions within the space.
3. WHEN the Admin opens the Deactivation_Dialog, THE System SHALL query all manual swaps (paired reassignments where one person replaces another in a slot) with CreatedAt between `freeze_started_at` and the current timestamp, scoped to the group's schedule versions within the space.
4. THE System SHALL use the `freeze_started_at` column from `home_leave_configs` for the matching group as the lower bound for identifying Freeze_Period_Changes.
5. IF `freeze_started_at` is null for the group when the Admin opens the Deactivation_Dialog, THEN THE System SHALL treat the Freeze_Period_Changes count as zero and display an error message indicating the freeze timestamp is missing.
6. WHEN the System queries Freeze_Period_Changes, THE System SHALL return results within 3 seconds for groups with up to 10,000 assignments in the freeze period.

### Requirement 3: Discard Execution via Immutable Versioning

**User Story:** As an admin, I want discarding freeze-period changes to create a new schedule version, so that the change history remains intact and auditable.

#### Acceptance Criteria

1. WHEN the Admin confirms deactivation with the Discard_Option enabled, THE Schedule_Versioning_System SHALL create a new Discard_Version with the next sequential version number for the space, copying only assignments from the most recent version whose `published_at` is earlier than `freeze_started_at` and whose status is Published.
2. THE Schedule_Versioning_System SHALL mark the Discard_Version with a `rollback_source_version_id` referencing the pre-freeze published version identified in criterion 1.
3. THE Schedule_Versioning_System SHALL set the Discard_Version status to draft, requiring the Admin to publish it explicitly.
4. THE System SHALL record the discard action in the audit log with actor, space, group, number of discarded changes, and the new version ID.
5. IF no published version with `published_at` earlier than `freeze_started_at` exists for the space, THEN THE Schedule_Versioning_System SHALL reject the discard operation and return an error indicating that no pre-freeze baseline version is available.
6. WHEN the Discard_Version is created successfully, THE Schedule_Versioning_System SHALL perform the version creation and assignment copy as a single atomic operation, so that a partial failure does not leave orphaned versions or missing assignments.

### Requirement 4: Deactivation Without Discard

**User Story:** As an admin, I want to deactivate the freeze without discarding changes, so that temporary adjustments can be kept when they are still relevant.

#### Acceptance Criteria

1. WHEN the Admin confirms deactivation with the Discard_Option disabled, THE System SHALL clear the freeze state for the group (setting `freeze_started_at` to null) and restore normal scheduling mode without creating, modifying, or deleting any schedule versions.
2. WHEN the Admin confirms deactivation with the Discard_Option disabled, THE System SHALL preserve all Freeze_Period_Changes in the current published schedule version without modification.
3. WHEN the Admin confirms deactivation with the Discard_Option disabled, THE System SHALL record the deactivation in the audit log with actor, space, group, and a flag indicating no discard was performed.
4. WHEN deactivation without discard completes successfully, THE System SHALL return a success response confirming the freeze has been lifted and no schedule versions were altered.

### Requirement 5: Permission Enforcement

**User Story:** As a system administrator, I want the discard action to require appropriate permissions, so that only authorized users can revert schedule changes.

#### Acceptance Criteria

1. THE System SHALL require `schedule.rollback` permission to view or use the Discard_Option; standard deactivation (without discard) SHALL NOT require this permission.
2. IF the Admin lacks `schedule.rollback` permission, THEN THE Deactivation_Dialog SHALL hide the Discard_Option and allow only standard deactivation without indicating that a discard feature exists.
3. THE System SHALL verify `schedule.rollback` permission server-side via `IPermissionService` before executing the discard operation, regardless of the client-side UI state.
4. IF the server-side permission check fails for a discard request, THEN THE System SHALL reject the request with a 403 response and record the denied attempt in the audit log with actor, space, group, and action attempted.

### Requirement 6: API Endpoint for Freeze Deactivation with Discard

**User Story:** As a frontend developer, I want a clear API contract for deactivating the freeze with an optional discard flag, so that the UI can communicate the admin's choice to the backend.

#### Acceptance Criteria

1. THE System SHALL accept a `discardFreezeChanges` boolean parameter in the freeze deactivation API request, defaulting to false when the parameter is absent.
2. WHEN `discardFreezeChanges` is true, THE System SHALL execute the discard flow and return a response containing the new draft version ID and the count of discarded changes.
3. WHEN `discardFreezeChanges` is false or absent, THE System SHALL execute standard deactivation without modifying schedule versions and return the updated home-leave configuration in the response.
4. IF `discardFreezeChanges` is true and the caller lacks `schedule.rollback` permission, THEN THE System SHALL return a 403 Forbidden response with an error message indicating insufficient permissions, without performing the deactivation.
5. IF the freeze deactivation API is called and the emergency freeze is not currently active for the group, THEN THE System SHALL return a 400 Bad Request response with an error message indicating that no active freeze exists.
6. IF `discardFreezeChanges` is true and no Freeze_Period_Changes exist, THEN THE System SHALL complete the deactivation without creating a Discard_Version and return a response indicating zero changes were discarded.

### Requirement 7: Change Count Preview

**User Story:** As an admin, I want to see how many changes would be discarded before confirming, so that I understand the impact of my decision.

#### Acceptance Criteria

1. THE System SHALL provide an API endpoint that returns the count of Freeze_Period_Changes for a group currently under freeze, requiring at minimum the caller to be authenticated and a member of the space.
2. THE System SHALL categorize the count by type: overrides, manual assignments, and swaps, returning an integer count of zero or greater for each category.
3. IF the freeze is not active for the requested group, THEN THE System SHALL return zero counts for all categories.
4. IF the requested group does not exist or the caller lacks access to the space, THEN THE System SHALL return the appropriate error response (404 for non-existent group, 403 for unauthorized access).
