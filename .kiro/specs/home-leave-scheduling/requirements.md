# Requirements Document

## Introduction

This feature introduces **home-leave scheduling** for closed-base army groups. In these groups, personnel live on-base and rotate between missions continuously. The current scheduler assigns people to tasks but has no concept of "going home" — making the schedule incomplete and impractical for real military operations.

Home-leave scheduling fundamentally extends the solver to:
1. Guarantee minimum rest between missions (hard constraint).
2. Enforce a maximum rest window of 24 hours, after which a person must either be assigned a new mission or sent home on leave (hard/soft configurable constraint).
3. Calculate optimal home-leave slots — determining how many people can go home simultaneously while maintaining operational coverage.
4. Produce a balanced ratio of time-at-base vs. time-at-home across all personnel.

This requires new constraint types in the domain, modifications to the solver payload and CP-SAT model, a new UI for configuring home-leave rules, and a new output visualization showing base-time vs. home-time distribution.

---

## Glossary

- **System**: The Jobuler backend (ASP.NET Core API + Application + Domain + Infrastructure layers).
- **Solver**: The Python CP-SAT scheduling engine (`apps/solver`) using Google OR-Tools.
- **UI**: The Jobuler Next.js frontend (`apps/web`).
- **Closed_Base_Group**: A group configured with `is_closed_base = true`, indicating personnel live on-base and require home-leave scheduling.
- **Home_Leave_Slot**: A time window during which a person is scheduled to be away from base (at home). Modeled as a special assignment type in the solver output.
- **Min_Rest_Constraint**: A hard constraint specifying the minimum hours of rest a person must have between consecutive mission assignments. Already partially exists as `min_rest_hours` rule type.
- **Max_Rest_Constraint**: A soft eligibility threshold specifying that after a configured number of hours of rest at base (e.g., 24h), a person becomes eligible to be sent home on leave. This is never a hard constraint — the only hard constraint is min rest.
- **Home_Leave_Capacity**: The maximum number of people who can be on home-leave simultaneously while maintaining operational coverage. Configured per group.
- **Home_Leave_Duration**: The configured duration (in hours) for a single home-leave rotation.
- **Base_Time_Ratio**: The proportion of total horizon time a person spends at base (on missions + resting between missions) vs. at home on leave.
- **Presence_State**: The state of a person at any point in time: `on_mission`, `free_in_base`, `at_home`, `blocked`.
- **Home_Leave_Template**: A reusable configuration template for closed-base groups that bundles min rest, max rest, home-leave capacity, and duration settings.
- **Solver_Payload**: The JSON input sent to the solver service containing all scheduling context (people, slots, constraints, etc.).
- **Home_Leave_Metrics**: Output statistics showing per-person base-time vs. home-time distribution.
- **Group_Admin**: A user with the `constraints.manage` permission scoped to the group's space.
- **IPermissionService**: The Application-layer interface used for all permission checks.
- **ConstraintRule**: The domain entity in the `constraint_rules` table.

---

## Requirements

### Requirement 1: Closed-Base Group Configuration

**User Story:** As a group admin, I want to mark a group as a closed-base group, so that the system enables home-leave scheduling features for that group.

#### Acceptance Criteria

1. WHEN a group admin opens the group settings page for a group, THE UI SHALL display a "בסיס סגור" (Closed Base) toggle switch reflecting the current `is_closed_base` value for that group (default: off for new groups).
2. WHEN the admin toggles the "בסיס סגור" switch, THE UI SHALL call `PUT /spaces/{spaceId}/groups/{groupId}` with `isClosedBase: true` or `isClosedBase: false` corresponding to the new toggle state.
3. THE System SHALL require the `constraints.manage` permission on the group's space before accepting the `isClosedBase` update; IF the caller lacks this permission, THEN THE System SHALL return HTTP 403.
4. THE System SHALL persist the `is_closed_base` boolean flag (default `false`) on the `groups` table.
5. IF the group does not exist or does not belong to the specified space, THEN THE System SHALL return HTTP 404.
6. WHILE `is_closed_base` is `true` for a group, THE UI SHALL display the home-leave configuration section in the group settings.
7. WHILE `is_closed_base` is `false` for a group, THE UI SHALL hide the home-leave configuration section entirely; existing home-leave configuration data SHALL be preserved in the database and restored when the toggle is re-enabled.

---

### Requirement 2: Home-Leave Rule Configuration

**User Story:** As a group admin of a closed-base group, I want to configure home-leave rules (min rest, max rest, leave capacity, leave duration), so that the solver respects these parameters when generating schedules.

#### Acceptance Criteria

