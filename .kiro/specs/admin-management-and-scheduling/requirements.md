# Requirements Document

## Introduction

This feature extends the Jobuler scheduling app with two coordinated parts:

**Part 1 — Admin CRUD Capabilities**: Admins can currently create constraints and tasks, but cannot edit or delete them. This part adds full lifecycle management for constraints and the new unified `Task` entity, plus admin-level edit/delete/pin for group messages and edit/delete for group alerts.

**Part 2 — Scheduling Activation UI**: The solver backend already exists and can be triggered via `POST /spaces/{spaceId}/schedule-runs/trigger`, but there is no UI to invoke it. This part adds a "הפעל סידור" (Activate Scheduling) section to the הגדרות tab, a polling state while the solver runs, automatic display of the resulting draft in the סידור tab, and admin controls to publish or discard the draft.

**Task Model Redesign**: The previous two-level model (`TaskType` + `TaskSlot`) is replaced with a single flat `Task` entity scoped to a group. The new `tasks` table is introduced via migration 014. The legacy `task_types` and `task_slots` tables are retained for backward compatibility but are no longer the primary model for new functionality.

---

## Glossary

- **System**: The Jobuler backend (ASP.NET Core API + Application + Domain + Infrastructure layers).
- **UI**: The Jobuler Next.js frontend.
- **GroupDetailPage**: The Next.js page at `/groups/[groupId]`.
- **Group_Admin**: A user whose `adminGroupId` in AuthStore equals the current `groupId` (frontend) / a user with the `people.manage` permission scoped to the group's space (backend).
- **IPermissionService**: The Application-layer interface used for all permission checks.
- **AuthStore**: The Zustand store holding authentication state including `adminGroupId`.
- **SpaceStore**: The Zustand store holding `currentSpaceId`.
- **apiClient**: The authenticated HTTP client used by the Next.js frontend.
- **ConstraintRule**: The domain entity representing a scheduling constraint. Has `ScopeType`, `ScopeId`, `Severity`, `RuleType`, `RulePayloadJson`, `EffectiveFrom`, `EffectiveUntil`, and `IsActive` fields.
- **Task**: The new unified domain entity representing a schedulable work window scoped to a group. Replaces the `TaskType` + `TaskSlot` two-level model. Fields: `id` (UUID PK), `space_id` (UUID FK), `group_id` (UUID FK), `name` (TEXT NOT NULL), `starts_at` (TIMESTAMPTZ NOT NULL), `ends_at` (TIMESTAMPTZ NOT NULL), `duration_hours` (DECIMAL NOT NULL), `required_headcount` (INT NOT NULL DEFAULT 1), `burden_level` (VARCHAR(20) NOT NULL DEFAULT 'neutral'), `allows_double_shift` (BOOLEAN NOT NULL DEFAULT false), `allows_overlap` (BOOLEAN NOT NULL DEFAULT false), `is_active` (BOOLEAN NOT NULL DEFAULT true), `created_by_user_id` (UUID FK), `created_at` (TIMESTAMPTZ), `updated_at` (TIMESTAMPTZ).
- **TaskType**: The legacy domain entity representing a category of work. Retained for backward compatibility — not used by new task management endpoints.
- **TaskSlot**: The legacy domain entity representing a scheduled instance of a TaskType. Retained for backward compatibility — not used by new task management endpoints.
- **GroupMessage**: The domain entity representing a message posted to a group. Has `Content`, `IsPinned`, and `AuthorUserId` fields.
- **GroupAlert**: The domain entity representing a broadcast alert posted by an admin to a group. Has `Title`, `Body`, `Severity`, and `CreatedByPersonId` fields.
- **ScheduleRun**: The domain entity tracking a solver execution. Has a `Status` of `Queued`, `Running`, `Completed`, `Failed`, or `TimedOut`.
- **Draft_Version**: A `ScheduleVersion` with status `Draft` produced by the solver after a completed `ScheduleRun`.
- **Solver_Horizon**: The number of days ahead the solver considers when generating schedules, stored as `SolverHorizonDays` on the group settings.
- **BurdenLevel**: An enumeration of task difficulty/desirability. Valid values: `favorable`, `neutral`, `disliked`, `hated`.
- **Permissions.PeopleManage**: The `people.manage` permission key — required for all admin write operations on groups, members, alerts, and messages.
- **Permissions.ConstraintsManage**: The `constraints.manage` permission key — required for constraint write operations.
- **Permissions.TasksManage**: The `tasks.manage` permission key — required for task write operations.
- **Permissions.SpaceView**: The `space.view` permission key — required to read task lists.
- **Permissions.ScheduleRecalculate**: The `schedule.recalculate` permission key — required to trigger the solver.
- **Permissions.SchedulePublish**: The `schedule.publish` permission key — required to publish or discard a draft version.

---

## Requirements

### Requirement 1: Create Task

**User Story:** As a group admin, I want to create a task with a name, time window, shift duration, headcount, burden level, and overlap settings, so that I can define the schedulable work units for my group.

#### Acceptance Criteria

1. THE System SHALL expose a `POST /spaces/{spaceId}/groups/{groupId}/tasks` endpoint requiring `[Authorize]` and the `tasks.manage` permission.
2. WHEN a valid create request is received, THE System SHALL insert a new `Task` record with all provided fields and return HTTP 201 with the new task's `id`.
3. THE System SHALL validate that `name` is between 1 and 200 non-blank characters; IF invalid, THEN THE System SHALL return HTTP 400.
4. THE System SHALL validate that `ends_at` is strictly after `starts_at`; IF invalid, THEN THE System SHALL return HTTP 400.
5. THE System SHALL validate that `duration_hours` is greater than 0; IF invalid, THEN THE System SHALL return HTTP 400.
6. THE System SHALL validate that `required_headcount` is at least 1; IF invalid, THEN THE System SHALL return HTTP 400.
7. THE System SHALL validate that `burden_level` is one of `favorable`, `neutral`, `disliked`, or `hated` (case-insensitive); IF invalid, THEN THE System SHALL return HTTP 400.
8. IF the requesting user does not hold `tasks.manage`, THEN THE System SHALL return HTTP 403.
9. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before inserting the entity.
10. WHEN the "משימות" tab is active and `adminGroupId` equals `groupId`, THE UI SHALL display a "הוסף משימה" (Add Task) button.
11. WHEN the admin clicks "הוסף משימה", THE UI SHALL open a create form with fields for: name (text input), starts_at (datetime picker), ends_at (datetime picker), duration_hours (number input), required_headcount (number input), burden_level (dropdown in Hebrew: נוח / ניטרלי / לא אהוב / שנוא), allows_double_shift (checkbox), and allows_overlap (checkbox).
12. WHEN the admin submits the create form, THE UI SHALL call `POST /spaces/{spaceId}/groups/{groupId}/tasks` and re-fetch the task list on success.
13. IF the create API call returns an error, THEN THE UI SHALL display the error message in Hebrew below the form.

---

### Requirement 2: Edit Task

**User Story:** As a group admin, I want to edit any field of an existing task, so that I can correct or update task definitions without deleting and recreating them.

#### Acceptance Criteria

1. THE System SHALL expose a `PUT /spaces/{spaceId}/groups/{groupId}/tasks/{taskId}` endpoint requiring `[Authorize]` and the `tasks.manage` permission.
2. WHEN a valid update request is received, THE System SHALL update all provided fields of the `Task` record and return HTTP 204.
3. THE System SHALL apply the same field-level validation rules as Requirement 1 (name length, ends_at after starts_at, duration_hours > 0, required_headcount ≥ 1, valid burden_level); IF any validation fails, THEN THE System SHALL return HTTP 400.
4. IF the `Task` is not found, is not active (`is_active = false`), or does not belong to the specified `spaceId` and `groupId`, THEN THE System SHALL return HTTP 404.
5. IF the requesting user does not hold `tasks.manage`, THEN THE System SHALL return HTTP 403.
6. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before updating the entity.
7. WHEN the "משימות" tab is active and `adminGroupId` equals `groupId`, THE UI SHALL display an edit button on each task row.
8. WHEN the admin clicks the edit button, THE UI SHALL open the task form pre-populated with the task's current values.
9. WHEN the admin submits the edit form, THE UI SHALL call `PUT /spaces/{spaceId}/groups/{groupId}/tasks/{taskId}` and re-fetch the task list on success.
10. IF the update API call returns an error, THEN THE UI SHALL display the error message in Hebrew below the form.

---

### Requirement 3: Delete Task

**User Story:** As a group admin, I want to delete a task, so that I can remove cancelled or obsolete work units from the group's schedule.

#### Acceptance Criteria