1. WHILE `is_closed_base` is `true` for the group, THE UI SHALL display a "הגדרות חופשות" (Leave Settings) panel with the following configurable fields: minimum rest hours (numeric, default 8, HARD — never violated), home-leave eligibility threshold hours (numeric, default 24 — soft trigger for when people become eligible to go home), home-leave capacity (numeric — max people on leave simultaneously, default 1), and home-leave duration hours (numeric, default 48).
2. WHEN the admin submits the leave settings form, THE UI SHALL call `PUT /spaces/{spaceId}/groups/{groupId}/home-leave-config` with the configured values.
3. THE System SHALL persist home-leave configuration in a `home_leave_configs` table with columns: `group_id`, `space_id`, `min_rest_hours`, `eligibility_threshold_hours`, `leave_capacity`, `leave_duration_hours`, `created_at`, `updated_at`.
4. IF `min_rest_hours` is less than 4 or greater than 16, THEN THE System SHALL reject the request with HTTP 400 and an error message indicating the allowed range is 4–16 inclusive.
5. IF `eligibility_threshold_hours` is less than `min_rest_hours` or greater than 48, THEN THE System SHALL reject the request with HTTP 400 and an error message indicating the threshold must be between min_rest_hours and 48 inclusive.
6. IF `leave_capacity` is less than 1 or greater than (total group member count minus 1), THEN THE System SHALL reject the request with HTTP 400 and an error message indicating the allowed range.
7. IF `leave_duration_hours` is less than 12 or greater than 168, THEN THE System SHALL reject the request with HTTP 400 and an error message indicating the allowed range is 12–168 inclusive.
8. THE System SHALL require the `constraints.manage` permission for the home-leave config endpoint; IF the caller lacks this permission, THEN THE System SHALL return HTTP 403.
9. WHEN the admin opens the leave settings panel for a group that has no saved configuration, THE UI SHALL display all fields populated with their default values (min rest: 8, eligibility threshold: 24, capacity: 1, duration: 48).

---

### Requirement 3: Minimum Rest Hard Constraint in Solver

**User Story:** As a group admin, I want the solver to guarantee minimum rest between missions as a hard constraint, so that personnel always get adequate rest regardless of scheduling pressure.

#### Acceptance Criteria

1. WHEN the solver payload is built for a closed-base group, THE System SHALL include a hard constraint with `rule_type = "min_rest_hours"`, `scope_type = "group"`, `scope_id = <group_id>`, and `payload = { "hours": <configured_min_rest_hours> }` where the hours value is read from the group's `home_leave_configs` record.
2. WHILE the `min_rest_hours` constraint is active, THE Solver SHALL prevent assigning a person to any task slot that starts less than `min_rest_hours` after their previous assignment ends, regardless of slot duration — the long-shift soft-penalty exception SHALL NOT apply when this constraint originates from a closed-base home-leave configuration.
3. THE Solver SHALL treat the minimum rest constraint as a hard constraint that cannot be violated — the solver returns INFEASIBLE rather than violating minimum rest.
4. IF emergency bypass constraints are active for a person, THEN THE Solver SHALL skip the `min_rest_hours` enforcement for that person, consistent with existing emergency bypass behavior.
5. IF the solver returns INFEASIBLE and minimum rest conflicts contributed to infeasibility, THEN THE Solver SHALL include a `HardConflict` entry with `rule_type = "min_rest_violation"`, `affected_person_ids` containing the person(s) whose rest would be violated, and `affected_slot_ids` containing the pair of conflicting slot IDs that cannot both be assigned without violating the rest window.

---

### Requirement 4: Home-Leave Eligibility Threshold (Rest Trigger)

**User Story:** As a group admin, I want the solver to recognize when a person has rested long enough to be eligible for home-leave, so that the solver can send people home once they've had sufficient rest without forcing them to stay idle at base.

#### Acceptance Criteria

1. WHEN the solver payload is built for a closed-base group, THE System SHALL include a constraint with `rule_type = "home_leave_eligibility"` and `payload = { "threshold_hours": <configured_max_rest_hours> }`.
2. WHEN a person's continuous `free_in_base` time (measured from the end of their last assignment or the horizon start) reaches or exceeds `threshold_hours`, THE Solver SHALL mark that person as eligible for home-leave assignment.
3. THE Solver MAY assign a person to home-leave before reaching `threshold_hours` if doing so improves fairness and the person has already satisfied `min_rest_hours`; the threshold is a soft eligibility signal, not a hard gate.
4. WHEN a person becomes eligible for home-leave and home-leave capacity is available, THE Solver SHALL prefer assigning that person to a home-leave slot over leaving them idle at base, weighted as a soft objective.
5. IF home-leave capacity is full when a person becomes eligible, THE Solver SHALL keep the person in `free_in_base` state and re-evaluate eligibility when capacity becomes available.
6. THE Solver SHALL NOT treat exceeding `threshold_hours` without going home as infeasible — it is always a soft preference, never a hard constraint. The only hard constraint in the rest system is `min_rest_hours`.