1. THE System SHALL expose a `DELETE /spaces/{spaceId}/groups/{groupId}/tasks/{taskId}` endpoint requiring `[Authorize]` and the `tasks.manage` permission.
2. WHEN a valid delete request is received, THE System SHALL soft-delete the `Task` by setting `is_active = false` and return HTTP 204.
3. IF the `Task` is not found or does not belong to the specified `spaceId` and `groupId`, THEN THE System SHALL return HTTP 404.
4. IF the requesting user does not hold `tasks.manage`, THEN THE System SHALL return HTTP 403.
5. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before deactivating the entity.
6. WHEN the "משימות" tab is active and `adminGroupId` equals `groupId`, THE UI SHALL display a delete button on each task row.
7. WHEN the admin clicks the delete button, THE UI SHALL display a confirmation dialog in Hebrew before proceeding.
8. WHEN the admin confirms deletion, THE UI SHALL call `DELETE /spaces/{spaceId}/groups/{groupId}/tasks/{taskId}` and remove the task from the list on success.
9. IF the delete API call returns an error, THEN THE UI SHALL display the error message in Hebrew.

---

### Requirement 4: List Tasks

**User Story:** As a group member, I want to view all active tasks for my group, so that I can see what work is scheduled and understand the upcoming assignments.

#### Acceptance Criteria

1. THE System SHALL expose a `GET /spaces/{spaceId}/groups/{groupId}/tasks` endpoint requiring `[Authorize]` and the `space.view` permission.
2. WHEN a valid list request is received, THE System SHALL return all `Task` records where `is_active = true` and `group_id` matches the specified `groupId`, ordered by `starts_at` ascending.
3. THE System SHALL include the following fields in each task response object: `id`, `name`, `starts_at`, `ends_at`, `duration_hours`, `required_headcount`, `burden_level`, `allows_double_shift`, `allows_overlap`, `created_at`, `updated_at`.
4. IF the requesting user does not hold `space.view`, THEN THE System SHALL return HTTP 403.
5. IF the `groupId` does not belong to the specified `spaceId`, THEN THE System SHALL return HTTP 404.
6. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before returning data.
7. WHEN the "משימות" tab is active, THE UI SHALL display a single unified list of tasks (no sub-tabs for types vs. slots).
8. THE UI SHALL display each task row with: name, time window (starts_at to ends_at formatted in Hebrew locale), duration_hours, required_headcount, and a burden level badge color-coded by severity (favorable = green, neutral = grey, disliked = orange, hated = red).

---

### Requirement 5: Edit Constraint

**User Story:** As a group admin, I want to edit an existing constraint's payload values and effective dates, so that I can correct or update scheduling rules without deleting and recreating them.

#### Acceptance Criteria

1. THE System SHALL expose a `PUT /spaces/{spaceId}/constraints/{constraintId}` endpoint requiring `[Authorize]` and the `constraints.manage` permission.
2. WHEN a valid update request is received, THE System SHALL update the `ConstraintRule`'s `RulePayloadJson`, `EffectiveFrom`, and `EffectiveUntil` fields and return HTTP 204.
3. IF the `ConstraintRule` is not found or does not belong to the specified `spaceId`, THEN THE System SHALL return HTTP 404.
4. IF the requesting user does not hold `constraints.manage`, THEN THE System SHALL return HTTP 403.
5. THE System SHALL validate that `RulePayloadJson` is valid JSON; IF invalid, THEN THE System SHALL return HTTP 400.
6. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before updating the entity.
7. WHEN the "אילוצים" tab is active and `adminGroupId` equals `groupId`, THE UI SHALL display an edit button on each constraint row.
8. WHEN the admin clicks the edit button, THE UI SHALL open the constraint form pre-populated with the constraint's current values.
9. WHEN the admin submits the edit form, THE UI SHALL call `PUT /spaces/{spaceId}/constraints/{constraintId}` and re-fetch the constraints list on success.
10. IF the update API call returns an error, THEN THE UI SHALL display the error message in Hebrew below the form.

---

### Requirement 6: Delete Constraint

**User Story:** As a group admin, I want to delete a constraint, so that I can remove scheduling rules that are no longer applicable.

#### Acceptance Criteria

1. THE System SHALL expose a `DELETE /spaces/{spaceId}/constraints/{constraintId}` endpoint requiring `[Authorize]` and the `constraints.manage` permission.
2. WHEN a valid delete request is received, THE System SHALL soft-delete the `ConstraintRule` by setting `IsActive = false` and return HTTP 204.
3. IF the `ConstraintRule` is not found or does not belong to the specified `spaceId`, THEN THE System SHALL return HTTP 404.
4. IF the requesting user does not hold `constraints.manage`, THEN THE System SHALL return HTTP 403.
5. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before deactivating the entity.
6. WHEN the "אילוצים" tab is active and `adminGroupId` equals `groupId`, THE UI SHALL display a delete button on each constraint row.
7. WHEN the admin clicks the delete button, THE UI SHALL display a confirmation dialog in Hebrew before proceeding.
8. WHEN the admin confirms deletion, THE UI SHALL call `DELETE /spaces/{spaceId}/constraints/{constraintId}` and remove the constraint from the list on success.
9. IF the delete API call returns an error, THEN THE UI SHALL display the error message in Hebrew.

---

### Requirement 7: Edit Constraint (Payload Validation)

**User Story:** As a group admin, I want the system to validate constraint payloads on edit, so that I cannot accidentally save a malformed rule that would break the solver.

#### Acceptance Criteria

1. WHEN a `PUT /spaces/{spaceId}/constraints/{constraintId}` request is received, THE System SHALL validate that `RulePayloadJson` is a well-formed JSON object.
2. IF `RulePayloadJson` is null, empty, or not valid JSON, THEN THE System SHALL return HTTP 400 with a descriptive error message.
3. THE System SHALL validate that `EffectiveUntil`, when provided, is on or after `EffectiveFrom`; IF invalid, THEN THE System SHALL return HTTP 400.
4. THE System SHALL perform all validation in the Application layer via FluentValidation before dispatching the update command.

---

### Requirement 8: Admin Delete Any Group Alert

**User Story:** As a group admin, I want to delete any alert in my group — not just alerts I created — so that I can moderate the alert history and remove outdated or incorrect information posted by any admin.

#### Acceptance Criteria

1. WHEN an authenticated user with `people.manage` permission calls `DELETE /spaces/{spaceId}/groups/{groupId}/alerts/{alertId}`, THE System SHALL delete the alert regardless of which admin created it and return HTTP 204.
2. IF the requesting user does not hold `people.manage`, THEN THE System SHALL return HTTP 403.
3. IF the alert is not found or does not belong to the specified `groupId` and `spaceId`, THEN THE System SHALL return HTTP 404.
4. THE System SHALL remove the ownership check (`alert.CreatedByPersonId == callerPerson.Id`) from `DeleteGroupAlertCommandHandler` so that any user with `people.manage` can delete any alert.
5. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before deleting the entity.
6. WHEN `adminGroupId` equals `groupId`, THE UI SHALL display a delete button on ALL alerts in the "התראות" tab, regardless of which admin created them.
7. WHEN the admin clicks the delete button, THE UI SHALL call `DELETE /spaces/{spaceId}/groups/{groupId}/alerts/{alertId}` and remove the alert from the list on success.
8. IF the delete API call returns an error, THEN THE UI SHALL display the error message in Hebrew.

---

### Requirement 9: Admin Delete Any Group Message

**User Story:** As a group admin, I want to delete any message in the group — not just my own — so that I can moderate the group's message board.

#### Acceptance Criteria

1. WHEN an authenticated user with `people.manage` permission calls `DELETE /spaces/{spaceId}/groups/{groupId}/messages/{messageId}`, THE System SHALL delete the message regardless of who authored it and return HTTP 204.
2. IF the requesting user does not hold `people.manage`, THEN THE System SHALL return HTTP 403.
3. IF the message is not found or does not belong to the specified `groupId` and `spaceId`, THEN THE System SHALL return HTTP 404.
4. THE System SHALL update `DeleteGroupMessageCommandHandler` to allow deletion when the caller holds `people.manage`, in addition to the existing author-or-owner check.
5. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before deleting the entity.
6. WHEN `adminGroupId` equals `groupId`, THE UI SHALL display a delete button on ALL messages in the "הודעות" tab, regardless of authorship.
7. WHEN the admin clicks the delete button, THE UI SHALL call `DELETE /spaces/{spaceId}/groups/{groupId}/messages/{messageId}` and remove the message from the list on success.
8. IF the delete API call returns an error, THEN THE UI SHALL display the error message in Hebrew.

---

### Requirement 10: Admin Pin/Unpin Group Message

**User Story:** As a group admin, I want to pin or unpin any message in the group, so that I can highlight important announcements for all members.

#### Acceptance Criteria