---

### Requirement 5: Home-Leave Slot Generation

**User Story:** As a group admin, I want the solver to automatically generate home-leave slots and assign people to them, so that the schedule includes explicit leave periods alongside mission assignments.

#### Acceptance Criteria

1. WHEN solving for a closed-base group, THE Solver SHALL generate home-leave slots as decision variables in the CP-SAT model, with each slot representing a contiguous window of exactly `leave_duration_hours` hours.
2. THE Solver SHALL enforce that at most `leave_capacity` people are on home-leave during any given hour within the scheduling horizon.
3. THE Solver SHALL ensure that a person assigned to a home-leave slot is not assigned to any mission slot that overlaps with their leave window.
4. THE Solver SHALL include home-leave assignments in the `home_leave_assignments` output field as objects containing `person_id`, `starts_at`, and `ends_at`, with `source = "solver"`.
5. WHEN a person is assigned a home-leave slot, THE Solver SHALL set their presence state to `at_home` for the duration of that slot.
6. THE Solver SHALL distribute home-leave assignments such that the maximum difference in `base_time_ratio` between any two group members does not exceed 10 percentage points.
7. IF the solver cannot assign home-leave slots without violating operational coverage (i.e., fewer than `group_member_count - leave_capacity` people would remain available for missions), THEN THE Solver SHALL prioritize operational coverage and reduce the number of concurrent home-leave assignments accordingly.
8. THE Solver SHALL not assign more than one home-leave slot to the same person at a time; a person must return to `free_in_base` state before being eligible for a subsequent home-leave slot.

---

### Requirement 6: Fairness in Base-Time vs. Home-Time Distribution

**User Story:** As a group admin, I want the solver to distribute home-leave fairly across all personnel, so that everyone gets approximately equal time at home over the scheduling horizon.

#### Acceptance Criteria

1. THE Solver SHALL calculate a `base_time_ratio` for each person defined as: (total hours on missions + total hours resting at base) / total horizon hours, yielding a value between 0.0 and 1.0 inclusive.
2. WHEN calculating `base_time_ratio`, THE Solver SHALL use only the hours during which a person is available within the horizon (excluding hours where the person is in `blocked` state or has not yet joined the group), so that partial-availability members are compared on an equal basis.
3. THE Solver SHALL include a soft objective that minimizes the maximum deviation of any individual's `base_time_ratio` from the group mean `base_time_ratio`.
4. THE Solver SHALL weight the fairness objective with a value between 100 and 999 (inclusive), placing it below operational coverage constraints (weight ≥ 1000) and above burden-level preferences (weight ≤ 99).
5. WHEN the solver output is returned, THE System SHALL include per-person `Home_Leave_Metrics` containing: `total_base_hours` (decimal, 2 decimal places), `total_home_hours` (decimal, 2 decimal places), `base_time_ratio` (decimal, 4 decimal places), and `leave_slot_count` (integer ≥ 0).
6. IF a group contains fewer than 2 members eligible for home-leave, THEN THE Solver SHALL skip the fairness objective and produce `Home_Leave_Metrics` without applying fairness balancing.
7. WHEN the solver output is returned, THE System SHALL include a `fairness_variance` field (decimal, 6 decimal places) representing the variance of `base_time_ratio` across all eligible group members.

---

### Requirement 7: Solver Payload Extension for Home-Leave

**User Story:** As a developer, I want the solver input payload to include home-leave configuration, so that the solver has all necessary context to generate leave schedules.

#### Acceptance Criteria

1. THE System SHALL extend the `SolverInput` model with an optional `home_leave_config` field containing: `enabled` (bool), `min_rest_hours` (float), `eligibility_threshold_hours` (float), `leave_capacity` (int), and `leave_duration_hours` (float).
2. IF `home_leave_config.enabled` is `false` or the `home_leave_config` field is absent from the payload, THEN THE Solver SHALL skip all home-leave constraint generation and produce output identical in structure and assignments to a run without the field present.
3. THE System SHALL include `home_leave_config` in the solver payload only for groups where `is_closed_base = true` and a `home_leave_configs` record exists with non-null values for all required fields (`min_rest_hours`, `eligibility_threshold_hours`, `leave_capacity`, `leave_duration_hours`).
4. IF `is_closed_base` is `true` for a group but no `home_leave_configs` record with all required fields exists, THEN THE System SHALL omit the `home_leave_config` field from the solver payload for that group and log a warning including the group ID.
5. THE System SHALL populate the `home_leave_config` field during the `ISolverPayloadNormalizer.BuildAsync` execution, using the `home_leave_configs` record associated with the target group.

---

### Requirement 8: Solver Output Extension for Home-Leave

**User Story:** As a developer, I want the solver output to include home-leave assignments and metrics, so that the API can store and display leave schedules.

#### Acceptance Criteria

1. THE Solver SHALL extend the `SolverOutput` model with a `home_leave_assignments` field: a list of objects each containing `person_id` (string), `starts_at` (ISO 8601 UTC datetime string), and `ends_at` (ISO 8601 UTC datetime string), where `starts_at` is strictly before `ends_at`.
2. THE Solver SHALL extend the `SolverOutput` model with a `home_leave_metrics` field: a list of per-person objects each containing `person_id` (string), `total_base_hours` (float), `total_home_hours` (float), `base_time_ratio` (float between 0.0 and 1.0 inclusive, rounded to 4 decimal places), and `leave_slot_count` (non-negative integer).
3. WHEN `home_leave_config` is not enabled (either `enabled` is `false` or the `home_leave_config` field is absent from the input), THE Solver SHALL return empty lists for `home_leave_assignments` and `home_leave_metrics`.
4. WHEN the API receives solver output containing `home_leave_assignments`, THE System SHALL store each home-leave assignment in the `assignments` table with `source = "solver"` and a reference to a synthetic task slot of `task_type = "home_leave"`, linked to the corresponding schedule version.
5. IF the solver output contains a `home_leave_assignments` entry where `person_id` does not match a known group member or where `starts_at` is not before `ends_at`, THEN THE System SHALL discard that entry and include a warning in the schedule run log.

---

### Requirement 9: Home-Leave Visualization in Schedule Output

**User Story:** As a group admin, I want to see a visualization of base-time vs. home-time for each person in the schedule output, so that I can verify the schedule is fair and complete.

#### Acceptance Criteria

1. WHEN viewing a schedule version for a closed-base group, THE UI SHALL display a "זמן בבסיס / בבית" (Base/Home Time) panel showing per-person statistics.
2. WHEN the "זמן בבסיס / בבית" panel is displayed, THE UI SHALL display for each person, sorted alphabetically by name: name, total base hours (rounded to the nearest integer), total home hours (rounded to the nearest integer), base-time ratio as a percentage (rounded to 1 decimal place), and number of leave slots.
3. THE UI SHALL display a horizontal stacked bar chart per person showing the proportion of base-time (one color) vs. home-time (another color).
4. WHEN the difference between the highest and lowest `base_time_ratio` percentages across all group members exceeds 15 percentage points, THE UI SHALL display a fairness warning indicator within the "זמן בבסיס / בבית" panel.
5. WHEN viewing a schedule version that contains home-leave assignments, THE UI SHALL display home-leave slots on the schedule timeline/Gantt view with a distinct visual style (different color and "בבית" label).
6. IF the schedule version has no `home_leave_metrics` data (e.g., solver ran without home-leave enabled or group was not closed-base at solve time), THEN THE UI SHALL hide the "זמן בבסיס / בבית" panel entirely.

---

### Requirement 10: Home-Leave Template Management

**User Story:** As a group admin, I want to save and load home-leave configuration templates, so that I can quickly apply standard configurations to new closed-base groups.

#### Acceptance Criteria

1. WHEN the admin is on the home-leave configuration panel, THE UI SHALL display a "שמור כתבנית" (Save as Template) button.
2. WHEN the admin clicks "שמור כתבנית", THE UI SHALL prompt for a template name and call `POST /spaces/{spaceId}/home-leave-templates` with the current configuration and the template name.
3. THE System SHALL persist templates in a `home_leave_templates` table with columns: `id`, `space_id`, `name`, `min_rest_hours`, `eligibility_threshold_hours`, `leave_capacity`, `leave_duration_hours`, `created_at`.
4. WHEN the admin is on the home-leave configuration panel, THE UI SHALL display a "טען תבנית" (Load Template) dropdown listing all saved templates for the space, sorted by `created_at` descending.
5. WHEN the admin selects a template from the dropdown, THE UI SHALL populate the configuration form with the template's values without saving automatically.
6. THE System SHALL require the `constraints.manage` permission for template CRUD operations.
7. IF a duplicate template name is submitted within the same space, THEN THE System SHALL return HTTP 409 with an error message indicating the name is already in use.
8. THE System SHALL validate that template names are between 1 and 100 characters in length and contain no leading or trailing whitespace; IF the name is invalid, THEN THE System SHALL return HTTP 400 with an error message indicating the naming constraint violated.
9. WHEN the admin requests deletion of a template via `DELETE /spaces/{spaceId}/home-leave-templates/{templateId}`, THE System SHALL remove the template record and return HTTP 204.
10. IF the admin requests deletion or loading of a template that does not exist, THEN THE System SHALL return HTTP 404.