1. THE System SHALL expose a `PATCH /spaces/{spaceId}/groups/{groupId}/messages/{messageId}/pin` endpoint requiring `[Authorize]` and the `people.manage` permission.
2. WHEN a valid pin request is received with `{ "isPinned": true }`, THE System SHALL set `GroupMessage.IsPinned = true` and return HTTP 204.
3. WHEN a valid pin request is received with `{ "isPinned": false }`, THE System SHALL set `GroupMessage.IsPinned = false` and return HTTP 204.
4. IF the message is not found or does not belong to the specified `groupId` and `spaceId`, THEN THE System SHALL return HTTP 404.
5. IF the requesting user does not hold `people.manage`, THEN THE System SHALL return HTTP 403.
6. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before updating the entity.
7. WHEN `adminGroupId` equals `groupId`, THE UI SHALL display a pin/unpin toggle button on each message in the "הודעות" tab.
8. WHEN the admin clicks the pin button on an unpinned message, THE UI SHALL call `PATCH /spaces/{spaceId}/groups/{groupId}/messages/{messageId}/pin` with `{ "isPinned": true }` and update the message's pinned state in the list on success.
9. WHEN the admin clicks the unpin button on a pinned message, THE UI SHALL call `PATCH /spaces/{spaceId}/groups/{groupId}/messages/{messageId}/pin` with `{ "isPinned": false }` and update the message's pinned state in the list on success.
10. THE UI SHALL visually distinguish pinned messages from unpinned messages (e.g., a pin icon or highlighted border).
11. IF the pin/unpin API call returns an error, THEN THE UI SHALL display the error message in Hebrew.

---

### Requirement 11: Trigger Scheduling Solver

**User Story:** As a group admin, I want to trigger the scheduling solver from the group settings tab, so that I can generate a new draft schedule based on the current members, tasks, constraints, and availability.

#### Acceptance Criteria

1. WHEN `adminGroupId` equals `groupId`, THE UI SHALL display a "הפעל סידור" section in the "הגדרות" tab containing a "הפעל סידור" button.
2. WHEN the admin clicks "הפעל סידור", THE UI SHALL call `POST /spaces/{spaceId}/schedule-runs/trigger` with `{ "triggerMode": "standard" }` and store the returned `runId`.
3. IF the trigger API call returns an error, THEN THE UI SHALL display the error message in Hebrew and return to the idle state.
4. THE System SHALL require the `schedule.recalculate` permission for `POST /spaces/{spaceId}/schedule-runs/trigger` — this is already enforced in `ScheduleRunsController`.
5. IF the requesting user does not hold `schedule.recalculate`, THEN THE System SHALL return HTTP 403.

---

### Requirement 12: Poll Solver Run Status

**User Story:** As a group admin, I want to see a live progress indicator while the solver is running, so that I know the system is working and when it finishes.

#### Acceptance Criteria

1. WHILE a solver run is in progress (after a successful trigger), THE UI SHALL display a polling state in the "הגדרות" tab showing a spinner and the message "הסידור מחושב..." (Schedule is being calculated...).
2. WHILE polling, THE UI SHALL call `GET /spaces/{spaceId}/schedule-runs/{runId}` every 3 seconds.
3. WHEN the poll response returns `status` of `Completed`, THE UI SHALL stop polling and display a success message "הסידור הושלם! הטיוטה מוכנה לעיון." (Scheduling complete! Draft is ready for review.).
4. WHEN the poll response returns `status` of `Failed` or `TimedOut`, THE UI SHALL stop polling and display an error message in Hebrew describing the failure.
5. THE UI SHALL disable the "הפעל סידור" button while polling is active to prevent duplicate runs.
6. THE System SHALL require the `space.admin_mode` permission for `GET /spaces/{spaceId}/schedule-runs/{runId}` — this is already enforced in `ScheduleRunsController`.
7. IF the poll API call returns HTTP 404, THEN THE UI SHALL stop polling and display an error message "לא נמצא מידע על ריצת הסידור." (No information found for this scheduling run.).

---

### Requirement 13: Display Draft Schedule in סידור Tab

**User Story:** As a group admin, I want the draft schedule produced by the solver to appear automatically in the סידור tab after the run completes, so that I can review it before publishing.

#### Acceptance Criteria

1. WHEN the solver run status transitions to `Completed`, THE UI SHALL automatically re-fetch the schedule data in the "סידור" tab to display the new draft version.
2. THE UI SHALL visually distinguish the draft schedule from a published schedule (e.g., a "טיוטה" badge or banner).
3. WHEN a draft version exists, THE UI SHALL display a "פרסם סידור" (Publish Schedule) button in the "סידור" tab, visible only when `adminGroupId` equals `groupId`.
4. WHEN a draft version exists, THE UI SHALL display a "בטל טיוטה" (Discard Draft) button in the "סידור" tab, visible only when `adminGroupId` equals `groupId`.
5. WHEN no draft version exists, THE UI SHALL NOT render the "פרסם סידור" or "בטל טיוטה" buttons.

---

### Requirement 14: Publish Draft Schedule

**User Story:** As a group admin, I want to publish the draft schedule, so that all group members can see the finalized assignments.

#### Acceptance Criteria

1. WHEN the admin clicks "פרסם סידור", THE UI SHALL call `POST /spaces/{spaceId}/schedule-versions/{versionId}/publish`.
2. WHEN the publish call succeeds, THE UI SHALL re-fetch the schedule and display the now-published version without the "טיוטה" badge.
3. WHEN the publish call succeeds, THE UI SHALL remove the "פרסם סידור" and "בטל טיוטה" buttons.
4. IF the publish API call returns an error, THEN THE UI SHALL display the error message in Hebrew.
5. THE System SHALL require the `schedule.publish` permission for `POST /spaces/{spaceId}/schedule-versions/{versionId}/publish` — this is already enforced in `ScheduleVersionsController`.
6. IF the requesting user does not hold `schedule.publish`, THEN THE System SHALL return HTTP 403.
7. THE UI SHALL disable the "פרסם סידור" button while the publish request is in flight to prevent duplicate submissions.

---

### Requirement 15: Discard Draft Schedule

**User Story:** As a group admin, I want to discard a draft schedule, so that I can remove an unsatisfactory solver result and trigger a new run if needed.

#### Acceptance Criteria

1. THE System SHALL expose a `DELETE /spaces/{spaceId}/schedule-versions/{versionId}` endpoint that soft-deletes a draft version by setting its status to `Discarded`, requiring `[Authorize]` and the `schedule.publish` permission.
2. IF the version is not in `Draft` status, THEN THE System SHALL return HTTP 400 with the message "Only draft versions can be discarded."
3. IF the version is not found or does not belong to the specified `spaceId`, THEN THE System SHALL return HTTP 404.
4. IF the requesting user does not hold `schedule.publish`, THEN THE System SHALL return HTTP 403.
5. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before discarding the version.
6. WHEN the admin clicks "בטל טיוטה", THE UI SHALL display a confirmation dialog in Hebrew before proceeding.
7. WHEN the admin confirms, THE UI SHALL call `DELETE /spaces/{spaceId}/schedule-versions/{versionId}` and re-fetch the schedule on success.
8. WHEN the discard call succeeds, THE UI SHALL remove the "פרסם סידור" and "בטל טיוטה" buttons and display the previously published schedule (or an empty state if none exists).
9. IF the discard API call returns an error, THEN THE UI SHALL display the error message in Hebrew.

---

### Requirement 16: Task Migration Strategy

**User Story:** As a developer, I want the new `tasks` table to be introduced without breaking existing functionality, so that the migration can be deployed safely alongside the legacy `task_types` and `task_slots` tables.

#### Acceptance Criteria

1. THE System SHALL introduce the `tasks` table via a new database migration (migration 014) with all fields defined in the `Task` glossary entry.
2. THE System SHALL retain the existing `task_types` and `task_slots` tables without modification — these tables MUST NOT be dropped or altered by migration 014.
3. THE System SHALL configure the `tasks` table with a unique index on `(space_id, group_id, name)` to prevent duplicate task names within a group.
4. THE System SHALL configure `burden_level` as a VARCHAR(20) column with a CHECK constraint limiting values to `favorable`, `neutral`, `disliked`, and `hated`.
5. WHEN the solver payload builder runs, THE System SHALL read task data from the `tasks` table rather than from `task_types` and `task_slots`.
6. THE System SHALL set `is_active = true` as the default for all newly created `Task` records.

---

### Requirement 17: Edit Group Alert

**User Story:** As a group admin, I want to edit an alert's title, body, and severity, so that I can correct mistakes or update information without deleting and recreating the alert.

#### Acceptance Criteria