---

### Requirement 11: Presence State Transitions for Home-Leave

**User Story:** As a developer, I want the system to correctly manage presence state transitions when home-leave is scheduled, so that availability constraints and the schedule timeline reflect leave periods accurately.

#### Acceptance Criteria

1. WHEN a schedule version containing home-leave assignments is published, THE System SHALL create one `presence_windows` record per home-leave assignment with `state = "at_home"`, `is_derived = true`, `person_id`, `starts_at`, `ends_at`, and `space_id` matching the corresponding home-leave assignment values.
2. IF a newly created `at_home` presence window would overlap with an existing `on_mission` presence window for the same person, THEN THE System SHALL reject the publish operation and return an error indicating the conflicting person and time range.
3. WHEN the solver builds the next schedule run, THE System SHALL include in the solver payload all published `at_home` presence windows whose `ends_at` is after the schedule horizon start and whose `starts_at` is before the schedule horizon end — preventing the solver from assigning missions during those windows.
4. IF a group admin manually cancels a home-leave assignment (via schedule override), THEN THE System SHALL delete the corresponding `at_home` presence window and set the person's presence state to `free_in_base` for the remaining duration of the cancelled window (from the current time or the window's `starts_at`, whichever is later, through the window's `ends_at`).
5. THE System SHALL require the `schedule.publish` permission before creating or removing `at_home` presence windows derived from home-leave assignments.
6. IF a group admin cancels a home-leave assignment whose `starts_at` is in the past and `ends_at` is in the future, THEN THE System SHALL truncate the `at_home` presence window to end at the current timestamp rather than deleting it entirely.

---

### Requirement 12: Database Schema for Home-Leave

**User Story:** As a developer, I want the database schema to support home-leave configuration and assignments, so that all home-leave data is persisted and queryable.

#### Acceptance Criteria

1. THE System SHALL add an `is_closed_base` boolean column (NOT NULL, default `false`) to the `groups` table.
2. THE System SHALL create a `home_leave_configs` table with columns: `id` (UUID PK), `group_id` (FK to `groups`, NOT NULL, UNIQUE, ON DELETE CASCADE), `space_id` (FK to `spaces`, NOT NULL, ON DELETE CASCADE), `min_rest_hours` (decimal NOT NULL), `eligibility_threshold_hours` (decimal NOT NULL), `leave_capacity` (int NOT NULL), `leave_duration_hours` (decimal NOT NULL), `created_at` (timestamptz NOT NULL DEFAULT NOW()), `updated_at` (timestamptz NOT NULL DEFAULT NOW()).
3. THE System SHALL create a `home_leave_templates` table with columns: `id` (UUID PK), `space_id` (FK to `spaces`, NOT NULL, ON DELETE CASCADE), `name` (varchar(100) NOT NULL), `min_rest_hours` (decimal NOT NULL), `eligibility_threshold_hours` (decimal NOT NULL), `leave_capacity` (int NOT NULL), `leave_duration_hours` (decimal NOT NULL), `created_at` (timestamptz NOT NULL DEFAULT NOW()); with a unique index on (`space_id`, `name`).
4. THE System SHALL add RLS policies to `home_leave_configs` and `home_leave_templates` using `space_id = current_setting('app.current_space_id', TRUE)::UUID` as the policy predicate.
5. THE System SHALL implement `ITenantScoped` on the `HomeLeaveConfig` and `HomeLeaveTemplate` domain entities, exposing the `SpaceId` property.
6. THE System SHALL create database migrations for all schema changes as sequentially numbered SQL files in `infra/migrations/`, including indexes on `group_id` for `home_leave_configs` and on `space_id` for `home_leave_templates`, and an `updated_at` trigger on `home_leave_configs`.
7. THE System SHALL enforce a one-to-one relationship between `home_leave_configs` and `groups` via the UNIQUE constraint on `group_id`; IF a second config is inserted for the same group, THEN THE System SHALL reject the insert with a unique constraint violation.