1. THE System SHALL expose a `PUT /spaces/{spaceId}/groups/{groupId}/alerts/{alertId}` endpoint requiring `[Authorize]` and the `people.manage` permission.
2. WHEN a valid update request is received, THE System SHALL update the `GroupAlert`'s `Title`, `Body`, and `Severity` fields and return HTTP 204.
3. THE System SHALL validate that `Title` is between 1 and 200 non-blank characters; IF invalid, THEN THE System SHALL return HTTP 400.
4. THE System SHALL validate that `Body` is between 1 and 2000 non-blank characters; IF invalid, THEN THE System SHALL return HTTP 400.
5. THE System SHALL validate that `Severity` is one of `info`, `warning`, or `critical` (case-insensitive); IF invalid, THEN THE System SHALL return HTTP 400.
6. IF the `GroupAlert` is not found or does not belong to the specified `groupId` and `spaceId`, THEN THE System SHALL return HTTP 404.
7. IF the requesting user does not hold `people.manage`, THEN THE System SHALL return HTTP 403.
8. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before updating the entity.
9. WHEN `adminGroupId` equals `groupId`, THE UI SHALL display an edit button on each alert in the "התראות" tab.
10. WHEN the admin clicks the edit button, THE UI SHALL open the alert form pre-populated with the alert's current `Title`, `Body`, and `Severity` values.
11. WHEN the admin submits the edit form, THE UI SHALL call `PUT /spaces/{spaceId}/groups/{groupId}/alerts/{alertId}` and re-fetch the alerts list on success.
12. IF the update API call returns an error, THEN THE UI SHALL display the error message in Hebrew below the form.

---

### Requirement 18: Edit Group Message

**User Story:** As a group admin, I want to edit any message's content in the group, so that I can correct errors or update information without deleting and reposting.

#### Acceptance Criteria

1. THE System SHALL expose a `PUT /spaces/{spaceId}/groups/{groupId}/messages/{messageId}` endpoint requiring `[Authorize]` and the `people.manage` permission.
2. WHEN a valid update request is received, THE System SHALL update the `GroupMessage`'s `Content` field and return HTTP 204.
3. THE System SHALL validate that `Content` is between 1 and 5000 non-blank characters; IF invalid, THEN THE System SHALL return HTTP 400.
4. IF the `GroupMessage` is not found or does not belong to the specified `groupId` and `spaceId`, THEN THE System SHALL return HTTP 404.
5. IF the requesting user does not hold `people.manage`, THEN THE System SHALL return HTTP 403.
6. THE System SHALL perform the permission check via `IPermissionService` in the Application layer before updating the entity.
7. WHEN `adminGroupId` equals `groupId`, THE UI SHALL display an edit button on each message in the "הודעות" tab.
8. WHEN the admin clicks the edit button, THE UI SHALL open an inline edit field or modal pre-populated with the message's current `Content`.
9. WHEN the admin submits the edit, THE UI SHALL call `PUT /spaces/{spaceId}/groups/{groupId}/messages/{messageId}` and update the message content in the list on success.
10. IF the update API call returns an error, THEN THE UI SHALL display the error message in Hebrew below the edit field.

---

### Requirement 19: Search People by Name or Phone

**User Story:** As a space member, I want to search for people in the space by name or phone number, so that I can quickly find someone without scrolling through the full list.

#### Acceptance Criteria

1. THE System SHALL expose a `GET /spaces/{spaceId}/people/search?q={query}` endpoint requiring `[Authorize]` and the `space.view` permission.
2. WHEN a valid search request is received, THE System SHALL return all `Person` records in the space where `FullName`, `DisplayName`, or `PhoneNumber` contains the query string (case-insensitive), ordered by `FullName` ascending.
3. THE System SHALL return at most 20 results per query.
4. IF `q` is empty or fewer than 2 characters, THE System SHALL return HTTP 400.
5. THE System SHALL include the following fields in each result: `id`, `fullName`, `displayName`, `phoneNumber`, `linkedUserId`.
6. THE UI SHALL display a search box above the members list in the "חברים" tab.
7. THE UI SHALL display a search box above the schedule in the "סידור" tab to search for a person's assignments.
8. WHEN the user types in the search box, THE UI SHALL filter the displayed list in real time (client-side for members already loaded; server-side for the people search endpoint).

---

### Requirement 20: Name-First Person Creation (Pending Invitation)

**User Story:** As a space admin, I want to add a person to the space by name only, so that I can build the roster before everyone has registered, and then invite them later.

#### Acceptance Criteria

1. THE System SHALL expose a `POST /spaces/{spaceId}/people` endpoint that accepts `{ fullName, displayName? }` without requiring a `linkedUserId`, requiring `[Authorize]` and the `people.manage` permission.
2. WHEN a valid create request is received with no `linkedUserId`, THE System SHALL insert a `Person` record with `InvitationStatus = "pending"` and return HTTP 201 with the new person's `id`.
3. THE System SHALL validate that `fullName` is between 1 and 100 non-blank characters; IF invalid, THEN THE System SHALL return HTTP 400.
4. THE System SHALL validate that no active `Person` with the same `fullName` (case-insensitive) already exists in the space; IF duplicate, THEN THE System SHALL return HTTP 409.
5. THE System SHALL expose a `POST /spaces/{spaceId}/people/{personId}/invite` endpoint requiring `[Authorize]` and the `people.manage` permission, accepting `{ contact, channel }` where `contact` is an email or phone number and `channel` is `"email"` or `"whatsapp"`.
6. WHEN a valid invite request is received, THE System SHALL store the contact on the `Person` record, create a `PendingInvitation` record with a secure token, and dispatch the invitation via `IInvitationSender`.
7. THE System SHALL validate that `contact` is a valid email address when `channel = "email"`, or a valid phone number when `channel = "whatsapp"`; IF invalid, THEN THE System SHALL return HTTP 400.
8. THE System SHALL expose a `POST /invitations/accept?token={token}` endpoint (no auth required) that links the `Person` to the authenticated or newly-registered `User` and sets `InvitationStatus = "accepted"`.
9. IF the token is expired (older than 7 days) or already used, THE System SHALL return HTTP 400.
10. WHEN a person is created without a `linkedUserId`, THE UI SHALL display them in the members list with a "ממתין לאישור" (Pending) badge.
11. WHEN `adminGroupId` equals `groupId`, THE UI SHALL display an "הזמן" (Invite) button next to each pending member.
12. WHEN the admin clicks "הזמן", THE UI SHALL open a form with a contact input (email or phone) and a channel selector (אימייל / WhatsApp).
13. WHEN the admin submits the invite form, THE UI SHALL call `POST /spaces/{spaceId}/people/{personId}/invite` and show a success message on completion.

---

### Requirement 21: Invitation Delivery Infrastructure

**User Story:** As a developer, I want a clean abstraction for sending invitations via email and WhatsApp, so that I can swap providers without changing business logic.

#### Acceptance Criteria

1. THE System SHALL define an `IInvitationSender` interface in the Application layer with a `SendInvitationAsync(string contact, string channel, string inviteUrl, string personName, CancellationToken ct)` method.
2. THE System SHALL implement a `NoOpInvitationSender` in Infrastructure that logs the invitation URL to the console (for local development).
3. THE System SHALL implement an `EmailInvitationSender` in Infrastructure that sends an HTML invitation email via `IEmailSender`.
4. THE System SHALL implement a `WhatsAppInvitationSender` in Infrastructure that sends an invitation message via `INotificationSender` (reusing the existing WhatsApp abstraction).
5. THE System SHALL select the correct sender implementation based on the `channel` field: `"email"` → `EmailInvitationSender`, `"whatsapp"` → `WhatsAppInvitationSender`.
6. THE System SHALL NOT implement SMS as a channel — WhatsApp is the preferred mobile channel for this application.
7. THE System SHALL store the invitation token hashed (SHA-256) in the `pending_invitations` table; the raw token is only sent to the recipient and never stored in plaintext.
8. THE System SHALL introduce migration 015 to add the `pending_invitations` table and the `invitation_status` column to the `people` table.

---

### Requirement 22: Wire Up In-App Notifications

**User Story:** As a user, I want to receive in-app notifications when important events happen (added to a group, schedule published, ownership transfer), so that I stay informed without checking every tab.

#### Acceptance Criteria

1. WHEN a user is added to a group (via `AddPersonByEmailCommand` or `AddPersonByPhoneCommand`), THE System SHALL create a `Notification` for that user with `EventType = "group.member_added"`.
2. WHEN a schedule version is published (via `PublishVersionCommand`), THE System SHALL create a `Notification` for every active member of the space with `EventType = "schedule.published"`.
3. WHEN an ownership transfer is confirmed (via `ConfirmOwnershipTransferCommand`), THE System SHALL create a `Notification` for the new owner with `EventType = "group.ownership_transferred"`.
4. THE System SHALL create notifications in the Application layer handlers, not in controllers.
5. THE UI SHALL display the notification bell in the sidebar as a compact icon (not in the topbar), showing an unread count badge when there are unread notifications.
6. THE UI SHALL show a dropdown panel when the bell is clicked, listing the 10 most recent notifications with title, body, and relative time.
7. THE UI SHALL poll for new notifications every 30 seconds while the user is logged in.
8. WHEN the user clicks a notification, THE UI SHALL mark it as read and navigate to the relevant page if a link is available in `MetadataJson`.
9. THE UI SHALL display a "סמן הכל כנקרא" (Mark all as read) button in the notification dropdown.
